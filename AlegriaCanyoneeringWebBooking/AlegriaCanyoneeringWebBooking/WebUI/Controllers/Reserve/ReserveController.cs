using AlegriaCanyoneeringWebBooking.Domain.Models;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
    public class ReserveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;
        private readonly IGuestService _guestService;

        public ReserveController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<GuestController> logger,
            IGuestService guestService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _guestService = guestService;

            if (!_context.Database.CanConnect())
                throw new Exception("Cannot connect to database. Please check your connection string.");
        }

        // =========================================================
        // SHARED HELPER — 1 staff per 2 guests, rounded up
        // e.g. 1-2 guests = 1, 3-4 = 2, 5-6 = 3 ...
        // =========================================================
        private static int GetRequiredStaffCount(int guestCount)
        {
            if (guestCount <= 0) return 0;
            return (int)Math.Ceiling(guestCount / 2.0);
        }





        // =========================================================
        // PRINT BATCH GUESTS
        // =========================================================
        [HttpGet, HttpPost]
        public IActionResult PrintBatchGuests(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("Batch code is required.");

            var rawGuests = _context.Guests
                .Include(g => g.Operators)
                .Where(g => g.Batch == batchCode)
                .ToList();

            var guests = rawGuests.Select(g => new
            {
                g.OperatorId,
                FullName = g.Fullname ?? "Unknown Guest",
                ArrivalDate = ParseUnixTimestamp(g.ArrivalDate),
                WristbandCode = g.RFIDCode,
                QRBase64 = GenerateQRCodeBase64(g.Operators?.Id.ToString() ?? "0"),
                Operators = g.Operators?.BusinessName ?? "No Operators"
            }).ToList();

            if (!guests.Any())
                return NotFound("No guests found for this batch.");

            ViewBag.BatchCode = batchCode;
            return View("PrintBatchGuests", guests);
        }

        // =========================================================
        // SCAN GUEST INFO
        // =========================================================
        [HttpGet]
        public IActionResult ScanGuestInfo(string? qrCodeValue)
        {
            if (string.IsNullOrEmpty(qrCodeValue))
            {
                TempData["ToastMessage"] = "Please scan a QR code before submitting.";
                TempData["ToastType"] = "warning";
                return View("ScanGuestInfo");
            }

            if (!int.TryParse(qrCodeValue, out int guestId))
            {
                TempData["ToastMessage"] = "Invalid QR code format. Must contain numeric GuestID.";
                TempData["ToastType"] = "danger";
                return View("ScanGuestInfo");
            }

            var guest = _context.Guests
                .Include(g => g.Operators)
                .Include(g => g.NationalityEntity)
                .FirstOrDefault(g => g.OperatorId == guestId);

            if (guest == null)
            {
                TempData["ToastMessage"] = "Guest not found for this QR code.";
                TempData["ToastType"] = "danger";
                return View("ScanGuestInfo");
            }

            var guestImage = _context.GuestImage
                .FirstOrDefault(i => i.WristbondGuestCode == guest.RFIDCode);

            byte[] imageBytes;
            string? imageBase64;

            if (guestImage != null && guestImage.Image?.Length > 0)
            {
                imageBytes = guestImage.Image;
                imageBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            }
            else
            {
                string defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/default_guest.png");
                imageBytes = System.IO.File.Exists(defaultImagePath)
                    ? System.IO.File.ReadAllBytes(defaultImagePath)
                    : Array.Empty<byte>();
                imageBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            }

            string hex = guest.RFIDCode.Replace(" ", "");
            string firstPart = hex.Substring(0, 8);
            uint numericId = Convert.ToUInt32(firstPart, 16);
            string wristBondCode = numericId.ToString().PadLeft(11, '0');

            var briefing = _context.GuestBriefings
                .FirstOrDefault(b => b.BWristBondCode == wristBondCode && b.BGuestName == guest.Fullname);

            if (briefing == null)
            {
                briefing = new GuestBriefing
                {
                    BWristBondCode = wristBondCode,
                    BGuestName = guest.Fullname,
                    BDateArrival = guest.Date != null ? DateTime.Parse(guest.Date).ToString("ddd MMM dd yyyy HH:mm:ss") : "-----",
                    BDateDeparture = DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss"),
                    BDateCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    BGuestImage = imageBytes
                };
                _context.GuestBriefings.Add(briefing);
                _context.SaveChanges();
            }

            DateTime arrivalDate;
            if (!string.IsNullOrEmpty(guest.ArrivalDate) && long.TryParse(guest.ArrivalDate, out long unix))
                arrivalDate = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            else
                arrivalDate = DateTime.Now;

            var model = new GuestDetailsViewModel
            {
                FullName = guest.Fullname,
                ArrivalDate = arrivalDate,
                WristbandCode = wristBondCode,
                QRText = briefing.BDateCode,
                Operators = guest.Operators?.BusinessName ?? "No Operator",
                Age = guest.Age,
                Nationality = guest.NationalityEntity?.NatName ?? "Unknown",
                GuestImageBase64 = imageBase64
            };

            TempData["ToastMessage"] = "Guest found successfully.";
            TempData["ToastType"] = "success";
            return View("ScanGuestInfo", model);
        }

        private DateTime? ParseUnixTimestamp(string? unixTimestamp)
        {
            if (string.IsNullOrEmpty(unixTimestamp)) return null;
            if (long.TryParse(unixTimestamp, out long seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;
            return null;
        }

        // =========================================================
        // INDEX
        // =========================================================
        public IActionResult Index()
        {
            var viewModel = new GuestListViewModel
            {
                ReservedGuests = new List<Guest>()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyBatchExistence(string Batch)
        {
            try
            {
                if (string.IsNullOrEmpty(Batch))
                    return Json(new { exists = false, error = "Batch code is required" });

                string batchNumbers = Batch.StartsWith("BATCH-") ? Batch.Substring(6) : Batch;

                var batchExists = await _context.Guests
                    .AnyAsync(g => g.Batch == batchNumbers && g.BookingStatus == 2);

                return Json(new { exists = batchExists });
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message });
            }
        }




        [HttpPost]
        public async Task<IActionResult> GetReservedGuestsData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = Math.Min(int.Parse(Request.Form["length"].FirstOrDefault() ?? "10"), 100);
                var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                int? currentOperatorId = null;
                if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                    currentOperatorId = parsedId;

                var query = from g in _context.Guests.AsNoTracking()
                            join o in _context.Operators.AsNoTracking()
                                on g.OperatorId equals o.Id into opGroup
                            from operatorItem in opGroup.DefaultIfEmpty()
                            where g.BookingStatus == 2
                            select new
                            {
                                Guest = g,
                                OperatorName = operatorItem != null ? operatorItem.BusinessName : "No Operator"
                            };

                if (currentOperatorId.HasValue)
                    query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(x =>
                        x.Guest.Fullname.ToLower().Contains(searchValue) ||
                        x.OperatorName.ToLower().Contains(searchValue) ||
                        x.Guest.Batch.ToLower().Contains(searchValue));

                var recordsTotal = await query.CountAsync();

                var totalBatchesQuery = query
                    .GroupBy(x => new { x.Guest.Batch, x.Guest.OperatorId })
                    .Select(grp => grp.Key);

                var recordsFiltered = await totalBatchesQuery.CountAsync();

                var groupedData = await query
                    .OrderBy(x => x.Guest.OperatorId)
                    .ThenBy(x => x.Guest.Batch)
                    .GroupBy(x => new { x.Guest.Batch, x.Guest.OperatorId, x.OperatorName })
                    .Select(grp => new
                    {
                        Batch = grp.Key.Batch,
                        OperatorId = grp.Key.OperatorId,
                        OperatorName = grp.Key.OperatorName,
                        TotalGuests = grp.Count(),
                        ArrivalDate = grp.Min(x => x.Guest.ArrivalDate),
                        RegistrationDate = grp.Min(x => x.Guest.Date),
                        MainGuestId = grp.OrderBy(x => x.Guest.Id).First().Guest.Id,
                        FirstGuest = grp.OrderBy(x => x.Guest.Id).First().Guest
                    })
                    .Skip(start)
                    .Take(length)
                    .ToListAsync();

                var result = groupedData.Select(g => new
                {
                    id = g.MainGuestId,
                    batch = g.Batch,
                    operatorName = g.OperatorName ?? "No Operator",
                    totalGuests = g.TotalGuests,
                    arrivalDate = g.ArrivalDate,
                    registrationDate = g.RegistrationDate,
                    bookingStatus = "reserved",
                    rfid = g.FirstGuest?.RFID ?? 0,
                    qrBase64 = GenerateQrCode(g.Batch)
                }).ToList();

                return Json(new { draw, recordsFiltered, recordsTotal, data = result });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Request.Form["draw"].FirstOrDefault(),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        private string GenerateQrCode(string batchCode)
        {
            try
            {
                if (string.IsNullOrEmpty(batchCode)) return "";
                return $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=BATCH-{Uri.EscapeDataString(batchCode)}&format=png";
            }
            catch { return ""; }
        }





        public async Task<IActionResult> reservebooking()
        {
            // ✅ Get current user's ID and Role from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
            {
                currentOperatorId = parsedId;
            }

            // 1. Get all operators from tbl_operator_mobile
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            // 2. Load reserved guests using int status = 2
            var reservedGuestsQuery = _context.Guests
                .Include(g => g.NationalityEntity)
                .Where(g => g.BookingStatus == 2);  // changed here from "reserved" to int 2

            // ✅ Filter by current operator if user is Operator
            if (currentOperatorId.HasValue)
            {
                reservedGuestsQuery = reservedGuestsQuery
                    .Where(g => g.OperatorId == currentOperatorId.Value);
            }

            var reservedGuests = await reservedGuestsQuery
                .OrderBy(g => g.Id)
                .ToListAsync();

            if (!reservedGuests.Any())
                return View(new GuestListViewModel());

            // 3. Generate QR for each guest
            foreach (var guest in reservedGuests)
            {
                guest.QRText = GenerateQRText(guest);
                guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
            }

            // 4. Group guests by Batch and map Operator BusinessName
            var grouped = reservedGuests
                .GroupBy(g => g.Batch)
                .Select(grp =>
                {
                    var first = grp.First();

                    // ✅ Lookup Operator's BusinessName from tbl_operator_mobile
                    var businessName = operators
                        .FirstOrDefault(o => o.Id == first.OperatorId)?.BusinessName ?? "No Operator";

                    return new Guest
                    {
                        Id = first.Id,
                        Fullname = first.Fullname,
                        Gender = first.Gender,
                        NationalityEntity = first.NationalityEntity,
                        OperatorId = first.OperatorId,

                        // ✅ Inject BusinessName using a stubbed OperatorList object
                        Operators = new Operator
                        {
                            BusinessName = businessName
                        },

                        RFID = grp.Count(x => x.BookingStatus != 1),  // 1 = canceled
                        ArrivalDate = first.ArrivalDate,
                        BookingStatus = first.BookingStatus,
                        Date = first.Date,
                        QRText = first.QRText,
                        QRBase64 = first.QRBase64,
                        Batch = first.Batch
                    };
                })
                .ToList();

            // 5. Generate batch QR
            string batchCode = reservedGuests.First().Batch;
            string batchQrBase64 = GenerateQRCodeBase64(batchCode);


            var vm = new GuestListViewModel
            {
                ReservedGuests = grouped,
                BatchQrBase64 = batchQrBase64
            };

            return View(vm);
        }


        private string GenerateQRText(Guest guest) => $"Batch        : {guest.Batch}";

        private string GenerateQRCodeBase64(string data)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(20);
            return "data:image/png;base64," + Convert.ToBase64String(qrBytes);
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingDetails(int id)
        {
            try
            {
                var guest = await _context.Guests
                    .Include(g => g.NationalityEntity)
                    .Include(g => g.Operators)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (guest == null)
                    return Content("<div class='text-center p-4'><i class='fas fa-exclamation-triangle fa-2x text-warning mb-3'></i><h5 class='text-warning'>Guest Not Found</h5></div>", "text/html");

                var guestsInBatch = await _context.Guests
                    .Where(g => g.Batch == guest.Batch && g.Id != guest.Id && g.BookingStatus != 1)
                    .Include(g => g.NationalityEntity)
                    .ToListAsync();

                var vm = new GuestDetailsViewModel { Guest = guest, GuestsInBatch = guestsInBatch };
                return PartialView("_ReserveBookingDetailsPartial", vm);
            }
            catch (Exception ex)
            {
                return Content($"<div class='text-center p-4'><h5 class='text-danger'>Error Loading Details</h5><p class='text-muted small'>{ex.Message}</p></div>", "text/html");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ReserveController.cs  — Booked Guests section
        // ─────────────────────────────────────────────────────────────────────────

        // GET: /Reserve/BookedGuest
        public IActionResult BookedGuest() => View();

        [HttpPost]
        [Authorize(Roles = "Super Admin")]
        public async Task<IActionResult> BookedGuest(string BatchCode)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                int? currentOperatorId = null;
                if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                    currentOperatorId = parsedId;

                if (string.IsNullOrEmpty(BatchCode))
                {
                    var lastBatch = await _context.Guests
                        .OrderByDescending(g => g.Id)
                        .Select(g => g.Batch)
                        .FirstOrDefaultAsync();

                    int newBatchNumber = int.TryParse(lastBatch, out int lastNum) ? lastNum + 1 : 10001;
                    return Json(new { success = false, message = $"No batch code provided. Auto-generated: {newBatchNumber}." });
                }

                var guestsToFinalize = await _context.Guests
                    .Where(g => g.Batch == BatchCode && g.BookingStatus == 2)
                    .ToListAsync();

                if (!guestsToFinalize.Any())
                    return Json(new { success = false, message = "No reserved guests found for this batch." });

                if (currentOperatorId.HasValue)
                {
                    var operatorGuests = guestsToFinalize.Where(g => g.OperatorId == currentOperatorId.Value).ToList();
                    if (!operatorGuests.Any())
                        return Json(new { success = false, message = "You don't have permission to confirm this batch." });
                }

                foreach (var guest in guestsToFinalize)
                    guest.BookingStatus = 0;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Successfully confirmed" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error confirming booking. Please try again." });
            }
        }



        // =========================================================
        // TIMEZONE
        // =========================================================
        private static readonly TimeZoneInfo PhilippineTime =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Manila");


        // =========================================================
        // DATE HELPERS
        // =========================================================

        private static DateOnly? ResolveGuestDate(string? arrivalDate, string? dateShort, string? date)
        {
            if (!string.IsNullOrWhiteSpace(arrivalDate) &&
                long.TryParse(arrivalDate.Trim(), out long unix))
            {
                try
                {
                    var utc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                    var local = TimeZoneInfo.ConvertTimeFromUtc(utc, PhilippineTime);
                    return DateOnly.FromDateTime(local);
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(dateShort) &&
                DateTime.TryParse(dateShort.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ds))
                return DateOnly.FromDateTime(ds);

            if (!string.IsNullOrWhiteSpace(date) &&
                DateTime.TryParse(date.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return DateOnly.FromDateTime(d);

            return null;
        }

        /// <summary>
        /// Parses the g.Date column.
        /// FIX: Added Unix timestamp handling — the original only tried DateTime.TryParse
        /// which always failed on a pure-digit Unix string, causing all guests to be
        /// excluded from the date filter and returning 0 rows.
        /// </summary>
        private static DateTime? ParseGuestDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();

            // 1. Pure Unix timestamp (all digits)
            if (long.TryParse(trimmed, out long unix))
            {
                try
                {
                    // Convert to PH local time (UTC+8)
                    return DateTimeOffset.FromUnixTimeSeconds(unix)
                                         .ToOffset(TimeSpan.FromHours(8))
                                         .DateTime;
                }
                catch { }
            }

            // 2. ISO / invariant culture string
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture,
                                  DateTimeStyles.AllowWhiteSpaces, out var dt))
                return dt;

            // 3. Current-culture fallback
            if (DateTime.TryParse(trimmed, out dt))
                return dt;

            return null;
        }

        private static string UnixNow()
            => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        private static string UnixTodayStart()
            => new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds().ToString();

        private static string UnixTodayEnd()
            => new DateTimeOffset(DateTime.Today.AddDays(1)).ToUnixTimeSeconds().ToString();

        private static string DtrDateNow()
            => DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss:ff");

        // ─────────────────────────────────────────────────────────────────────────
        // SAFE STATUS RESOLVER
        // BookingStatus may be an int column or an enum — this handles both.
        // ─────────────────────────────────────────────────────────────────────────
        private static string ResolveStatusLabel(object bookingStatus)
        {
            try
            {
                var intVal = Convert.ToInt32(bookingStatus);

                return intVal switch
                {
                    Guest.Status.Confirmed => "Confirmed",
                    Guest.Status.Canceled => "Canceled",
                    Guest.Status.Reserved => "Reserved",
                    Guest.Status.Anticipated => "Anticipated",
                    _ => intVal.ToString()
                };
            }
            catch
            {
                return bookingStatus?.ToString() ?? "Unknown";
            }
        }


        // ─────────────────────────────────────────────────────────────────────────
        // POST: /Reserve/GetGuestsData   (DataTables server-side)
        // ─────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetGuestsData(string? startDate, string? endDate)
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            // No BookingStatus filter — same scope as GetGuestOfTheDay
            var rawGuests = await _context.Guests
                .AsNoTracking()
                .Include(g => g.Operators)
                .Where(g => !string.IsNullOrEmpty(g.Date))
                .ToListAsync();

            // Parse dates in memory
            var filtered = rawGuests
                .Select(g => new { Guest = g, ParsedDate = ParseGuestDate(g.Date) })
                .Where(x => x.ParsedDate.HasValue);

            if (currentOperatorId.HasValue)
                filtered = filtered.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

            if (DateTime.TryParse(startDate, out var sd))
                filtered = filtered.Where(x => x.ParsedDate!.Value.Date >= sd.Date);

            if (DateTime.TryParse(endDate, out var ed))
                filtered = filtered.Where(x => x.ParsedDate!.Value.Date <= ed.Date);

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(x =>
                    (!string.IsNullOrEmpty(x.Guest.Fullname)
                        && x.Guest.Fullname.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(x.Guest.Batch)
                        && x.Guest.Batch.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (x.Guest.Operators?.BusinessName != null
                        && x.Guest.Operators.BusinessName.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Materialise before grouping (LINQ-to-Objects only)
            var materialised = filtered.ToList();

            // ── Pre-load outside guide names per batch from BatchAssignment ──
            var batchCodes = materialised
                .Select(x => x.Guest.Batch ?? "")
                .Distinct()
                .ToList();

            var outsideGuideMap = await _context.BatchAssignments
                .AsNoTracking()
                .Include(ba => ba.OutsideGuide)
                .Where(ba => batchCodes.Contains(ba.BatchCode) && ba.OutsideGuideId != null)
                .GroupBy(ba => ba.BatchCode)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => string.Join(", ", g
                        .Select(ba => ba.OutsideGuide != null
                            ? ba.OutsideGuide.FullName
                            : "")
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct())
                );

            var grouped = materialised
                .GroupBy(x => x.Guest.Batch ?? "")
                .Select(g =>
                {
                    var firstGuest = g.First().Guest;

                    var minDate = g.Min(x => x.ParsedDate);
                    var arrivalDateStr = minDate.HasValue
                        ? minDate.Value.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)
                        : "N/A";

                    var statusLabel = ResolveStatusLabel(firstGuest.BookingStatus);

                    // Pick the first non-null operator name in the batch
                    var opName = g
                        .Select(x => x.Guest.Operators?.BusinessName)
                        .FirstOrDefault(n => !string.IsNullOrEmpty(n))
                        ?? "No Operator";

                    // Lookup outside guide name(s) from BatchAssignment
                    var outsideGuideName = outsideGuideMap.TryGetValue(g.Key, out var name)
                        ? name
                        : "";

                    return new
                    {
                        batch = g.Key,
                        operatorId = g.Select(x => x.Guest.OperatorId).FirstOrDefault(),
                        operatorName = opName,
                        totalGuests = g.Count(),
                        outsideGuide = outsideGuideName,
                        arrivalDate = arrivalDateStr,
                        status = statusLabel
                    };
                })
                .OrderBy(x => x.batch)
                .ToList();

            var paged = grouped.Skip(start).Take(length).ToList();

            return Json(new
            {
                draw,
                recordsTotal = grouped.Count,
                recordsFiltered = grouped.Count,
                totalGuestsCount = materialised.Count,
                data = paged
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET: /Reserve/GetGuestOfTheDay
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetGuestOfTheDay(
            int pageNumber = 1,
            int pageSize = 50,
            string? batchFilter = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            var rawGuests = await _context.Guests
                .AsNoTracking()
                .Include(g => g.NationalityEntity)
                .Include(g => g.Operators)
                .Where(g => !string.IsNullOrEmpty(g.Date))
                .ToListAsync();

            var filteredGuests = rawGuests
                .Select(g => new { Guest = g, ParsedDate = ParseGuestDate(g.Date) })
                .Where(x => x.ParsedDate.HasValue && x.ParsedDate.Value.Date == DateTime.Today);

            if (currentOperatorId.HasValue)
                filteredGuests = filteredGuests.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

            if (!string.IsNullOrWhiteSpace(batchFilter))
                filteredGuests = filteredGuests.Where(x =>
                    !string.IsNullOrEmpty(x.Guest.Batch) &&
                    x.Guest.Batch.Contains(batchFilter, StringComparison.OrdinalIgnoreCase));

            var totalGuests = filteredGuests.Count();

            var pagedGuests = filteredGuests
                .OrderByDescending(x => x.ParsedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GuestWithOperatorVM
                {
                    Guest = x.Guest,
                    OperatorName = x.Guest.Operators?.BusinessName ?? "No Operator"
                })
                .ToList();

            if (!pagedGuests.Any())
                return Json(new { success = false, message = "No guest arrivals found today." });

            var model = new GuestPaginationViewModel
            {
                Guests = pagedGuests,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalGuests,
                TotalPages = (int)Math.Ceiling(totalGuests / (double)pageSize),
                BatchFilter = batchFilter
            };

            return PartialView("_GuestDetailsPartial", model);
        }


        // ─────────────────────────────────────────────────────────────────────────
        // POST: /Reserve/GetBatchDetails
        // ─────────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetBatchDetails(string Batch)
        {
            try
            {
                if (string.IsNullOrEmpty(Batch))
                    return Json(new { success = false, message = "Batch code is required" });

                var batchNumbers = Batch.StartsWith("BATCH-", StringComparison.OrdinalIgnoreCase)
                    ? Batch[6..]
                    : Batch;

                var batchDetails = await _context.Guests
                    .Where(g => g.Batch == batchNumbers && g.BookingStatus == 2)
                    .GroupBy(g => g.Batch)
                    .Select(g => new
                    {
                        operatorName = g.First().Operators.BusinessName ?? "No Operator",
                        totalGuests = g.Count()
                    })
                    .FirstOrDefaultAsync();

                if (batchDetails != null)
                    return Json(new { success = true, data = new { batchDetails.operatorName, batchDetails.totalGuests } });

                return Json(new { success = false, message = "Batch not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }


        // =========================================================
        // GET ASSIGNED BATCHES
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignedBatches()
        {
            var todayDriverIds = await _context.DriverAttendances
                .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                         && string.Compare(a.Date, UnixTodayEnd()) < 0)
                .Select(a => a.DriverId)
                .Distinct()
                .ToListAsync();

            var driverPkIds = await _context.Drivers
                .Where(d => todayDriverIds.Contains(d.RefId))
                .Select(d => (int?)d.DriverId)
                .ToListAsync();

            var assignedBatches = await _context.BatchAssignments
                .Where(b => !string.IsNullOrEmpty(b.BatchCode)
                         && b.DriverId != null
                         && driverPkIds.Contains(b.DriverId))
                .Select(b => b.BatchCode)
                .Distinct()
                .ToListAsync();

            return Json(new { assignedBatches });
        }


        // ─────────────────────────────────────────────────────────────────────────
        // GET: /Reserve/GetGuestsByBatch
        // ✅ Absent drivers / guides / outside guides are excluded from the display
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode, int? operatorId, string? startDate, string? endDate)
        {
            const string guestDetailsViewName = "ViewGuestDetails";

            if (string.IsNullOrWhiteSpace(batchCode))
                return BadRequest("Batch code is required.");

            // ── 1. Load guests ──────────────────────────────────────────────────
            var guests = await _context.Guests
                .AsNoTracking()
                .Include(g => g.NationalityEntity)
                .Include(g => g.Operators)
                .Where(g => g.Batch == batchCode)
                .OrderBy(g => g.Id)
                .ToListAsync();

            if (operatorId.HasValue)
                guests = guests.Where(g => g.OperatorId == operatorId.Value).ToList();

            var hasStartDate = DateTime.TryParse(startDate, out var parsedStartDate);
            var hasEndDate = DateTime.TryParse(endDate, out var parsedEndDate);

            if (hasStartDate || hasEndDate)
            {
                guests = guests
                    .Where(g =>
                    {
                        var guestDate = ParseGuestDate(g.Date);
                        if (!guestDate.HasValue) return false;
                        if (hasStartDate && guestDate.Value.Date < parsedStartDate.Date) return false;
                        if (hasEndDate && guestDate.Value.Date > parsedEndDate.Date) return false;
                        return true;
                    })
                    .OrderBy(g => g.Id)
                    .ToList();
            }

            if (!guests.Any())
                return PartialView(guestDetailsViewName, new List<GuestWithOperatorVM>());

            var operatorName = guests.First().Operators?.BusinessName ?? "No Operator";
            var model = guests.Select(g => new GuestWithOperatorVM
            {
                Guest = g,
                OperatorName = g.Operators?.BusinessName ?? operatorName
            }).ToList();

            // ── Absent detection — load today's DTR records in memory ───────────
            long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

            // ✅ Absent DRIVERS: DriverDtr today where SUM(Passenger) == 0
            var driverDtrToday = await _context.DriverDtrs
                .Where(d => !string.IsNullOrEmpty(d.Date)
                         && string.Compare(d.Date, UnixTodayStart()) >= 0
                         && string.Compare(d.Date, UnixTodayEnd()) < 0)
                .Select(d => new { d.Rfid, d.Passenger })
                .ToListAsync();

            var absentDriverRfids = driverDtrToday
                .GroupBy(d => d.Rfid)
                .Where(g => g.Sum(x => int.TryParse(x.Passenger, out int p) ? p : 0) == 0)
                .Select(g => g.Key)
                .ToHashSet();                                    // HashSet<int>

            // ✅ Absent GUIDES: TourGuideDtr today where SUM(NoOfGuest) == 0
            var guideDtrToday = await _context.TourGuideDtrs
                .Where(d => d.Date == todayLong)
                .Select(d => new { d.Rfid, d.NoOfGuest })
                .ToListAsync();

            var absentGuideRfids = guideDtrToday
                .GroupBy(d => d.Rfid)
                .Where(g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0) == 0)
                .Select(g => g.Key)
                .ToHashSet();                                    // HashSet<long>

            // ✅ Absent OUTSIDE GUIDES: TourGuidePriority today where SUM(NoOfGuest) == 0
            var outsidePriorityToday = await _context.TourGuidePriorities
                .Where(p => string.Compare(p.Date, UnixTodayStart()) >= 0
                         && string.Compare(p.Date, UnixTodayEnd()) < 0)
                .Select(p => new { p.GuideIdPrior, p.NoOfGuest })
                .ToListAsync();

            var absentOutsideGuideRfids = outsidePriorityToday
                .GroupBy(p => p.GuideIdPrior)
                .Where(g => g.Sum(x => x.NoOfGuest) == 0)
                .Select(g => g.Key)
                .ToHashSet();                                    // HashSet<int>

            // ── 2. Assigned guides — exclude absent ─────────────────────────────
            var guideIds = await _context.BatchAssignments
                .AsNoTracking()
                .Where(b => b.BatchCode == batchCode && b.GuideId != null)
                .Select(b => b.GuideId!.Value)
                .Distinct()
                .ToListAsync();

            var assignedGuides = new List<object>();
            if (guideIds.Any())
            {
                var guideList = await _context.Guides
                    .AsNoTracking()
                    .Where(g => guideIds.Contains(g.GuideId))
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim(),
                        Image = g.Image ?? ""
                    })
                    .ToListAsync();

                // ✅ Skip absent guides (Rfid parsed to long matched against absentGuideRfids)
                assignedGuides = guideList
                    .Where(g => !(long.TryParse(g.Rfid, out long rfidL)
                                  && absentGuideRfids.Contains(rfidL)))
                    .Cast<object>()
                    .ToList();
            }

            // ── 3. Assigned drivers — exclude absent ────────────────────────────
            var driverIds = await _context.BatchAssignments
                .AsNoTracking()
                .Where(b => b.BatchCode == batchCode && b.DriverId != null)
                .Select(b => b.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            var assignedDrivers = new List<object>();
            if (driverIds.Any())
            {
                var driverList = await _context.Drivers
                    .AsNoTracking()
                    .Where(d => driverIds.Contains(d.DriverId))
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        fullName = ((d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? "")).Trim(),
                        Image = d.Image ?? ""
                    })
                    .ToListAsync();

                // ✅ Skip absent drivers (RefId parsed to int matched against absentDriverRfids)
                assignedDrivers = driverList
                    .Where(d => !(int.TryParse(d.RefId, out int refInt)
                                  && absentDriverRfids.Contains(refInt)))
                    .Cast<object>()
                    .ToList();
            }

            // ── 4. Assigned outside guides — exclude absent ─────────────────────
            var outsideGuideIds = await _context.BatchAssignments
                .AsNoTracking()
                .Where(b => b.BatchCode == batchCode && b.OutsideGuideId != null)
                .Select(b => b.OutsideGuideId!.Value)
                .Distinct()
                .ToListAsync();

            var assignedOutsideGuides = new List<object>();
            if (outsideGuideIds.Any())
            {
                var outsideGuideList = await _context.OutsideGuides
                    .AsNoTracking()
                    .Where(g => outsideGuideIds.Contains(g.OutsideGuideId))
                    .Select(g => new
                    {
                        g.OutsideGuideId,
                        Rfid = g.Rfid ?? "",
                        fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim(),
                        Image = g.Image ?? ""
                    })
                    .ToListAsync();

                // ✅ Skip absent outside guides (Rfid parsed to int matched against absentOutsideGuideRfids)
                assignedOutsideGuides = outsideGuideList
                    .Where(g => !(int.TryParse(g.Rfid, out int rfidInt)
                                  && absentOutsideGuideRfids.Contains(rfidInt)))
                    .Cast<object>()
                    .ToList();
            }

            ViewBag.AssignedGuides = assignedGuides;
            ViewBag.AssignedDrivers = assignedDrivers;
            ViewBag.AssignedOutsideGuides = assignedOutsideGuides;

            return PartialView(guestDetailsViewName, model);
        }

        // =========================================================
        // SHARED HELPER — guest count filtered by date range
        // Used by assign modals to match the table's filtered count.
        // Falls back to full batch count when no dates are supplied.
        // =========================================================
        private async Task<int> GetFilteredGuestCount(string batch, string? startDate, string? endDate)
        {
            bool hasStart = DateTime.TryParse(startDate, out var sd);
            bool hasEnd = DateTime.TryParse(endDate, out var ed);

            if (!hasStart && !hasEnd)
                return await _context.Guests.CountAsync(g => g.Batch == batch);

            var dates = await _context.Guests
                .Where(g => g.Batch == batch && !string.IsNullOrEmpty(g.Date))
                .Select(g => g.Date)
                .ToListAsync();

            var count = dates
                .Select(ParseGuestDate)
                .Where(d => d.HasValue
                    && (!hasStart || d!.Value.Date >= sd.Date)
                    && (!hasEnd || d!.Value.Date <= ed.Date))
                .Count();

            // Fallback: if date filter yields 0, return the full batch count
            return count > 0
                ? count
                : await _context.Guests.CountAsync(g => g.Batch == batch);
        }

        // =========================================================
        // ACTIVE ASSIGNED STAFF COUNT HELPER
        // =========================================================
        private async Task<(int ActiveDrivers, int ActiveGuides)> GetActiveAssignedStaffCount(string batch)
        {
            if (string.IsNullOrWhiteSpace(batch))
                return (0, 0);

            long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

            var driverDtrToday = await _context.DriverDtrs
                .AsNoTracking()
                .Where(d => !string.IsNullOrEmpty(d.Date)
                         && string.Compare(d.Date, UnixTodayStart()) >= 0
                         && string.Compare(d.Date, UnixTodayEnd()) < 0)
                .Select(d => new { d.Rfid, d.Passenger })
                .ToListAsync();

            var absentDriverRfids = driverDtrToday
                .GroupBy(d => d.Rfid)
                .Where(g => g.Sum(x => int.TryParse(x.Passenger, out var p) ? p : 0) == 0)
                .Select(g => g.Key)
                .ToHashSet();

            var guideDtrToday = await _context.TourGuideDtrs
                .AsNoTracking()
                .Where(d => d.Date == todayLong)
                .Select(d => new { d.Rfid, d.NoOfGuest })
                .ToListAsync();

            var absentGuideRfids = guideDtrToday
                .GroupBy(d => d.Rfid)
                .Where(g => g.Sum(x => int.TryParse(x.NoOfGuest, out var p) ? p : 0) == 0)
                .Select(g => g.Key)
                .ToHashSet();

            var outsidePriorityToday = await _context.TourGuidePriorities
                .AsNoTracking()
                .Where(p => string.Compare(p.Date, UnixTodayStart()) >= 0
                         && string.Compare(p.Date, UnixTodayEnd()) < 0)
                .Select(p => new { p.GuideIdPrior, p.NoOfGuest })
                .ToListAsync();

            var absentOutsideGuideRfids = outsidePriorityToday
                .GroupBy(p => p.GuideIdPrior)
                .Where(g => g.Sum(x => x.NoOfGuest) == 0)
                .Select(g => g.Key)
                .ToHashSet();

            var assignments = await _context.BatchAssignments
                .AsNoTracking()
                .Where(b => b.BatchCode == batch)
                .ToListAsync();

            int activeDrivers = 0;
            var driverIds = assignments
                .Where(b => b.DriverId != null)
                .Select(b => b.DriverId!.Value)
                .Distinct()
                .ToList();

            if (driverIds.Any())
            {
                var driverRefs = await _context.Drivers
                    .AsNoTracking()
                    .Where(d => driverIds.Contains(d.DriverId))
                    .Select(d => d.RefId)
                    .ToListAsync();

                activeDrivers = driverRefs.Count(r =>
                    !string.IsNullOrWhiteSpace(r) &&
                    !(int.TryParse(r, out var rfidInt) && absentDriverRfids.Contains(rfidInt)));
            }

            int activeGuides = 0;

            var guideIds = assignments
                .Where(b => b.GuideId != null)
                .Select(b => b.GuideId!.Value)
                .Distinct()
                .ToList();

            if (guideIds.Any())
            {
                var guideRfids = await _context.Guides
                    .AsNoTracking()
                    .Where(g => guideIds.Contains(g.GuideId))
                    .Select(g => g.Rfid)
                    .ToListAsync();

                activeGuides += guideRfids.Count(r =>
                    !string.IsNullOrWhiteSpace(r) &&
                    !(long.TryParse(r, out var rfidLong) && absentGuideRfids.Contains(rfidLong)));
            }

            var outsideGuideIds = assignments
                .Where(b => b.OutsideGuideId != null)
                .Select(b => b.OutsideGuideId!.Value)
                .Distinct()
                .ToList();

            if (outsideGuideIds.Any())
            {
                var outsideRfids = await _context.OutsideGuides
                    .AsNoTracking()
                    .Where(g => outsideGuideIds.Contains(g.OutsideGuideId))
                    .Select(g => g.Rfid)
                    .ToListAsync();

                activeGuides += outsideRfids.Count(r =>
                    !string.IsNullOrWhiteSpace(r) &&
                    !(int.TryParse(r, out var rfidInt) && absentOutsideGuideRfids.Contains(rfidInt)));
            }

            return (activeDrivers, activeGuides);
        }


        // =========================================================
        // GET ASSIGN DRIVER MODAL
        // ✅ FIFO ordered by Driver.DPosition
        // ✅ hasTrip / isAbsent / passengers from DriverAttendance + DriverDtr today
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignDriverModal(string batch, string? startDate = null, string? endDate = null)
        {
            try
            {
                var allDrivers = await _context.Drivers
                    .OrderBy(d => d.DPosition)
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        fullName = ((d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? "")).Trim(),
                        d.Image,
                        d.DPosition
                    })
                    .ToListAsync();

                var attendanceToday = await _context.DriverAttendances
                    .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var lastAssignmentMap = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.DriverId))
                    .GroupBy(a => a.DriverId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                var driverDtrToday = await _context.DriverDtrs
                    .Where(d => !string.IsNullOrEmpty(d.Date)
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var driverPassengerMap = driverDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.Passenger, out var p) ? p : 0));

                var driversWithDtrRfidSet = driverDtrToday.Select(d => d.Rfid).ToHashSet();
                var absentDriverRefIds = new HashSet<string>();

                foreach (var d in allDrivers.Where(x => lastAssignmentMap.ContainsKey(x.RefId)))
                {
                    if (int.TryParse(d.RefId, out var refInt)
                        && driversWithDtrRfidSet.Contains(refInt)
                        && driverPassengerMap.ContainsKey(refInt)
                        && driverPassengerMap[refInt] == 0)
                    {
                        absentDriverRefIds.Add(d.RefId);
                    }
                }

                var orderedDrivers = allDrivers
                    .OrderBy(d => lastAssignmentMap.ContainsKey(d.RefId) ? 1 : 0)
                    .ThenBy(d => d.DPosition)
                    .ToList();

                var availableDriversRaw = new List<object>();
                for (int i = 0; i < orderedDrivers.Count; i++)
                {
                    var d = orderedDrivers[i];
                    int.TryParse(d.RefId, out var refInt);

                    availableDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        hasTrip = lastAssignmentMap.ContainsKey(d.RefId),
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePosition = i + 1,
                        passengers = refInt > 0 && driverPassengerMap.ContainsKey(refInt)
                            ? driverPassengerMap[refInt]
                            : 0,
                        dPosition = d.DPosition
                    });
                }

                var assignedTodayList = orderedDrivers
                    .Where(d => lastAssignmentMap.ContainsKey(d.RefId))
                    .ToList();

                var busyDriversRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var d = assignedTodayList[i];
                    int.TryParse(d.RefId, out var refInt2);

                    busyDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePos = i + 1,
                        passengers = refInt2 > 0 && driverPassengerMap.ContainsKey(refInt2)
                            ? driverPassengerMap[refInt2]
                            : 0,
                        dPosition = d.DPosition
                    });
                }

                var guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int totalRequiredDrivers = GetRequiredStaffCount(guestCount);
                var activeCounts = await GetActiveAssignedStaffCount(batch);
                int recommendedDrivers = Math.Max(0, totalRequiredDrivers - activeCounts.ActiveDrivers);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedDrivers = recommendedDrivers;
                ViewBag.AvailableDrivers = availableDriversRaw.Cast<dynamic>().ToList();
                ViewBag.BusyDrivers = busyDriversRaw.Cast<dynamic>().ToList();
                ViewBag.StartDate = startDate ?? "";
                ViewBag.EndDate = endDate ?? "";

                return PartialView("_AssignDriverModal");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Content($@"
<div class='modal-header bg-danger text-white'>
    <h5 class='modal-title'>Error Loading Drivers</h5>
    <button type='button' class='btn-close btn-close-white' data-bs-dismiss='modal'></button>
</div>
<div class='modal-body'>
    <div class='alert alert-danger'><strong>Error:</strong> {inner}</div>
</div>
<div class='modal-footer'>
    <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
</div>", "text/html");
            }
        }

        // =========================================================
        // ASSIGN DRIVERS — POST
        // ✅ Inserts DriverAttendance + DriverDtr + BatchAssignment
        // ✅ After assignment, DPosition updated → moves to bottom of FIFO
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDrivers(string batch, List<string> driverRefIds, string? startDate = null, string? endDate = null)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || driverRefIds == null || !driverRefIds.Any())
                    return Json(new { success = false, message = "Batch and at least one driver are required." });

                var guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int totalRequiredDrivers = GetRequiredStaffCount(guestCount);
                var activeCounts = await GetActiveAssignedStaffCount(batch);
                int remainingDriverSlots = Math.Max(0, totalRequiredDrivers - activeCounts.ActiveDrivers);

                if (remainingDriverSlots <= 0)
                    return Json(new { success = false, message = "Driver limit already reached for this batch." });

                if (driverRefIds.Count > remainingDriverSlots)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {remainingDriverSlots} driver(s). Limit already reached by current active assignments."
                    });

                int driversCount = driverRefIds.Count;
                int basePassengers = driversCount > 0 ? guestCount / driversCount : 0;
                int remainder = driversCount > 0 ? guestCount % driversCount : 0;

                var operatorId = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .Select(g => (int?)g.OperatorId)
                    .FirstOrDefaultAsync();

                var assignedNames = new List<string>();

                for (int i = 0; i < driversCount; i++)
                {
                    var refId = driverRefIds[i].Trim();
                    var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == refId);
                    if (driver == null)
                        continue;

                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);
                    int.TryParse(driver.RefId, out var rfidInt);

                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = driver.RefId,
                        Date = UnixNow(),
                        Passenger = assignedPassengers
                    });

                    _context.DriverDtrs.Add(new DriverDtr
                    {
                        Rfid = rfidInt > 0 ? rfidInt : driver.DriverId,
                        Date = UnixNow(),
                        Passenger = assignedPassengers.ToString(),
                        ComDateDr = DtrDateNow()
                    });

                    _context.BatchAssignments.Add(new BatchAssignment
                    {
                        BatchCode = batch,
                        OperatorId = operatorId,
                        GuideId = null,
                        DriverId = driver.DriverId
                    });

                    driver.DPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    assignedNames.Add($"{driver.FName} {driver.LName}");
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{driversCount} driver(s) assigned: {string.Join(", ", assignedNames)}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // EDIT DRIVER ASSIGNMENT — update DriverDtr.Passenger
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDriverAssignment(string driverRefId, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (passengers < 1)
                    return Json(new { success = false, message = "Passenger count must be at least 1." });

                if (!int.TryParse(driverRefId, out int rfidInt))
                    return Json(new { success = false, message = "Invalid driver Ref ID (must be numeric)." });

                var dtr = await _context.DriverDtrs
                    .Where(d => d.Rfid == rfidInt
                             && !string.IsNullOrEmpty(d.Date)
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (dtr == null)
                    return Json(new { success = false, message = "No active DTR record found for this driver today." });

                dtr.Passenger = passengers.ToString();
                dtr.ComDateDr = DtrDateNow();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Passenger count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // REMOVE DRIVER ASSIGNMENT
        // ✅ Removes DriverAttendance + DriverDtr + BatchAssignment
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                // Remove DriverAttendance
                var attendances = await _context.DriverAttendances
                    .Where(a => a.DriverId == driverRefId
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any())
                    _context.DriverAttendances.RemoveRange(attendances);

                // Remove DriverDtr
                if (int.TryParse(driverRefId, out int rfidInt))
                {
                    var dtrs = await _context.DriverDtrs
                        .Where(d => d.Rfid == rfidInt
                                 && !string.IsNullOrEmpty(d.Date)
                                 && string.Compare(d.Date, UnixTodayStart()) >= 0
                                 && string.Compare(d.Date, UnixTodayEnd()) < 0)
                        .ToListAsync();

                    if (dtrs.Any())
                        _context.DriverDtrs.RemoveRange(dtrs);
                }

                // Remove BatchAssignment
                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                if (driver != null)
                {
                    var batchRecords = await _context.BatchAssignments
                        .Where(b => b.DriverId == driver.DriverId)
                        .ToListAsync();

                    if (batchRecords.Any())
                        _context.BatchAssignments.RemoveRange(batchRecords);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // MARK DRIVER ABSENT — Passenger = 0, stays in FIFO queue
        // ✅ Ensures attendance exists + replaces DTR with Passenger = "0"
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDriverAbsent(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                // Ensure attendance exists — driver stays in FIFO rotation
                var attendance = await _context.DriverAttendances
                    .Where(a => a.DriverId == driverRefId
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .FirstOrDefaultAsync();

                if (attendance == null)
                {
                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = driverRefId,
                        Date = UnixNow(),
                        Passenger = 0
                    });
                }

                // Replace DriverDtr with Passenger = "0"
                if (int.TryParse(driverRefId, out int rfidInt))
                {
                    var existingDtrs = await _context.DriverDtrs
                        .Where(d => d.Rfid == rfidInt
                                 && !string.IsNullOrEmpty(d.Date)
                                 && string.Compare(d.Date, UnixTodayStart()) >= 0
                                 && string.Compare(d.Date, UnixTodayEnd()) < 0)
                        .ToListAsync();

                    if (existingDtrs.Any())
                        _context.DriverDtrs.RemoveRange(existingDtrs);

                    _context.DriverDtrs.Add(new DriverDtr
                    {
                        Rfid = rfidInt,
                        Date = UnixNow(),
                        Passenger = "0",
                        ComDateDr = DtrDateNow()
                    });
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver marked as absent. Still in queue rotation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // GET ASSIGN GUIDE MODAL
        // ✅ FIFO ordered by Guide.TPosition (Unix timestamp)
        // ✅ TPosition passed to ViewBag for display in modal
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignGuideModal(string batch, string? startDate = null, string? endDate = null)
        {
            try
            {
                var allGuides = await _context.Guides
                    .OrderBy(g => g.TPosition)                          // ✅ FIFO by TPosition
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim(),
                        g.Image,
                        g.TPosition                                     // ✅ included
                    })
                    .ToListAsync();

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                // ✅ Today's attendance — unix timestamp range
                var attendanceToday = await _context.TourGuideAttendances
                    .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                // FIFO: last attendance Id per guide (TGId = Rfid)
                var lastAssignmentMap = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.TGId))
                    .GroupBy(a => a.TGId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                // Passenger totals from TourGuideDtr (Date = yyyyMMdd long)
                var guideDtrToday = await _context.TourGuideDtrs
                    .Where(d => d.Date == todayLong)
                    .ToListAsync();

                var guidePassengerMap = guideDtrToday
                      .GroupBy(d => d.Rfid)
                      .ToDictionary(
                          g => g.Key,
                          g => g.Sum(x => int.TryParse(x.NoOfGuest, out var pax) ? pax : 0));

                // ✅ Absent = has attendance today + DTR today + NoOfGuest total == 0
                var guidesWithDtrRfidSet = guideDtrToday.Select(d => d.Rfid).ToHashSet();
                var absentGuideRfids = new HashSet<string>();

                foreach (var g in allGuides.Where(x => lastAssignmentMap.ContainsKey(x.Rfid)))
                {
                    if (long.TryParse(g.Rfid, out long rfidL)
                        && guidesWithDtrRfidSet.Contains(rfidL)
                        && guidePassengerMap.ContainsKey(rfidL)
                        && guidePassengerMap[rfidL] == 0)
                    {
                        absentGuideRfids.Add(g.Rfid);
                    }
                }

                // ✅ FIFO order: never-assigned first → then by TPosition
                var orderedGuides = allGuides
                    .OrderBy(g => lastAssignmentMap.ContainsKey(g.Rfid) ? 1 : 0)
                    .ThenBy(g => g.TPosition)                           // ✅ TPosition as tiebreaker
                    .ToList();

                var availableGuidesRaw = new List<object>();
                for (int i = 0; i < orderedGuides.Count; i++)
                {
                    var g = orderedGuides[i];
                    long.TryParse(g.Rfid, out long rfidL);
                    availableGuidesRaw.Add(new
                    {
                        g.GuideId,
                        g.Rfid,
                        g.fullName,
                        Image = g.Image ?? "",
                        hasTrip = lastAssignmentMap.ContainsKey(g.Rfid),
                        isAbsent = absentGuideRfids.Contains(g.Rfid),
                        queuePosition = i + 1,
                        passengers = rfidL > 0 && guidePassengerMap.ContainsKey(rfidL)
                                            ? guidePassengerMap[rfidL] : 0,
                        tPosition = g.TPosition                     // ✅ pass to view
                    });
                }

                var assignedTodayList = orderedGuides
                    .Where(g => lastAssignmentMap.ContainsKey(g.Rfid))
                    .ToList();

                var busyGuidesRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var g = assignedTodayList[i];
                    long.TryParse(g.Rfid, out long rfidL2);
                    busyGuidesRaw.Add(new
                    {
                        g.GuideId,
                        g.Rfid,
                        g.fullName,
                        Image = g.Image ?? "",
                        isAbsent = absentGuideRfids.Contains(g.Rfid),
                        queuePos = i + 1,
                        passengers = rfidL2 > 0 && guidePassengerMap.ContainsKey(rfidL2)
                                     ? guidePassengerMap[rfidL2] : 0,
                        tPosition = g.TPosition                        // ✅ pass to view
                    });
                }

                var guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedGuides = recommendedGuides;
                ViewBag.AvailableGuides = availableGuidesRaw.Cast<dynamic>().ToList();
                ViewBag.BusyGuides = busyGuidesRaw.Cast<dynamic>().ToList();
                ViewBag.StartDate = startDate ?? "";
                ViewBag.EndDate = endDate ?? "";

                return PartialView("_AssignGuideModal");


            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Content($@"
<div class='modal-header bg-danger text-white'>
    <h5 class='modal-title'>Error Loading Guides</h5>
    <button type='button' class='btn-close btn-close-white' data-bs-dismiss='modal'></button>
</div>
<div class='modal-body'>
    <div class='alert alert-danger'><strong>Error:</strong> {inner}</div>
</div>
<div class='modal-footer'>
    <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
</div>", "text/html");
            }
        }


        // =========================================================
        // ASSIGN GUIDES — POST
        // ✅ Inserts TourGuideAttendance + TourGuideDtr + BatchAssignment
        // ✅ TourGuideAttendance: TGId = Rfid, Date = unix
        // ✅ TourGuideDtr: Rfid = long, Date = yyyyMMdd long
        // ✅ After assignment, guide TPosition updated → moves to bottom of FIFO queue
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignGuides(string batch, List<string> guideRfids, string? startDate = null, string? endDate = null)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one Guide are required." });

                var guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int totalRequiredGuides = GetRequiredStaffCount(guestCount);
                var activeCounts = await GetActiveAssignedStaffCount(batch);
                int remainingGuideSlots = Math.Max(0, totalRequiredGuides - activeCounts.ActiveGuides);

                if (remainingGuideSlots <= 0)
                    return Json(new { success = false, message = "Guide limit already reached for this batch." });

                if (guideRfids.Count > remainingGuideSlots)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {remainingGuideSlots} guide(s). Limit already reached by current active assignments."
                    });

                int guidesCount = guideRfids.Count;
                int basePassengers = guidesCount > 0 ? guestCount / guidesCount : 0;
                int remainder = guidesCount > 0 ? guestCount % guidesCount : 0;

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                var operatorId = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .Select(g => (int?)g.OperatorId)
                    .FirstOrDefaultAsync();

                var assignedNames = new List<string>();

                for (int i = 0; i < guidesCount; i++)
                {
                    var guideRfid = guideRfids[i].Trim();
                    var guide = await _context.Guides.FirstOrDefaultAsync(g => g.Rfid == guideRfid);
                    if (guide == null)
                        continue;

                    long rfidLong = long.TryParse(guide.Rfid, out var parsed) ? parsed : guide.GuideId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    _context.TourGuideAttendances.Add(new TourGuideAttendance
                    {
                        TGId = guide.Rfid,
                        Date = UnixNow(),
                        Rfid = guide.Rfid
                    });

                    _context.TourGuideDtrs.Add(new TourGuideDtr
                    {
                        Rfid = rfidLong,
                        Date = todayLong,
                        NoOfGuest = assignedPassengers.ToString(),
                        ComDate = DtrDateNow()
                    });

                    _context.BatchAssignments.Add(new BatchAssignment
                    {
                        BatchCode = batch,
                        OperatorId = operatorId,
                        GuideId = guide.GuideId,
                        DriverId = null
                    });

                    guide.TPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    assignedNames.Add($"{guide.FName} {guide.LName}");
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{guidesCount} guide(s) assigned: {string.Join(", ", assignedNames)}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // EDIT GUIDE ASSIGNMENT — update TourGuideDtr.NoOfGuest
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGuideAssignment(string guideRfid, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                if (passengers < 0)
                    return Json(new { success = false, message = "Passenger count cannot be negative." });

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                if (!long.TryParse(guideRfid, out long rfidLong))
                    return Json(new { success = false, message = "Invalid guide RFID." });

                var dtr = await _context.TourGuideDtrs
                    .Where(d => d.Rfid == rfidLong && d.Date == todayLong)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (dtr == null)
                    return Json(new { success = false, message = "No active assignment found for this guide today." });

                dtr.NoOfGuest = passengers.ToString();
                dtr.ComDate = DtrDateNow();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Passenger count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // REMOVE GUIDE ASSIGNMENT
        // ✅ Removes TourGuideAttendance + TourGuideDtr + BatchAssignment
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveGuideAssignment(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                // Remove TourGuideAttendance
                var attendances = await _context.TourGuideAttendances
                    .Where(a => a.TGId == guideRfid
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any())
                    _context.TourGuideAttendances.RemoveRange(attendances);

                // Remove TourGuideDtr
                if (long.TryParse(guideRfid, out long rfidLong))
                {
                    var dtrs = await _context.TourGuideDtrs
                        .Where(d => d.Rfid == rfidLong && d.Date == todayLong)
                        .ToListAsync();

                    if (dtrs.Any())
                        _context.TourGuideDtrs.RemoveRange(dtrs);
                }

                // Remove BatchAssignment
                var guide = await _context.Guides.FirstOrDefaultAsync(g => g.Rfid == guideRfid);
                if (guide != null)
                {
                    var batchRecords = await _context.BatchAssignments
                        .Where(b => b.GuideId == guide.GuideId)
                        .ToListAsync();

                    if (batchRecords.Any())
                        _context.BatchAssignments.RemoveRange(batchRecords);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Guide assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // MARK GUIDE ABSENT — NoOfGuest = 0, stays in FIFO queue
        // ✅ Ensures attendance exists + replaces DTR with NoOfGuest = "0"
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkGuideAbsent(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                if (!long.TryParse(guideRfid, out long rfidLong))
                    return Json(new { success = false, message = "Invalid guide RFID." });

                // ✅ Ensure attendance exists — guide stays in FIFO rotation
                var attendance = await _context.TourGuideAttendances
                    .Where(a => a.TGId == guideRfid
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .FirstOrDefaultAsync();

                if (attendance == null)
                {
                    _context.TourGuideAttendances.Add(new TourGuideAttendance
                    {
                        TGId = guideRfid,
                        Date = UnixNow(),
                        Rfid = guideRfid
                    });
                }

                // ✅ Replace TourGuideDtr with NoOfGuest = "0"
                var existingDtrs = await _context.TourGuideDtrs
                    .Where(d => d.Rfid == rfidLong && d.Date == todayLong)
                    .ToListAsync();

                if (existingDtrs.Any())
                    _context.TourGuideDtrs.RemoveRange(existingDtrs);

                _context.TourGuideDtrs.Add(new TourGuideDtr
                {
                    Rfid = rfidLong,
                    Date = todayLong,
                    NoOfGuest = "0",
                    ComDate = DtrDateNow()
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Guide marked as absent. Still in queue rotation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // CLEAR GUIDE — remove TourGuideAttendance only
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearGuideAssignment(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                var toRemove = await _context.TourGuideAttendances
                    .Where(a => a.TGId == guideRfid
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (toRemove.Any())
                    _context.TourGuideAttendances.RemoveRange(toRemove);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Guide is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // GET ASSIGN OUTSIDE GUIDE MODAL
        // ✅ FIFO from tourguide_priority (MAX Date per guide = last assigned)
        // ✅ hasTrip / isAbsent / passengers from tourguide_priority today
        // ✅ Pulls guides from outside_tourguide_details by OperatorId
        // ✅ Fallback match by OperatorName if OperatorId yields nothing
        // ✅ FIXED: Final fallback to ALL guides if operator yields nothing
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignOutsideGuideModal(string batch, string? startDate = null, string? endDate = null)
        {
            try
            {
                if (string.IsNullOrEmpty(batch))
                    return BadRequest("Batch is required.");

                var batchMeta = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .GroupBy(g => new { g.Batch, g.OperatorId })
                    .Select(g => new { GuestCount = g.Count(), OperatorId = g.Key.OperatorId })
                    .FirstOrDefaultAsync();

                int guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int totalRequiredGuides = GetRequiredStaffCount(guestCount);
                var activeCounts = await GetActiveAssignedStaffCount(batch);
                int recommendedGuides = Math.Max(0, totalRequiredGuides - activeCounts.ActiveGuides);

                Operator? op = batchMeta?.OperatorId != null
                    ? await _context.Operators.FindAsync(batchMeta.OperatorId)
                    : null;

                string operatorName = op?.Name ?? string.Empty;
                string operatorId = op?.Id.ToString() ?? string.Empty;

                List<OutsideGuide> guides = new();

                if (op != null)
                {
                    guides = await _context.OutsideGuides
                        .Where(g => g.OperatorId == op.Id)
                        .ToListAsync();

                    if (!guides.Any())
                    {
                        guides = await _context.OutsideGuides
                            .Include(g => g.Operator)
                            .Where(g => g.Operator != null && g.Operator.Name == op.Name)
                            .ToListAsync();
                    }
                }

                if (!guides.Any())
                {
                    guides = await _context.OutsideGuides
                        .Include(g => g.Operator)
                        .ToListAsync();
                }

                var rfidMap = guides
                    .Where(g => !string.IsNullOrWhiteSpace(g.Rfid)
                             && long.TryParse(g.Rfid.Trim(), out var v)
                             && v > 0
                             && v <= int.MaxValue)
                    .GroupBy(g => (int)long.Parse(g.Rfid.Trim()))
                    .ToDictionary(grp => grp.Key, grp => grp.First());

                var rfidInts = rfidMap.Keys.ToList();

                if (!rfidInts.Any())
                {
                    ViewBag.Batch = batch;
                    ViewBag.GuestCount = guestCount;
                    ViewBag.RecommendedGuides = recommendedGuides;
                    ViewBag.OperatorName = operatorName;
                    ViewBag.OperatorId = operatorId;
                    ViewBag.AvailableGuides = new List<object>();
                    ViewBag.BusyGuides = new List<object>();
                    ViewBag.StartDate = startDate ?? "";
                    ViewBag.EndDate = endDate ?? "";
                    return PartialView("_AssignOutsideGuideModal");
                }

                var todayRows = await _context.TourGuidePriorities
                    .Where(p => rfidInts.Contains(p.GuideIdPrior)
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var todayState = todayRows
                    .GroupBy(p => p.GuideIdPrior)
                    .ToDictionary(g => g.Key, g => new
                    {
                        HasTrip = g.Any(x => x.NoOfGuest > 0),
                        IsAbsent = g.All(x => x.NoOfGuest == 0),
                        Passengers = g.Sum(x => x.NoOfGuest)
                    });

                var lastDateMap = await _context.TourGuidePriorities
                    .Where(p => rfidInts.Contains(p.GuideIdPrior))
                    .GroupBy(p => p.GuideIdPrior)
                    .Select(g => new { GuideId = g.Key, LastDate = g.Max(x => x.Date) })
                    .ToDictionaryAsync(x => x.GuideId, x => x.LastDate);

                var sorted = rfidMap
                    .OrderBy(kv => lastDateMap.TryGetValue(kv.Key, out var d) ? d : "0")
                    .Select(kv => kv.Value)
                    .ToList();

                var queuePosMap = sorted
                    .Select((g, i) => new
                    {
                        Rfid = (int)long.Parse(g.Rfid.Trim()),
                        Pos = i + 1
                    })
                    .ToDictionary(x => x.Rfid, x => x.Pos);

                var availableGuides = sorted.Select(g =>
                {
                    int rfid = (int)long.Parse(g.Rfid.Trim());
                    var state = todayState.TryGetValue(rfid, out var s) ? s : null;
                    var fullName = BuildFullName(g.FName, g.MName, g.LName);

                    return (object)new
                    {
                        Rfid = g.Rfid.Trim(),
                        fullName,
                        Image = g.Image ?? string.Empty,
                        queuePosition = queuePosMap[rfid],
                        hasTrip = state?.HasTrip ?? false,
                        isAbsent = state?.IsAbsent ?? false,
                        passengers = state?.Passengers ?? 0
                    };
                }).ToList();

                var busyGuides = sorted
                    .Where(g => todayState.ContainsKey((int)long.Parse(g.Rfid.Trim())))
                    .Select(g =>
                    {
                        int rfid = (int)long.Parse(g.Rfid.Trim());
                        var state = todayState[rfid];
                        var fullName = BuildFullName(g.FName, g.MName, g.LName);

                        return (object)new
                        {
                            Rfid = g.Rfid.Trim(),
                            fullName,
                            Image = g.Image ?? string.Empty,
                            queuePos = queuePosMap[rfid],
                            isAbsent = state.IsAbsent,
                            passengers = state.Passengers
                        };
                    })
                    .ToList();

                ViewBag.Batch = batch;
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedGuides = recommendedGuides;
                ViewBag.OperatorName = operatorName;
                ViewBag.OperatorId = operatorId;
                ViewBag.AvailableGuides = availableGuides;
                ViewBag.BusyGuides = busyGuides;
                ViewBag.StartDate = startDate ?? "";
                ViewBag.EndDate = endDate ?? "";

                return PartialView("_AssignOutsideGuideModal");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Content($"<div class='p-3'><div class='alert alert-danger'><strong>Error loading modal:</strong><br/>{msg}</div></div>", "text/html");
            }
        }

        // ── Helper — safe full name build ────────────────────────────────────
        private static string BuildFullName(string fName, string? mName, string lName)
        {
            return string.IsNullOrWhiteSpace(mName)
                ? $"{fName} {lName}".Trim()
                : $"{fName} {mName} {lName}".Trim();
        }

        // =========================================================
        // ASSIGN OUTSIDE GUIDES — POST
        // ✅ Inserts into tourguide_priority
        // ✅ Inserts into tbl_batch_assignments (OutsideGuideId)
        // ✅ One row per guide with distributed guest count
        // ✅ Names sourced from outside_tourguide_details
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignOutsideGuide(string batch, List<string> guideRfids, string? startDate = null, string? endDate = null)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one guide are required." });

                int guestCount = await GetFilteredGuestCount(batch, startDate, endDate);
                int totalRequiredGuides = GetRequiredStaffCount(guestCount);
                var activeCounts = await GetActiveAssignedStaffCount(batch);
                int remainingGuideSlots = Math.Max(0, totalRequiredGuides - activeCounts.ActiveGuides);

                if (remainingGuideSlots <= 0)
                    return Json(new { success = false, message = "Guide limit already reached for this batch." });

                if (guideRfids.Count > remainingGuideSlots)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {remainingGuideSlots} guide(s). Limit already reached by current active assignments."
                    });

                var operatorId = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .Select(g => (int?)g.OperatorId)
                    .FirstOrDefaultAsync();

                int guidesCount = guideRfids.Count;
                int baseGuests = guidesCount > 0 ? guestCount / guidesCount : 0;
                int remainder = guidesCount > 0 ? guestCount % guidesCount : 0;

                var trimmedRfids = guideRfids.Select(r => r.Trim()).ToList();

                var guideMap = await _context.OutsideGuides
                    .Where(g => trimmedRfids.Contains(g.Rfid.Trim()))
                    .ToDictionaryAsync(g => g.Rfid.Trim(), g => g);

                var assignedNames = new List<string>();

                for (int i = 0; i < guidesCount; i++)
                {
                    var rfidStr = guideRfids[i].Trim();

                    if (!long.TryParse(rfidStr, out var rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                        continue;

                    int rfidInt = (int)rfidLong;
                    int assignedGuests = baseGuests + (i == 0 ? remainder : 0);

                    _context.TourGuidePriorities.Add(new TourGuidePriority
                    {
                        GuideIdPrior = rfidInt,
                        Date = UnixNow(),
                        NoOfGuest = assignedGuests
                    });

                    var outsideGuide = guideMap.TryGetValue(rfidStr, out var og) ? og : null;

                    _context.BatchAssignments.Add(new BatchAssignment
                    {
                        BatchCode = batch,
                        OperatorId = operatorId,
                        GuideId = null,
                        DriverId = null,
                        OutsideGuideId = outsideGuide?.OutsideGuideId
                    });

                    assignedNames.Add(outsideGuide?.FullName ?? rfidStr);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{guidesCount} outside guide(s) assigned: {string.Join("; ", assignedNames)}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // EDIT OUTSIDE GUIDE ASSIGNMENT
        // ✅ Updates latest tourguide_priority.NoOfGuest for today
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOutsideGuideAssignment(string guideRfid, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                if (passengers < 1)
                    return Json(new { success = false, message = "Guest count must be at least 1." });

                if (!long.TryParse(guideRfid, out long rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid guide RFID." });

                int rfidInt = (int)rfidLong;

                var record = await _context.TourGuidePriorities
                    .Where(p => p.GuideIdPrior == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (record == null)
                    return Json(new { success = false, message = "No active assignment found for this guide today." });

                record.NoOfGuest = passengers;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Guest count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // REMOVE OUTSIDE GUIDE ASSIGNMENT
        // ✅ Deletes all tourguide_priority records for this guide today
        // ✅ Guide returns to FIFO queue as if never assigned today
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveOutsideGuideAssignment(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                if (!long.TryParse(guideRfid, out long rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid guide RFID." });

                int rfidInt = (int)rfidLong;

                var records = await _context.TourGuidePriorities
                    .Where(p => p.GuideIdPrior == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (records.Any())
                    _context.TourGuidePriorities.RemoveRange(records);

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Guide assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // MARK OUTSIDE GUIDE ABSENT
        // ✅ Zeros out today's tourguide_priority.NoOfGuest
        // ✅ Inserts a 0-guest row if no record exists today
        // ✅ Guide stays in FIFO rotation
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkOutsideGuideAbsent(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                if (!long.TryParse(guideRfid, out long rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid guide RFID." });

                int rfidInt = (int)rfidLong;

                var todayRecords = await _context.TourGuidePriorities
                    .Where(p => p.GuideIdPrior == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (todayRecords.Any())
                {
                    foreach (var r in todayRecords)
                        r.NoOfGuest = 0;
                }
                else
                {
                    _context.TourGuidePriorities.Add(new TourGuidePriority
                    {
                        GuideIdPrior = rfidInt,
                        Date = UnixNow(),
                        NoOfGuest = 0
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Guide marked as absent. Still in queue rotation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // CLEAR OUTSIDE GUIDE ASSIGNMENT
        // ✅ Deletes today's tourguide_priority rows
        // ✅ Guide returns to queue as if never assigned today
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearOutsideGuideAssignment(string guideRfid)
        {
            try
            {
                if (string.IsNullOrEmpty(guideRfid))
                    return Json(new { success = false, message = "Guide RFID is required." });

                if (!long.TryParse(guideRfid, out long rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid guide RFID." });

                int rfidInt = (int)rfidLong;

                var records = await _context.TourGuidePriorities
                    .Where(p => p.GuideIdPrior == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (records.Any())
                    _context.TourGuidePriorities.RemoveRange(records);

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Guide is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

    }
}