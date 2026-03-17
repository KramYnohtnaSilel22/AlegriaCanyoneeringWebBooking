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
        // GET GUESTS BY BATCH
        // ✅ Guides: passenger count from TourGuideDtr.NoOfGuest
        // ✅ Drivers: passenger count from DriverDtr.Passenger
        // ✅ Absent = explicitly marked (MarkGuideAbsent / MarkDriverAbsent)
        // ✅ Drivers/Guides with 0 pax from distribution still show
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("Batch code is required.");

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

            long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

            // ── GUIDES ───────────────────────────────────────────────────
            var assignedGuideIds = await _context.BatchAssignments
                .Where(b => b.BatchCode == batchCode && b.GuideId != null)
                .Select(b => b.GuideId)
                .Distinct()
                .ToListAsync();

            var guideEntities = await _context.Guides
                .Where(g => assignedGuideIds.Contains(g.GuideId))
                .Select(g => new { g.GuideId, g.Rfid, g.FName, g.MName, g.LName, g.Image })
                .ToListAsync();

            var guideRfidLongs = guideEntities
                .Where(g => long.TryParse(g.Rfid, out _))
                .Select(g => long.Parse(g.Rfid))
                .ToList();

            var guideDtrToday = await _context.TourGuideDtrs
                .Where(d => guideRfidLongs.Contains(d.Rfid) && d.Date == todayLong)
                .ToListAsync();

            var guidePassengerMap = guideDtrToday
                .GroupBy(d => d.Rfid)
                .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

            // ✅ Absent guide = has DTR today with ALL NoOfGuest == "0"
            var absentGuideRfids = guideRfidLongs
                .Where(rfid =>
                {
                    var records = guideDtrToday.Where(d => d.Rfid == rfid).ToList();
                    return records.Any() && records.All(d => d.NoOfGuest == "0");
                })
                .ToHashSet();

            // ✅ FIXED: explicit named assignments in anonymous type
            var assignedGuides = guideEntities
                .Select(g =>
                {
                    long.TryParse(g.Rfid, out long rfidLong);
                    int passengers = 0;
                    guidePassengerMap.TryGetValue(rfidLong, out passengers);
                    bool isAbsent = absentGuideRfids.Contains(rfidLong);
                    string fullName = $"{g.FName ?? ""} {g.MName ?? ""} {g.LName ?? ""}".Trim();

                    return new
                    {
                        Rfid = g.Rfid,
                        fullName = fullName,        // ✅ explicit assignment
                        Image = g.Image,
                        passengers = passengers,
                        isAbsent = isAbsent
                    };
                })
                .Where(g => !g.isAbsent)
                .Select(g => new
                {
                    Rfid = g.Rfid,
                    fullName = g.fullName,
                    Image = g.Image,
                    passengers = g.passengers
                })
                .ToList<object>();

            // ── DRIVERS ──────────────────────────────────────────────────
            var assignedDriverIds = await _context.BatchAssignments
                .Where(b => b.BatchCode == batchCode && b.DriverId != null)
                .Select(b => b.DriverId)
                .Distinct()
                .ToListAsync();

            var driverEntities = await _context.Drivers
                .Where(d => assignedDriverIds.Contains(d.DriverId))
                .Select(d => new { d.DriverId, d.RefId, d.FName, d.MName, d.LName, d.Image })
                .ToListAsync();

            var driverRfidInts = driverEntities
                .Where(d => int.TryParse(d.RefId, out _))
                .Select(d => int.Parse(d.RefId))
                .ToList();

            var driverDtrToday = await _context.DriverDtrs
                .Where(d => driverRfidInts.Contains(d.Rfid)
                         && string.Compare(d.Date, UnixTodayStart()) >= 0
                         && string.Compare(d.Date, UnixTodayEnd()) < 0)
                .ToListAsync();

            var driverPassengerMap = driverDtrToday
                .GroupBy(d => d.Rfid)
                .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.Passenger, out int p) ? p : 0));

            var driverAttendanceToday = await _context.DriverAttendances
                .Where(a => driverEntities.Select(d => d.RefId).Contains(a.DriverId)
                         && string.Compare(a.Date, UnixTodayStart()) >= 0
                         && string.Compare(a.Date, UnixTodayEnd()) < 0)
                .ToListAsync();

            var attendancePassengerMap = driverAttendanceToday
                .GroupBy(a => a.DriverId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Passenger));

            // ✅ Absent driver = ALL DTR records today are "0" + attendance is 0
            var absentDriverRefIds = new HashSet<string>();
            foreach (var d in driverEntities)
            {
                if (!int.TryParse(d.RefId, out int rInt)) continue;
                var dtrRecords = driverDtrToday.Where(x => x.Rfid == rInt).ToList();
                if (!dtrRecords.Any()) continue;

                bool allZero = dtrRecords.All(x => x.Passenger == "0");
                bool attendanceZero = !attendancePassengerMap.ContainsKey(d.RefId)
                                      || attendancePassengerMap[d.RefId] == 0;

                if (allZero && attendanceZero)
                    absentDriverRefIds.Add(d.RefId);
            }

            // ✅ FIXED: explicit named assignments in anonymous type
            var assignedDrivers = driverEntities
                .Select(d =>
                {
                    int.TryParse(d.RefId, out int rfidInt);
                    int passengers = 0;
                    driverPassengerMap.TryGetValue(rfidInt, out passengers);
                    string fullName = $"{d.FName ?? ""} {d.MName ?? ""} {d.LName ?? ""}".Trim();

                    return new
                    {
                        RefId = d.RefId,
                        fullName = fullName,        // ✅ explicit assignment
                        Image = d.Image,
                        passengers = passengers,
                        isAbsent = absentDriverRefIds.Contains(d.RefId)
                    };
                })
                .Where(d => !d.isAbsent)
                .Select(d => new
                {
                    RefId = d.RefId,
                    fullName = d.fullName,
                    Image = d.Image,
                    passengers = d.passengers
                })
                .ToList<object>();

            ViewBag.AssignedGuides = assignedGuides;
            ViewBag.AssignedDrivers = assignedDrivers;

            return PartialView("ViewGuestDetails", guestsWithOperatorName);
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
                        OperatorList = new OperatorList
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

        public IActionResult BookedGuest() => View();

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
                    batch = g.Key.Batch,
                    operatorName = g.Key.OperatorName,
                    totalGuests = g.Count(),
                    arrivalDate = g.First().Guest.ArrivalDate,
                    status = "Confirmed"
                })
                .ToList();

            var pagedData = groupedData.Skip(start).Take(length);

            return Json(new
            {
                draw,
                recordsTotal = groupedData.Count,
                recordsFiltered = groupedData.Count,
                data = pagedData
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
        // DATE HELPERS
        // =========================================================

        private static string UnixNow()
            => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        private static string UnixTodayStart()
            => new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds().ToString();

        private static string UnixTodayEnd()
            => new DateTimeOffset(DateTime.Today.AddDays(1)).ToUnixTimeSeconds().ToString();

        /// <summary>"Wed May 29 2019 09:32:03:83" format — used for ComDate / ComDateDr DTR fields</summary>
        private static string DtrDateNow()
            => DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss:ff");


        // =========================================================
        // GET ASSIGNED BATCHES
        // ✅ Only returns batches that have a driver attendance TODAY
        // ✅ Cross-references BatchAssignment + DriverAttendance date
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignedBatches()
        {
            // Step 1: get all DriverIds (RefId) that have an attendance record today
            var todayDriverIds = await _context.DriverAttendances
                .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                         && string.Compare(a.Date, UnixTodayEnd()) < 0)
                .Select(a => a.DriverId)
                .Distinct()
                .ToListAsync();

            // Step 2: get the driver PK ids for those RefIds
            var driverPkIds = await _context.Drivers
                .Where(d => todayDriverIds.Contains(d.RefId))
                .Select(d => (int?)d.DriverId)
                .ToListAsync();

            // Step 3: find BatchAssignment rows that match those driver PKs
            var assignedBatches = await _context.BatchAssignments
                .Where(b => !string.IsNullOrEmpty(b.BatchCode)
                         && b.DriverId != null
                         && driverPkIds.Contains(b.DriverId))
                .Select(b => b.BatchCode)
                .Distinct()
                .ToListAsync();

            return Json(new { assignedBatches });
        }

        // =========================================================
        // GET ASSIGN GUIDE MODAL
        // ✅ FIFO ordered by Guide.TPosition (Unix timestamp)
        // ✅ TPosition passed to ViewBag for display in modal
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignGuideModal(string batch)
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

                // ✅ FIFO: last attendance Id per guide (TGId = Rfid)
                var lastAssignmentMap = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.TGId))
                    .GroupBy(a => a.TGId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                // ✅ Passenger totals from TourGuideDtr (Date = yyyyMMdd long)
                var guideDtrToday = await _context.TourGuideDtrs
                    .Where(d => d.Date == todayLong)
                    .ToListAsync();

                var guidePassengerMap = guideDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

                // ✅ Absent = has attendance today + DTR today + NoOfGuest total == 0
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

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedGuides = recommendedGuides;
                ViewBag.AvailableGuides = availableGuidesRaw.Cast<dynamic>().ToList();
                ViewBag.BusyGuides = busyGuidesRaw.Cast<dynamic>().ToList();

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
        public async Task<IActionResult> AssignGuides(string batch, List<string> guideRfids)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one Guide are required." });

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

                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                var operatorId = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .Select(g => (int?)g.OperatorId)
                    .FirstOrDefaultAsync();

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

                    // ✅ Update TPosition so guide moves to the bottom of the FIFO queue
                    guide.TPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
        // GET ASSIGN DRIVER MODAL
        // ✅ FIFO ordered by Driver.DPosition (Unix timestamp)
        // ✅ DPosition passed to ViewBag for display in modal
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignDriverModal(string batch)
        {
            try
            {
                var allDrivers = await _context.Drivers
                    .OrderBy(d => d.DPosition)                          // ✅ FIFO by DPosition
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        fullName = ((d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? "")).Trim(),
                        d.Image,
                        d.DPosition                                     // ✅ included
                    })
                    .ToListAsync();

                // ✅ FIFO from DriverAttendance — Date = unix timestamp
                var attendanceToday = await _context.DriverAttendances
                    .Where(a => string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var lastAssignmentMap = attendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.DriverId))
                    .GroupBy(a => a.DriverId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Id));

                var driverDtrToday = await _context.DriverDtrs
                    .Where(d => string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                var driverPassengerMap = driverDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.Passenger, out int p) ? p : 0));

                int GetRfidKey(int driverId, string refId)
                {
                    if (long.TryParse(refId, out long l) && l > 0 && l <= int.MaxValue)
                        return (int)l;
                    return driverId;
                }

                var absentDriverRefIds = new HashSet<string>();
                foreach (var d in allDrivers)
                {
                    if (!lastAssignmentMap.ContainsKey(d.RefId)) continue;
                    int rfidKey = GetRfidKey(d.DriverId, d.RefId);
                    if (driverPassengerMap.ContainsKey(rfidKey) && driverPassengerMap[rfidKey] == 0)
                        absentDriverRefIds.Add(d.RefId);
                }

                // ✅ FIFO order: by DPosition (already ordered from DB)
                //    never-assigned first, then by DPosition for assigned ones
                var orderedDrivers = allDrivers
                    .OrderBy(d => lastAssignmentMap.ContainsKey(d.RefId) ? 1 : 0)
                    .ThenBy(d => d.DPosition)                           // ✅ DPosition as tiebreaker
                    .ToList();

                var availableDriversRaw = new List<object>();
                for (int i = 0; i < orderedDrivers.Count; i++)
                {
                    var d = orderedDrivers[i];
                    int rfidKey = GetRfidKey(d.DriverId, d.RefId);
                    availableDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        hasTrip = lastAssignmentMap.ContainsKey(d.RefId),
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePosition = i + 1,
                        passengers = driverPassengerMap.ContainsKey(rfidKey)
                                        ? driverPassengerMap[rfidKey] : 0,
                        dPosition = d.DPosition                         // ✅ pass to view
                    });
                }

                var assignedTodayList = orderedDrivers
                    .Where(d => lastAssignmentMap.ContainsKey(d.RefId))
                    .ToList();

                var busyDriversRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var d = assignedTodayList[i];
                    int rfidKey2 = GetRfidKey(d.DriverId, d.RefId);
                    busyDriversRaw.Add(new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        isAbsent = absentDriverRefIds.Contains(d.RefId),
                        queuePos = i + 1,
                        passengers = driverPassengerMap.ContainsKey(rfidKey2)
                                     ? driverPassengerMap[rfidKey2] : 0,
                        dPosition = d.DPosition                         // ✅ pass to view
                    });
                }

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedDrivers = GetRequiredStaffCount(guestCount);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedDrivers = recommendedDrivers;
                ViewBag.AvailableDrivers = availableDriversRaw.Cast<dynamic>().ToList();
                ViewBag.BusyDrivers = busyDriversRaw.Cast<dynamic>().ToList();

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
        // ✅ FIX: RefId parsed as long
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

                var operatorId = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .Select(g => (int?)g.OperatorId)
                    .FirstOrDefaultAsync();

                var assignedNames = new List<string>();

                for (int i = 0; i < driversCount; i++)
                {
                    var driverRefId = driverRefIds[i];
                    var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                    if (driver == null) continue;

                    // ✅ FIX: long parse, fall back to DriverId if RefId won't fit int
                    long rfidLong = long.TryParse(driver.RefId, out long pLong) ? pLong : driver.DriverId;
                    int rfidValue = rfidLong <= int.MaxValue ? (int)rfidLong : driver.DriverId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = driverRefId,
                        Date = UnixNow(),
                        Passenger = assignedPassengers
                    });

                    _context.DriverDtrs.Add(new DriverDtr
                    {
                        Rfid = rfidValue,
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

                    // ✅ Update DPosition so driver moves to the bottom of the FIFO queue
                    driver.DPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
        // EDIT DRIVER ASSIGNMENT — update DriverDtr.Passenger
        // ✅ FIX: int.TryParse → long.TryParse for RefId validation
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

                // ✅ FIX: long.TryParse — RefId like 0005820000991 > int.MaxValue
                if (!long.TryParse(driverRefId, out long rfidLong))
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                // Use int cast if it fits, otherwise fall back to DriverId (same logic as AssignDrivers)
                var driverForEdit = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                int rfidInt = rfidLong > 0 && rfidLong <= int.MaxValue
                              ? (int)rfidLong
                              : (driverForEdit?.DriverId ?? 0);

                if (rfidInt == 0)
                    return Json(new { success = false, message = "Could not resolve driver record." });

                var dtr = await _context.DriverDtrs
                    .Where(d => d.Rfid == rfidInt
                             && string.Compare(d.Date, UnixTodayStart()) >= 0
                             && string.Compare(d.Date, UnixTodayEnd()) < 0)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (dtr == null)
                    return Json(new { success = false, message = "No active assignment found for this driver today." });

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
        // ✅ FIX: long.TryParse for RefId
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                // Remove DriverAttendance (uses RefId string — no parse needed)
                var attendances = await _context.DriverAttendances
                    .Where(a => a.DriverId == driverRefId
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any())
                    _context.DriverAttendances.RemoveRange(attendances);

                // Remove DriverDtr
                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                if (driver != null)
                {
                    // ✅ FIX: try long parse first, fall back to DriverId
                    long.TryParse(driverRefId, out long rfidLong);
                    int rfidInt = rfidLong > 0 && rfidLong <= int.MaxValue ? (int)rfidLong : driver.DriverId;

                    var dtrs = await _context.DriverDtrs
                        .Where(d => d.Rfid == rfidInt
                                 && string.Compare(d.Date, UnixTodayStart()) >= 0
                                 && string.Compare(d.Date, UnixTodayEnd()) < 0)
                        .ToListAsync();

                    if (dtrs.Any())
                        _context.DriverDtrs.RemoveRange(dtrs);

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
        // ✅ FIX: long.TryParse for RefId
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDriverAbsent(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                // ✅ Ensure DriverAttendance exists — uses string DriverId, no parse needed
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
                else
                {
                    attendance.Passenger = 0;
                }

                // ✅ FIX: long parse, use DriverId as Rfid when RefId > int.MaxValue
                var driverForAbsent = await _context.Drivers.FirstOrDefaultAsync(d => d.RefId == driverRefId);
                long.TryParse(driverRefId, out long rfidLong);
                int rfidInt = rfidLong > 0 && rfidLong <= int.MaxValue
                              ? (int)rfidLong
                              : (driverForAbsent?.DriverId ?? 0);

                if (rfidInt > 0)
                {
                    var existingDtrs = await _context.DriverDtrs
                        .Where(d => d.Rfid == rfidInt
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
        // CLEAR DRIVER — remove DriverAttendance only
        // ✅ Uses string DriverId — no parse needed
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
                    .Where(a => a.DriverId == driverRefId
                             && string.Compare(a.Date, UnixTodayStart()) >= 0
                             && string.Compare(a.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (attendances.Any())
                    _context.DriverAttendances.RemoveRange(attendances);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

      

        // =========================================================
        // GET ASSIGN OUTSIDE GUIDE MODAL
        // ✅ FIFO from tourguide_priority (MAX Date per guide = last assigned)
        // ✅ hasTrip / isAbsent / noOfGuest all from tourguide_priority today
        // ✅ Pulls guides from outside_guide_from_operator
        // ✅ Matches by OperatorId OR OperatorName (fallback)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignOutsideGuideModal(string batch)
        {
            try
            {
                var guest = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .FirstOrDefaultAsync();

                // ── Resolve operatorIdStr safely ────────────────────────────
                string operatorIdStr = guest?.OperatorId?.ToString()?.Trim() ?? "";

                string operatorName = "";
                if (!string.IsNullOrEmpty(operatorIdStr))
                {
                    if (int.TryParse(operatorIdStr, out int opIdInt))
                    {
                        var op = await _context.Operators.FindAsync(opIdInt);
                        operatorName = op?.BusinessName ?? op?.Name ?? "";
                    }
                    else
                    {
                        var op = await _context.Operators
                            .Where(o => o.Id.ToString() == operatorIdStr)
                            .FirstOrDefaultAsync();
                        operatorName = op?.BusinessName ?? op?.Name ?? "";
                    }
                }

                int guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                // ── Pull ALL guides from outside_guide_from_operator ─────────
                var outsideLinks = await _context.OutsideGuideFromOperators
                    .ToListAsync();

                if (!outsideLinks.Any())
                {
                    ViewBag.Batch = batch ?? "";
                    ViewBag.GuestCount = guestCount;
                    ViewBag.RecommendedGuides = recommendedGuides;
                    ViewBag.OperatorName = operatorName;
                    ViewBag.OperatorId = operatorIdStr;
                    ViewBag.AvailableGuides = new List<dynamic>();
                    ViewBag.BusyGuides = new List<dynamic>();
                    return PartialView("_AssignOutsideGuideModal");
                }

                // ── Helper: GuideId string -> int for tourguide_priority ────
                int GuideIdToInt(string guideId) =>
                    long.TryParse(guideId, out long l) && l > 0 && l <= int.MaxValue
                        ? (int)l : 0;

                // ── Fetch images from Guides table in one query ──────────────
                var rfidStrings = outsideLinks.Select(x => x.GuideId.Trim()).ToList();
                var guideImages = await _context.Guides
                    .Where(g => rfidStrings.Contains(g.Rfid))
                    .Select(g => new { g.Rfid, g.Image })
                    .ToListAsync();
                var imageMap = guideImages.ToDictionary(g => g.Rfid.Trim(), g => g.Image ?? "");

                // ── Build guide list from OutsideGuideFromOperator ──────────
                var allGuides = outsideLinks.Select(x => new
                {
                    Rfid = x.GuideId.Trim(),
                    fullName = x.GuideName,
                    Image = imageMap.TryGetValue(x.GuideId.Trim(), out var img) ? img : ""
                }).ToList();

                var guideRfidInts = allGuides
                    .Select(g => GuideIdToInt(g.Rfid))
                    .Where(k => k > 0).Distinct().ToList();

                // ── All tourguide_priority records for these guides ──────────
                var allPriorityRecords = await _context.TourGuidePriorities
                    .Where(p => guideRfidInts.Contains(p.GuideIdPrior))
                    .ToListAsync();

                // FIFO: MAX Date per guide across ALL time
                var lastPositionMap = allPriorityRecords
                    .GroupBy(p => p.GuideIdPrior)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Date ?? "0"));

                // ── Today's records only ─────────────────────────────────────
                var todayPriority = allPriorityRecords
                    .Where(p => string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToList();

                var assignedTodayRfidInts = todayPriority
                    .Select(p => p.GuideIdPrior)
                    .ToHashSet();

                var todayGuestMap = todayPriority
                    .GroupBy(p => p.GuideIdPrior)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.NoOfGuest));

                var absentRfidInts = todayPriority
                    .GroupBy(p => p.GuideIdPrior)
                    .Where(g => g.All(x => x.NoOfGuest == 0))
                    .Select(g => g.Key)
                    .ToHashSet();

                // ── FIFO order: never-assigned first, then oldest ────────────
                var orderedGuides = allGuides
                    .OrderBy(g => lastPositionMap.ContainsKey(GuideIdToInt(g.Rfid)) ? 1 : 0)
                    .ThenBy(g => lastPositionMap.TryGetValue(GuideIdToInt(g.Rfid), out var pos) ? pos : "0")
                    .ToList();

                var availableGuidesRaw = new List<object>();
                for (int i = 0; i < orderedGuides.Count; i++)
                {
                    var g = orderedGuides[i];
                    int rfidInt = GuideIdToInt(g.Rfid);
                    availableGuidesRaw.Add(new
                    {
                        g.Rfid,
                        g.fullName,
                        g.Image,
                        hasTrip = assignedTodayRfidInts.Contains(rfidInt),
                        isAbsent = absentRfidInts.Contains(rfidInt),
                        queuePosition = i + 1,
                        passengers = todayGuestMap.TryGetValue(rfidInt, out int p) ? p : 0
                    });
                }

                var assignedTodayList = orderedGuides
                    .Where(g => assignedTodayRfidInts.Contains(GuideIdToInt(g.Rfid)))
                    .ToList();

                var busyGuidesRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var g = assignedTodayList[i];
                    int rfidInt = GuideIdToInt(g.Rfid);
                    busyGuidesRaw.Add(new
                    {
                        g.Rfid,
                        g.fullName,
                        g.Image,
                        isAbsent = absentRfidInts.Contains(rfidInt),
                        queuePos = i + 1,
                        passengers = todayGuestMap.TryGetValue(rfidInt, out int p) ? p : 0
                    });
                }

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedGuides = recommendedGuides;
                ViewBag.OperatorName = operatorName;
                ViewBag.OperatorId = operatorIdStr;
                ViewBag.AvailableGuides = availableGuidesRaw.Cast<dynamic>().ToList();
                ViewBag.BusyGuides = busyGuidesRaw.Cast<dynamic>().ToList();

                return PartialView("_AssignOutsideGuideModal");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Content($@"
<div class='modal-header text-white' style='background:#6f42c1;'>
    <h5 class='modal-title'>Error Loading Outside Guides</h5>
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
        // ASSIGN OUTSIDE GUIDES — POST
        // ✅ Only inserts into tourguide_priority
        // ✅ One row per guide with distributed guest count
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignOutsideGuide(string batch, List<string> guideRfids)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one Guide are required." });

                int guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedGuides = GetRequiredStaffCount(guestCount);

                if (guideRfids.Count > recommendedGuides)
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot assign more than {recommendedGuides} guide(s) for {guestCount} guest(s)."
                    });

                int guidesCount = guideRfids.Count;
                int baseGuests = guidesCount > 0 ? guestCount / guidesCount : 0;
                int remainder = guidesCount > 0 ? guestCount % guidesCount : 0;

                // ── Fetch guide names from outside_guide_from_operator ───────
                var trimmedRfids = guideRfids.Select(r => r.Trim()).ToList();
                var outsideLinks = await _context.OutsideGuideFromOperators
                    .Where(x => trimmedRfids.Contains(x.GuideId.Trim()))
                    .ToListAsync();

                var nameMap = outsideLinks.ToDictionary(x => x.GuideId.Trim(), x => x.GuideName);
                var assignedNames = new List<string>();

                for (int i = 0; i < guidesCount; i++)
                {
                    var rfidStr = guideRfids[i].Trim();

                    if (!long.TryParse(rfidStr, out long rfidLong) || rfidLong <= 0 || rfidLong > int.MaxValue)
                        continue;

                    int rfidInt = (int)rfidLong;
                    int assignedGuests = baseGuests + (i == 0 ? remainder : 0);

                    _context.TourGuidePriorities.Add(new TourGuidePriority
                    {
                        GuideIdPrior = rfidInt,
                        Date = UnixNow(),
                        NoOfGuest = assignedGuests
                    });

                    assignedNames.Add(nameMap.TryGetValue(rfidStr, out var name) ? name : rfidStr);
                }

                await _context.SaveChangesAsync();

                var names = string.Join("; ", assignedNames);
                return Json(new { success = true, message = $"{guidesCount} outside guide(s) assigned: {names}" });
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
        // CLEAR GUIDE ASSIGNMENT
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

        // =========================================================
        // GET ASSIGN OUTSIDE DRIVER MODAL
        // ✅ FIFO from driver_priority (MAX Date per driver = last assigned)
        // ✅ hasTrip / isAbsent / Passenger all from driver_priority today
        // ✅ Pulls drivers from outside_driver_from_operator
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAssignOutsideDriverModal(string batch)
        {
            try
            {
                var guest = await _context.Guests
                    .Where(g => g.Batch == batch)
                    .FirstOrDefaultAsync();

                string operatorIdStr = guest?.OperatorId?.ToString()?.Trim() ?? "";

                string operatorName = "";
                if (!string.IsNullOrEmpty(operatorIdStr))
                {
                    if (int.TryParse(operatorIdStr, out int opIdInt))
                    {
                        var op = await _context.Operators.FindAsync(opIdInt);
                        operatorName = op?.BusinessName ?? op?.Name ?? "";
                    }
                    else
                    {
                        var op = await _context.Operators
                            .Where(o => o.Id.ToString() == operatorIdStr)
                            .FirstOrDefaultAsync();
                        operatorName = op?.BusinessName ?? op?.Name ?? "";
                    }
                }

                int guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int recommendedDrivers = GetRequiredStaffCount(guestCount);

                // ── Pull ALL drivers from outside_driver_from_operator ──────
                var outsideLinks = await _context.OutsideDriverFromOperators.ToListAsync();

                if (!outsideLinks.Any())
                {
                    ViewBag.Batch = batch ?? "";
                    ViewBag.GuestCount = guestCount;
                    ViewBag.RecommendedDrivers = recommendedDrivers;
                    ViewBag.OperatorName = operatorName;
                    ViewBag.OperatorId = operatorIdStr;
                    ViewBag.AvailableDrivers = new List<dynamic>();
                    ViewBag.BusyDrivers = new List<dynamic>();
                    return PartialView("_AssignOutsideDriverModal");
                }

                // ── Helper: DriverId string -> int for driver_priority ───────
                int DriverIdToInt(string driverId) =>
                    long.TryParse(driverId, out long l) && l > 0 && l <= int.MaxValue
                        ? (int)l : 0;

                // ── Fetch images from Drivers table in one query ─────────────
                var refIdStrings = outsideLinks.Select(x => x.DriverId.Trim()).ToList();
                var driverImages = await _context.Drivers
                    .Where(d => refIdStrings.Contains(d.RefId))
                    .Select(d => new { d.RefId, d.Image })
                    .ToListAsync();
                var imageMap = driverImages.ToDictionary(d => d.RefId.Trim(), d => d.Image ?? "");

                // ── Build driver list from OutsideDriverFromOperator ─────────
                var allDrivers = outsideLinks.Select(x => new
                {
                    RefId = x.DriverId.Trim(),
                    fullName = x.DriverName,
                    Image = imageMap.TryGetValue(x.DriverId.Trim(), out var img) ? img : ""
                }).ToList();

                var driverRefIdInts = allDrivers
                    .Select(d => DriverIdToInt(d.RefId))
                    .Where(k => k > 0).Distinct().ToList();

                // ── All driver_priority records for these drivers ─────────────
                var allPriorityRecords = await _context.DriverPriorities
                    .Where(p => driverRefIdInts.Contains(p.DriverIdPrior))
                    .ToListAsync();

                // FIFO: MAX Date per driver across ALL time
                var lastPositionMap = allPriorityRecords
                    .GroupBy(p => p.DriverIdPrior)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Date ?? "0"));

                // ── Today's records only ─────────────────────────────────────
                var todayPriority = allPriorityRecords
                    .Where(p => string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToList();

                var assignedTodayRefIdInts = todayPriority
                    .Select(p => p.DriverIdPrior)
                    .ToHashSet();

                // ✅ FIXED: Passenger (not NoOfPassenger)
                var todayPassengerMap = todayPriority
                    .GroupBy(p => p.DriverIdPrior)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Passenger));

                var absentRefIdInts = todayPriority
                    .GroupBy(p => p.DriverIdPrior)
                    .Where(g => g.All(x => x.Passenger == 0))
                    .Select(g => g.Key)
                    .ToHashSet();

                // ── FIFO order: never-assigned first, then oldest ─────────────
                var orderedDrivers = allDrivers
                    .OrderBy(d => lastPositionMap.ContainsKey(DriverIdToInt(d.RefId)) ? 1 : 0)
                    .ThenBy(d => lastPositionMap.TryGetValue(DriverIdToInt(d.RefId), out var pos) ? pos : "0")
                    .ToList();

                var availableDriversRaw = new List<object>();
                for (int i = 0; i < orderedDrivers.Count; i++)
                {
                    var d = orderedDrivers[i];
                    int refInt = DriverIdToInt(d.RefId);
                    availableDriversRaw.Add(new
                    {
                        d.RefId,
                        d.fullName,
                        d.Image,
                        hasTrip = assignedTodayRefIdInts.Contains(refInt),
                        isAbsent = absentRefIdInts.Contains(refInt),
                        queuePosition = i + 1,
                        passengers = todayPassengerMap.TryGetValue(refInt, out int p) ? p : 0
                    });
                }

                var assignedTodayList = orderedDrivers
                    .Where(d => assignedTodayRefIdInts.Contains(DriverIdToInt(d.RefId)))
                    .ToList();

                var busyDriversRaw = new List<object>();
                for (int i = 0; i < assignedTodayList.Count; i++)
                {
                    var d = assignedTodayList[i];
                    int refInt = DriverIdToInt(d.RefId);
                    busyDriversRaw.Add(new
                    {
                        d.RefId,
                        d.fullName,
                        d.Image,
                        isAbsent = absentRefIdInts.Contains(refInt),
                        queuePos = i + 1,
                        passengers = todayPassengerMap.TryGetValue(refInt, out int p) ? p : 0
                    });
                }

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.RecommendedDrivers = recommendedDrivers;
                ViewBag.OperatorName = operatorName;
                ViewBag.OperatorId = operatorIdStr;
                ViewBag.AvailableDrivers = availableDriversRaw.Cast<dynamic>().ToList();
                ViewBag.BusyDrivers = busyDriversRaw.Cast<dynamic>().ToList();

                return PartialView("_AssignOutsideDriverModal");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Content($@"
<div class='modal-header text-white' style='background:#0dcaf0;'>
    <h5 class='modal-title'>Error Loading Outside Drivers</h5>
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
        // ASSIGN OUTSIDE DRIVERS — POST
        // ✅ Only inserts into driver_priority
        // ✅ One row per driver with distributed passenger count
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignOutsideDriver(string batch, List<string> driverRefIds)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || driverRefIds == null || !driverRefIds.Any())
                    return Json(new { success = false, message = "Batch and at least one Driver are required." });

                int guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
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

                var trimmedRefIds = driverRefIds.Select(r => r.Trim()).ToList();
                var outsideLinks = await _context.OutsideDriverFromOperators
                    .Where(x => trimmedRefIds.Contains(x.DriverId.Trim()))
                    .ToListAsync();

                var nameMap = outsideLinks.ToDictionary(x => x.DriverId.Trim(), x => x.DriverName);
                var assignedNames = new List<string>();

                for (int i = 0; i < driversCount; i++)
                {
                    var refIdStr = driverRefIds[i].Trim();

                    if (!long.TryParse(refIdStr, out long refIdLong) || refIdLong <= 0 || refIdLong > int.MaxValue)
                        continue;

                    int refIdInt = (int)refIdLong;
                    int assignedPax = basePassengers + (i == 0 ? remainder : 0);

                    // ✅ FIXED: new DriverPriority (not new Driver), Passenger (not NoOfPassenger)
                    _context.DriverPriorities.Add(new DriverPriority
                    {
                        DriverIdPrior = refIdInt,
                        Date = UnixNow(),
                        Passenger = assignedPax
                    });

                    assignedNames.Add(nameMap.TryGetValue(refIdStr, out var name) ? name : refIdStr);
                }

                await _context.SaveChangesAsync();

                var names = string.Join("; ", assignedNames);
                return Json(new { success = true, message = $"{driversCount} outside driver(s) assigned: {names}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // EDIT OUTSIDE DRIVER ASSIGNMENT
        // ✅ Updates latest driver_priority.Passenger for today
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOutsideDriverAssignment(string driverRefId, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (passengers < 1)
                    return Json(new { success = false, message = "Passenger count must be at least 1." });

                if (!long.TryParse(driverRefId, out long refIdLong) || refIdLong <= 0 || refIdLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                int refIdInt = (int)refIdLong;

                var record = await _context.DriverPriorities
                    .Where(p => p.DriverIdPrior == refIdInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (record == null)
                    return Json(new { success = false, message = "No active assignment found for this driver today." });

                // ✅ FIXED: Passenger (not NoOfPassenger)
                record.Passenger = passengers;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Passenger count updated to {passengers}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // REMOVE OUTSIDE DRIVER ASSIGNMENT
        // ✅ Deletes all driver_priority records for this driver today
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveOutsideDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (!long.TryParse(driverRefId, out long refIdLong) || refIdLong <= 0 || refIdLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                int refIdInt = (int)refIdLong;

                var records = await _context.DriverPriorities
                    .Where(p => p.DriverIdPrior == refIdInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (records.Any())
                    _context.DriverPriorities.RemoveRange(records);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver assignment has been removed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // MARK OUTSIDE DRIVER ABSENT
        // ✅ Zeros out today's driver_priority.Passenger
        // ✅ Driver stays in FIFO rotation
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkOutsideDriverAbsent(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (!long.TryParse(driverRefId, out long refIdLong) || refIdLong <= 0 || refIdLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                int refIdInt = (int)refIdLong;
                var todayRecords = await _context.DriverPriorities
                    .Where(p => p.DriverIdPrior == refIdInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (todayRecords.Any())
                {
                    // ✅ FIXED: Passenger (not NoOfPassenger)
                    foreach (var r in todayRecords)
                        r.Passenger = 0;
                }
                else
                {
                    _context.DriverPriorities.Add(new DriverPriority
                    {
                        DriverIdPrior = refIdInt,
                        Date = UnixNow(),
                        Passenger = 0   // ✅ FIXED
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
        // CLEAR OUTSIDE DRIVER ASSIGNMENT
        // ✅ Deletes today's driver_priority rows
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearOutsideDriverAssignment(string driverRefId)
        {
            try
            {
                if (string.IsNullOrEmpty(driverRefId))
                    return Json(new { success = false, message = "Driver Ref ID is required." });

                if (!long.TryParse(driverRefId, out long refIdLong) || refIdLong <= 0 || refIdLong > int.MaxValue)
                    return Json(new { success = false, message = "Invalid driver Ref ID." });

                int refIdInt = (int)refIdLong;

                var records = await _context.DriverPriorities
                    .Where(p => p.DriverIdPrior == refIdInt
                             && string.Compare(p.Date, UnixTodayStart()) >= 0
                             && string.Compare(p.Date, UnixTodayEnd()) < 0)
                    .ToListAsync();

                if (records.Any())
                    _context.DriverPriorities.RemoveRange(records);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Driver is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }
}