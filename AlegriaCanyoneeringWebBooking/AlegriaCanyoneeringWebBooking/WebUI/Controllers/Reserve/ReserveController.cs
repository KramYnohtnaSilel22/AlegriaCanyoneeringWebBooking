using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using System.Text;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using AlegriaCanyoneeringWebBooking.Domain.Models;

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
                g.id,
                FullName = g.Fullname ?? "Unknown Guest",
                ArrivalDate = ParseUnixTimestamp(g.ArrivalDate),
                WristbandCode = g.RFIDCode,
                QRBase64 = GenerateQRCodeBase64(g.id.ToString()),
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
                .FirstOrDefault(g => g.id == guestId);

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
        public async Task<IActionResult> GetBatchDetails(string Batch)
        {
            try
            {
                if (string.IsNullOrEmpty(Batch))
                    return Json(new { success = false, message = "Batch code is required" });

                string batchNumbers = Batch.StartsWith("BATCH-") ? Batch.Substring(6) : Batch;

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

        [HttpGet]
        public async Task<IActionResult> GetGuestOfTheDay(int pageNumber = 1, int pageSize = 50, string batchFilter = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var todayUnix = ((DateTimeOffset)today).ToUnixTimeSeconds().ToString();
            var tomorrowUnix = ((DateTimeOffset)tomorrow).ToUnixTimeSeconds().ToString();

            var query = _context.Guests
                .AsNoTracking()
                .Include(g => g.NationalityEntity)
                .Where(g =>
                    g.BookingStatus == 0 &&
                    !string.IsNullOrEmpty(g.ArrivalDate) &&
                    string.Compare(g.ArrivalDate, todayUnix) >= 0 &&
                    string.Compare(g.ArrivalDate, tomorrowUnix) < 0);

            if (currentOperatorId.HasValue)
                query = query.Where(g => g.OperatorId == currentOperatorId.Value);

            if (!string.IsNullOrEmpty(batchFilter))
                query = query.Where(g => g.Batch.Contains(batchFilter));

            var totalGuests = await query.CountAsync();

            var pagedGuests = await query
                .OrderByDescending(g => g.ArrivalDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!pagedGuests.Any())
                return Json(new { success = false, message = "No guest arrivals found today." });

            var operatorIds = pagedGuests.Select(g => g.OperatorId).Distinct().ToList();
            var operators = await _context.Operators
                .Where(o => operatorIds.Contains(o.Id))
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            var vmList = pagedGuests.Select(g => new GuestWithOperatorVM
            {
                Guest = g,
                OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "No Operator"
            }).ToList();

            var model = new GuestPaginationViewModel
            {
                Guests = vmList,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalGuests,
                TotalPages = (int)Math.Ceiling(totalGuests / (double)pageSize),
                BatchFilter = batchFilter
            };

            return PartialView("_GuestDetailsPartial", model);
        }

        // =========================================================
        // GET GUESTS BY BATCH — scopes guides/drivers to batch
        // ✅ Date filters updated to unix timestamp range
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("Batch code is required.");

            // ── LOAD OPERATORS & GUESTS ──────────────────────────
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            var guests = await _context.Guests
                .Include(g => g.NationalityEntity)
                .Where(g => g.Batch == batchCode)
                .ToListAsync();

            var guestsWithOperatorName = guests
                .Select(g => new GuestWithOperatorVM
                {
                    Guest = g,
                    OperatorName = operators
                        .FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "No Operator"
                })
                .ToList();

            // ── DATE HELPERS ─────────────────────────────────────
            // Guide DTR still uses yyyyMMdd long
            var todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

            // Attendance & driver records use unix timestamp strings
            var todayStart = UnixTodayStart();
            var todayEnd = UnixTodayEnd();

            // =========================================================
            // GET ASSIGNED GUIDES — scoped to THIS batch
            // TGId format: "rfid|batchCode" (new) OR "rfid" (legacy)
            // =========================================================
            var attendanceToday = await _context.TourGuideAttendances
                .Where(a => string.Compare(a.Date, todayStart) >= 0
                         && string.Compare(a.Date, todayEnd) < 0)   // ✅ unix range
                .ToListAsync();

            // Try new batch-scoped format first
            var batchGuideRfids = attendanceToday
                .Where(a => !string.IsNullOrEmpty(a.TGId) && a.TGId.Contains("|"))
                .Where(a => a.TGId.Split('|')[1] == batchCode)
                .Select(a => a.TGId.Split('|')[0])
                .Distinct()
                .ToList();

            // Fallback: legacy records with no batch suffix
            if (!batchGuideRfids.Any())
            {
                batchGuideRfids = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.TGId) && !a.TGId.Contains("|"))
                    .Select(a => a.TGId)
                    .Distinct()
                    .ToList();
            }

            var guideDtrToday = await _context.TourGuideDtrs
                .Where(d => d.Date == todayLong)                        // guide DTR stays yyyyMMdd long
                .ToListAsync();

            var guidePassengerMap = guideDtrToday
                .GroupBy(d => d.Rfid)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

            var guideEntities = await _context.Guides
                .Where(g => batchGuideRfids.Contains(g.Rfid))
                .Select(g => new { g.Rfid, g.FName, g.MName, g.LName, g.Image })
                .ToListAsync();

            var assignedGuides = guideEntities.Select(g =>
            {
                int passengers = 0;
                if (long.TryParse(g.Rfid, out long rfidLong))
                    guidePassengerMap.TryGetValue(rfidLong, out passengers);

                return new
                {
                    Rfid = g.Rfid,
                    fullName = $"{g.FName ?? ""} {g.MName ?? ""} {g.LName ?? ""}".Trim(),
                    Image = g.Image,
                    passengers
                };
            }).ToList();

            // =========================================================
            // GET ASSIGNED DRIVERS — scoped to THIS batch
            // DriverId format: "refId|batchCode" (new) OR "refId" (legacy)
            // =========================================================
            var driverAttendanceToday = await _context.DriverAttendances
                .Where(a => string.Compare(a.Date, todayStart) >= 0
                         && string.Compare(a.Date, todayEnd) < 0)   // ✅ unix range
                .ToListAsync();

            // Try new batch-scoped format first
            var batchDriverRefIds = driverAttendanceToday
                .Where(a => !string.IsNullOrEmpty(a.DriverId) && a.DriverId.Contains("|"))
                .Where(a => a.DriverId.Split('|')[1] == batchCode)
                .Select(a => a.DriverId.Split('|')[0])
                .Distinct()
                .ToList();

            // Fallback: legacy records with no batch suffix
            if (!batchDriverRefIds.Any())
            {
                batchDriverRefIds = driverAttendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.DriverId) && !a.DriverId.Contains("|"))
                    .Select(a => a.DriverId)
                    .Distinct()
                    .ToList();
            }

            // Passenger map keyed by plain refId (strip batch suffix if present)
            var driverPassengerMap = driverAttendanceToday
                .Where(a => !string.IsNullOrEmpty(a.DriverId))
                .GroupBy(a => a.DriverId.Contains("|") ? a.DriverId.Split('|')[0] : a.DriverId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Passenger));

            var driverEntities = await _context.Drivers
                .Where(d => d.RefId != null && batchDriverRefIds.Contains(d.RefId))
                .Select(d => new { d.RefId, d.FName, d.MName, d.LName, d.Image })
                .ToListAsync();

            var assignedDrivers = driverEntities.Select(d =>
            {
                driverPassengerMap.TryGetValue(d.RefId, out int passengers);
                return new
                {
                    RefId = d.RefId,
                    fullName = $"{d.FName ?? ""} {d.MName ?? ""} {d.LName ?? ""}".Trim(),
                    Image = d.Image,
                    passengers
                };
            }).ToList();

            // ── PASS TO VIEW ─────────────────────────────────────
            ViewBag.AssignedGuides = assignedGuides;
            ViewBag.AssignedDrivers = assignedDrivers;

            return PartialView("ViewGuestDetails", guestsWithOperatorName);
        }


        private string GenerateQRText(Guest guest) => $"Batch        : {guest.Batch}";

        private string GenerateQRCodeBase64(string data)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData  = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            var qrCode      = new PngByteQRCode(qrCodeData);
            var qrBytes     = qrCode.GetGraphic(20);
            return "data:image/png;base64," + Convert.ToBase64String(qrBytes);
        }

        public IActionResult BookedGuest() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetGuestsData(string? startDate, string? endDate)
        {
            var draw   = Request.Form["draw"].FirstOrDefault();
            var start  = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            long? startUnix = null, endUnix = null;
            if (DateTime.TryParse(startDate, out DateTime sd))
                startUnix = new DateTimeOffset(sd.Date).ToUnixTimeSeconds();
            if (DateTime.TryParse(endDate, out DateTime ed))
                endUnix = new DateTimeOffset(ed.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();

            var query = from g in _context.Guests
                        join o in _context.Operators on g.OperatorId equals o.Id
                        where g.BookingStatus == 0
                        select new { Guest = g, OperatorName = o.BusinessName };

            if (currentOperatorId.HasValue)
                query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(x =>
                    x.Guest.Fullname.Contains(search) ||
                    x.Guest.Batch.Contains(search) ||
                    x.OperatorName.Contains(search));

            if (startUnix.HasValue && endUnix.HasValue)
                query = query.Where(x => !string.IsNullOrEmpty(x.Guest.ArrivalDate));

            var guestsList = await query.ToListAsync();

            if (startUnix.HasValue && endUnix.HasValue)
            {
                guestsList = guestsList
                    .Where(x =>
                        long.TryParse(x.Guest.ArrivalDate, out var unix) &&
                        unix >= startUnix.Value &&
                        unix <= endUnix.Value)
                    .ToList();
            }

            var groupedData = guestsList
                .GroupBy(x => new { x.Guest.Batch, x.OperatorName })
                .Select(g => new
                {
                    batch        = g.Key.Batch,
                    operatorName = g.Key.OperatorName,
                    totalGuests  = g.Count(),
                    arrivalDate  = g.First().Guest.ArrivalDate,
                    status       = "Confirmed"
                })
                .ToList();

            var pagedData = groupedData.Skip(start).Take(length);

            return Json(new
            {
                draw,
                recordsTotal    = groupedData.Count,
                recordsFiltered = groupedData.Count,
                data            = pagedData
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingDetails(int id)
        {
            try
            {
                var guest = await _context.Guests
                    .Include(g => g.NationalityEntity)
                    .Include(g => g.OperatorList)
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

        [HttpPost]
        [Authorize(Roles = "Super Admin")]
        public async Task<IActionResult> BookedGuest(string BatchCode)
        {
            try
            {
                var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        // DATE HELPERS — add these as private methods in ReserveController
        // =========================================================

        /// <summary>Unix timestamp of RIGHT NOW — used for attendance/priority Date fields</summary>
        private static string UnixNow()
            => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        /// <summary>
        /// Today's START unix timestamp (midnight local) — used for "today" range filters
        /// </summary>
        private static string UnixTodayStart()
            => new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds().ToString();

        /// <summary>
        /// Tomorrow's START unix timestamp — upper bound for "today" range filters
        /// </summary>
        private static string UnixTodayEnd()
            => new DateTimeOffset(DateTime.Today.AddDays(1)).ToUnixTimeSeconds().ToString();

        /// <summary>
        /// "Wed May 29 2019 09:32:03:83" format — used for ComDate / ComDateDr DTR fields
        /// </summary>
        private static string DtrDateNow()
            => DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss:ff");


        // =========================================================
        // HOW TO FILTER "TODAY" WITH UNIX TIMESTAMPS
        // Replace every:  .Where(a => a.Date == today)
        // With this range filter (string compare works — all 10-digit unix stamps)
        //
        //   .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
        //            && string.Compare(a.Date, UnixTodayEnd())   <  0)
        // =========================================================



        // =========================================================
        // ASSIGN DRIVER — GET modal form (FIFO Queue + Absent)
        // =========================================================
        // =========================================================
        // GET ASSIGN DRIVER MODAL (updated "today" filter)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignDriverModal(string batch)
        {
            try
            {
                var allDrivers = await _context.Drivers
                    .OrderBy(d => d.FName)
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        fullName = ((d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? "")).Trim(),
                        d.Image
                    })
                    .ToListAsync();

                // ✅ Filter today using unix timestamp range
                var priorityToday = await _context.DriverIdPriors
                    .Where(p => string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var lastAssignmentMap = priorityToday
                    .GroupBy(p => p.DriverIdPriorValue)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                var driverDtrToday = await _context.DriverDtrs
                    .Where(d => string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var driverPassengerMap = driverDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.Passenger, out int p) ? p : 0));

                var driversWithDtrRfidSet = driverDtrToday.Select(d => d.Rfid).ToHashSet();
                var absentDriverRefIds = new HashSet<string>();

                foreach (var d in allDrivers)
                {
                    if (!int.TryParse(d.RefId, out int rInt)) continue;
                    if (!lastAssignmentMap.ContainsKey(rInt)) continue;
                    if (driversWithDtrRfidSet.Contains(rInt)
                        && driverPassengerMap.ContainsKey(rInt)
                        && driverPassengerMap[rInt] == 0)
                    {
                        absentDriverRefIds.Add(d.RefId);
                    }
                }

                var orderedDrivers = allDrivers
                    .OrderBy(d => { int.TryParse(d.RefId, out int rid); return lastAssignmentMap.ContainsKey(rid) ? 1 : 0; })
                    .ThenBy(d => { int.TryParse(d.RefId, out int rid); return lastAssignmentMap.ContainsKey(rid) ? lastAssignmentMap[rid] : 0; })
                    .ThenBy(d => d.fullName)
                    .ToList();

                var availableDriversRaw = new List<object>();
                for (int i = 0; i < orderedDrivers.Count; i++)
                {
                    var d = orderedDrivers[i];
                    int.TryParse(d.RefId, out int refInt);
                    availableDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        hasTrip = lastAssignmentMap.ContainsKey(refInt),
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePosition = i + 1,
                        passengers = refInt > 0 && driverPassengerMap.ContainsKey(refInt) ? driverPassengerMap[refInt] : 0
                    });
                }
                var availableDrivers = availableDriversRaw.Cast<dynamic>().ToList();

                var assignedTodayList = orderedDrivers
                    .Where(d => { int.TryParse(d.RefId, out int rid); return lastAssignmentMap.ContainsKey(rid); })
                    .ToList();

                var busyDriversRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var d = assignedTodayList[i];
                    int.TryParse(d.RefId, out int refInt2);
                    busyDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePos = i + 1,
                        passengers = refInt2 > 0 && driverPassengerMap.ContainsKey(refInt2) ? driverPassengerMap[refInt2] : 0
                    });
                }
                var busyDriversList = busyDriversRaw.Cast<dynamic>().ToList();

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedDrivers = GetRequiredStaffCount(guestCount);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedDrivers = recommendedDrivers;
                ViewBag.AvailableDrivers = availableDrivers;
                ViewBag.BusyDrivers = busyDriversList;

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
        // ASSIGN MULTIPLE DRIVERS — POST (updated date formats)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDrivers(string batch, List<string> driverRefIds)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || driverRefIds == null || !driverRefIds.Any())
                    return Json(new { success = false, message = "Batch and at least one Driver are required." });

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedDrivers = GetRequiredStaffCount(guestCount);

                if (driverRefIds.Count > recommendedDrivers)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {recommendedDrivers} driver(s) for {guestCount} guest(s)."
                    });

                int driversCount = driverRefIds.Count;
                int basePassengers = driversCount > 0 ? guestCount / driversCount : 0;
                int remainder = driversCount > 0 ? guestCount % driversCount : 0;

                var assignedNames = new List<string>();

                for (int i = 0; i < driversCount; i++)
                {
                    var driverRefId = driverRefIds[i];
                    var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                    if (driver == null) continue;

                    int rfidValue = int.TryParse(driver.RefId, out int parsed) ? parsed : driver.DriverId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = $"{driverRefId}|{batch}",
                        Date = UnixNow(),                  // ✅ unix timestamp
                        Passenger = assignedPassengers
                    });

                    _context.DriverDtrs.Add(new DriverDtr
                    {
                        Rfid = rfidValue,
                        Date = UnixNow(),                  // ✅ unix timestamp
                        Passenger = assignedPassengers.ToString(),
                        ComDateDr = DtrDateNow()                // ✅ "Wed May 29 2019 09:32:03:83"
                    });

                    _context.DriverIdPriors.Add(new DriverIdPrior
                    {
                        DriverIdPriorValue = rfidValue,
                        Date = UnixNow(),         // ✅ unix timestamp
                        Passenger = assignedPassengers
                    });

                    assignedNames.Add($"{driver.FName} {driver.LName}");
                }

                await _context.SaveChangesAsync();

                var names = string.Join(", ", assignedNames);
                return Json(new { success = true, message = $"{driversCount} driver(s) assigned: {names}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // EDIT DRIVER ASSIGNMENT (updated filter)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDriverAssignment(string driverRefId, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (passengers < 0)
                    return Json(new { success = false, message = "Passenger count cannot be negative." });

                if (!int.TryParse(driverRefId, out int rfidInt))
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                // ✅ Filter using unix timestamp range for today
                var dtr = await _context.DriverDtrs
                    .Where(d => d.Rfid == rfidInt
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (dtr == null)
                    return Json(new { success = false, message = "No active assignment found for this driver today." });

                dtr.Passenger = passengers.ToString();
                dtr.ComDateDr = DtrDateNow();                   // ✅ updated ComDateDr

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Passenger count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // REMOVE DRIVER ASSIGNMENT (updated filter)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (!int.TryParse(driverRefId, out int rfidInt))
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                // ✅ Filter using unix timestamp range for today
                var attendances = await _context.DriverAttendances
                    .Where(a => (a.DriverId == driverRefId || a.DriverId.StartsWith(driverRefId + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any()) _context.DriverAttendances.RemoveRange(attendances);

                var dtrs = await _context.DriverDtrs
                    .Where(d => d.Rfid == rfidInt
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (dtrs.Any()) _context.DriverDtrs.RemoveRange(dtrs);

                var priorities = await _context.DriverIdPriors
                    .Where(p => p.DriverIdPriorValue == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (priorities.Any()) _context.DriverIdPriors.RemoveRange(priorities);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // MARK DRIVER ABSENT (updated date formats + filter)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDriverAbsent(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (!int.TryParse(driverRefId, out int rfidInt))
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                // ✅ Filter using unix timestamp range for today
                var attendance = await _context.DriverAttendances
                    .Where(a => (a.DriverId == driverRefId || a.DriverId.StartsWith(driverRefId + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .FirstOrDefaultAsync();

                if (attendance == null)
                {
                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = driverRefId,
                        Date = UnixNow(),                  // ✅ unix timestamp
                        Passenger = 0
                    });
                }
                else
                {
                    attendance.Passenger = 0;
                }

                var priority = await _context.DriverIdPriors
                    .Where(p => p.DriverIdPriorValue == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .FirstOrDefaultAsync();

                if (priority == null)
                {
                    _context.DriverIdPriors.Add(new DriverIdPrior
                    {
                        DriverIdPriorValue = rfidInt,
                        Date = UnixNow(),         // ✅ unix timestamp
                        Passenger = 0
                    });
                }

                var existingDtrs = await _context.DriverDtrs
                    .Where(d => d.Rfid == rfidInt
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (existingDtrs.Any()) _context.DriverDtrs.RemoveRange(existingDtrs);

                _context.DriverDtrs.Add(new DriverDtr
                {
                    Rfid = rfidInt,
                    Date = UnixNow(),                      // ✅ unix timestamp
                    Passenger = "0",
                    ComDateDr = DtrDateNow()                    // ✅ "Wed May 29 2019 09:32:03:83"
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver marked as absent. Still in queue rotation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // CLEAR DRIVER (updated filter)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                var attendances = await _context.DriverAttendances
                    .Where(a => (a.DriverId == driverRefId || a.DriverId.StartsWith(driverRefId + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any()) _context.DriverAttendances.RemoveRange(attendances);

                int.TryParse(driverRefId, out int rfidInt);

                var priorities = await _context.DriverIdPriors
                    .Where(p => p.DriverIdPriorValue == rfidInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (priorities.Any()) _context.DriverIdPriors.RemoveRange(priorities);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // GET ASSIGNED BATCHES
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignedBatches()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            var allEntries = await _context.DriverAttendances
                .Where(a => a.Date == today)
                .Select(a => a.DriverId)
                .ToListAsync();

            var assignedBatches = allEntries
                .Where(id => !string.IsNullOrEmpty(id) && id.Contains("|"))
                .Select(id => id.Split('|')[1])
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToList();

            return Json(new { assignedBatches });
        }

        // =========================================================
        // GET ASSIGN GUIDE MODAL (updated "today" attendance filter)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignGuideModal(string batch)
        {
            try
            {
                var allGuides = await _context.Guides
                    .OrderBy(g => g.FName)
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim(),
                        g.Image
                    })
                    .ToListAsync();

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                // ✅ Filter attendance using unix timestamp range for today
                var attendanceToday = await _context.TourGuideAttendances
                    .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                // Strip "|batch" suffix for global FIFO position
                var lastAssignmentMap = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.TGId))
                    .GroupBy(a => a.TGId.Contains("|") ? a.TGId.Split('|')[0] : a.TGId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                var guideDtrToday = await _context.TourGuideDtrs
                    .Where(d => d.Date == todayLong)
                    .ToListAsync();

                var guidePassengerMap = guideDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

                var guidesWithDtrRfidSet = guideDtrToday.Select(d => d.Rfid).ToHashSet();
                var absentGuideRfids = new HashSet<string>();

                foreach (var g in allGuides.Where(x => lastAssignmentMap.ContainsKey(x.Rfid)))
                {
                    if (long.TryParse(g.Rfid, out long rLong)
                        && guidesWithDtrRfidSet.Contains(rLong)
                        && guidePassengerMap.ContainsKey(rLong)
                        && guidePassengerMap[rLong] == 0)
                    {
                        absentGuideRfids.Add(g.Rfid);
                    }
                }

                var orderedGuides = allGuides
                    .OrderBy(g => lastAssignmentMap.ContainsKey(g.Rfid) ? 1 : 0)
                    .ThenBy(g => lastAssignmentMap.ContainsKey(g.Rfid) ? lastAssignmentMap[g.Rfid] : 0)
                    .ThenBy(g => g.fullName)
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
                        passengers = rfidL > 0 && guidePassengerMap.ContainsKey(rfidL) ? guidePassengerMap[rfidL] : 0
                    });
                }
                var availableGuides = availableGuidesRaw.Cast<dynamic>().ToList();

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
                        passengers = rfidL2 > 0 && guidePassengerMap.ContainsKey(rfidL2) ? guidePassengerMap[rfidL2] : 0
                    });
                }
                var busyGuidesList = busyGuidesRaw.Cast<dynamic>().ToList();

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedGuides = recommendedGuides;
                ViewBag.AvailableGuides = availableGuides;
                ViewBag.BusyGuides = busyGuidesList;

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
        // ASSIGN MULTIPLE GUIDES — POST (updated date formats)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignGuides(string batch, List<string> guideRfids)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one Guide are required." });

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd")); // DTR Date stays yyyyMMdd long

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                if (guideRfids.Count > recommendedGuides)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {recommendedGuides} guide(s) for {guestCount} guest(s)."
                    });

                int guidesCount = guideRfids.Count;
                int basePassengers = guidesCount > 0 ? guestCount / guidesCount : 0;
                int remainder = guidesCount > 0 ? guestCount % guidesCount : 0;

                var assignedNames = new List<string>();

                for (int i = 0; i < guidesCount; i++)
                {
                    var guideRfid = guideRfids[i];
                    var guide = await _context.Guides.FirstOrDefaultAsync(g => g.Rfid == guideRfid);
                    if (guide == null) continue;

                    long rfidLong = long.TryParse(guide.Rfid, out long parsed) ? parsed : guide.GuideId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    _context.TourGuideAttendances.Add(new TourGuideAttendance
                    {
                        TGId = $"{guide.Rfid}|{batch}",
                        Date = UnixNow(),                       // ✅ unix timestamp
                        Rfid = guide.Rfid
                    });

                    _context.TourGuideDtrs.Add(new TourGuideDtr
                    {
                        Rfid = rfidLong,
                        Date = todayLong,                  // guide DTR Date stays as yyyyMMdd long
                        NoOfGuest = assignedPassengers.ToString(),
                        ComDate = DtrDateNow()                // ✅ "Wed May 29 2019 09:32:03:83"
                    });

                    assignedNames.Add($"{guide.FName} {guide.LName}");
                }

                await _context.SaveChangesAsync();

                var names = string.Join(", ", assignedNames);
                return Json(new { success = true, message = $"{guidesCount} guide(s) assigned: {names}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        // =========================================================
        // EDIT GUIDE ASSIGNMENT (ComDate updated)
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
                dtr.ComDate = DtrDateNow();                   // ✅ "Wed May 29 2019 09:32:03:83"

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Passenger count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }



        // =========================================================
        // REMOVE GUIDE ASSIGNMENT (updated attendance filter)
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

                if (!long.TryParse(guideRfid, out long rfidLong))
                    return Json(new { success = false, message = "Invalid guide RFID." });

                // ✅ Filter attendance using unix timestamp range for today
                var attendances = await _context.TourGuideAttendances
                    .Where(a => (a.TGId == guideRfid || a.TGId.StartsWith(guideRfid + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any()) _context.TourGuideAttendances.RemoveRange(attendances);

                var dtrs = await _context.TourGuideDtrs
                    .Where(d => d.Rfid == rfidLong && d.Date == todayLong)
                    .ToListAsync();

                if (dtrs.Any()) _context.TourGuideDtrs.RemoveRange(dtrs);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Guide assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }



        // =========================================================
        // MARK GUIDE ABSENT (updated date formats)
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

                var attendance = await _context.TourGuideAttendances
                    .Where(a => (a.TGId == guideRfid || a.TGId.StartsWith(guideRfid + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .FirstOrDefaultAsync();

                if (attendance == null)
                {
                    _context.TourGuideAttendances.Add(new TourGuideAttendance
                    {
                        TGId = guideRfid,
                        Date = UnixNow(),                       // ✅ unix timestamp
                        Rfid = guideRfid
                    });
                }

                var existingDtrs = await _context.TourGuideDtrs
                    .Where(d => d.Rfid == rfidLong && d.Date == todayLong)
                    .ToListAsync();

                if (existingDtrs.Any()) _context.TourGuideDtrs.RemoveRange(existingDtrs);

                _context.TourGuideDtrs.Add(new TourGuideDtr
                {
                    Rfid = rfidLong,
                    Date = todayLong,                      // guide DTR Date stays as yyyyMMdd long
                    NoOfGuest = "0",
                    ComDate = DtrDateNow()                    // ✅ "Wed May 29 2019 09:32:03:83"
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
        // CLEAR GUIDE (updated attendance filter)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearGuideAssignment(string guideRfid)
        {
            try
            {
                var toRemove = await _context.TourGuideAttendances
                    .Where(a => (a.TGId == guideRfid || a.TGId.StartsWith(guideRfid + "|"))
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (toRemove.Any()) _context.TourGuideAttendances.RemoveRange(toRemove);

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
