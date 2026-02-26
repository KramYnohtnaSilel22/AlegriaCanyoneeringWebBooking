
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
    [Authorize(Roles = "Super Admin,Admin,Operator")]
    public class ReserveController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;
        private readonly IGuestService _guestService;
        public ReserveController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<GuestController> logger, IGuestService guestService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _guestService = guestService;
            // Test connection
            if (!_context.Database.CanConnect())
            {
                throw new Exception("Cannot connect to database. Please check your connection string.");
            }
        }
        [HttpGet, HttpPost]
        public IActionResult PrintBatchGuests(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("Batch code is required.");

            // Get all guests in the batch + eager load Operators
            var rawGuests = _context.Guests
                .Include(g => g.Operators)
                .Where(g => g.Batch == batchCode)
                .ToList();

            // ✅ Map guests with QR = GuestID instead of RFIDCode
            var guests = rawGuests.Select(g => new
            {
                g.id, // include for clarity
                FullName = g.Fullname ?? "Unknown Guest",
                ArrivalDate = ParseUnixTimestamp(g.ArrivalDate),
                WristbandCode = g.RFIDCode, // keep showing RFID visually if needed
                                            // ✅ Generate QR using GuestID — ensures unique per guest
                QRBase64 = GenerateQRCodeBase64(g.id.ToString()),
                Operators = g.Operators?.BusinessName ?? "No Operators"
            }).ToList();

            if (!guests.Any())
                return NotFound("No guests found for this batch.");

            ViewBag.BatchCode = batchCode;

            return View("PrintBatchGuests", guests);
        }

        //[HttpGet, HttpPost]
        //public IActionResult PrintBatchGuests(string batchCode)
        //{
        //    if (string.IsNullOrEmpty(batchCode))
        //        return BadRequest("Batch code is required.");

        //    // Get all guests in the batch + eager load Operators
        //    var rawGuests = _context.Guests
        //        .Include(g => g.Operators)
        //        .Where(g => g.Batch == batchCode)
        //        .ToList();

        //    // Map guests with QR = RFIDCode
        //    var guests = rawGuests.Select(g => new
        //    {
        //        FullName = g.Fullname ?? "Unknown Guest",
        //        ArrivalDate = ParseUnixTimestamp(g.ArrivalDate),
        //        WristbandCode = g.RFIDCode, // use RFIDCode as unique QR
        //        QRBase64 = !string.IsNullOrEmpty(g.RFIDCode)
        //                   ? GenerateQRCodeBase64(g.RFIDCode)
        //                   : null, // fallback if missing
        //        Operators = g.Operators?.BusinessName ?? "No Operators"
        //    }).ToList();

        //    if (!guests.Any())
        //        return NotFound("No guests found for this batch.");

        //    ViewBag.BatchCode = batchCode;

        //    return View("PrintBatchGuests", guests);
        //}

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

            // ✅ Step 1: Try to get guest image
            var guestImage = _context.GuestImage
                .FirstOrDefault(i => i.WristbondGuestCode == guest.RFIDCode);

            byte[] imageBytes;
            string? imageBase64;

            if (guestImage != null && guestImage.Image?.Length > 0)
            {
                // Use stored guest image
                imageBytes = guestImage.Image;
                imageBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            }
            else
            {
                // ✅ Use default image when none exists
                string defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/default_guest.png");
                imageBytes = System.IO.File.Exists(defaultImagePath)
                    ? System.IO.File.ReadAllBytes(defaultImagePath)
                    : Array.Empty<byte>(); // fallback to empty array if file missing

                imageBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            }

            // ✅ Step 2: Generate padded wristband code
            //string wristBondCode = guest.RFIDCode.PadLeft(11, '0');
            string hex = guest.RFIDCode.Replace(" ", "");
            string firstPart = hex.Substring(0, 8); // first 4 bytes
            uint numericId = Convert.ToUInt32(firstPart, 16);
            string wristBondCode = numericId.ToString().PadLeft(11, '0');

            // ✅ Step 3: Check or create guest briefing
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
                    BGuestImage = imageBytes // ✅ always has valid bytes now
                };

                _context.GuestBriefings.Add(briefing);
                _context.SaveChanges();
            }
            // Convert Unix timestamp to DateTime
            DateTime arrivalDate;

            if (!string.IsNullOrEmpty(guest.ArrivalDate) && long.TryParse(guest.ArrivalDate, out long unix))
            {
                arrivalDate = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            }
            else
            {
                arrivalDate = DateTime.Now; // fallback
            }

            // Prepare view model
            var model = new GuestDetailsViewModel
            {
                FullName = guest.Fullname,
                ArrivalDate = arrivalDate,  // <-- THIS IS NOW CORRECT
                WristbandCode = wristBondCode,
                QRText = briefing.BDateCode,
                Operators = guest.Operators?.BusinessName ?? "No Operator",
                Age = guest.Age,
                Nationality = guest.NationalityEntity?.NatName ?? "Unknown",
                GuestImageBase64 = imageBase64
            };


            TempData["ToastMessage"] = $"Guest found successfully.";
            TempData["ToastType"] = "success";

            return View("ScanGuestInfo", model);
        }

        //[HttpGet]
        //public IActionResult ScanGuestInfo(string? qrCodeValue)
        //{
        //    if (string.IsNullOrEmpty(qrCodeValue))
        //        return View("ScanGuestInfo"); // show empty scan page

        //    // Parse GuestID from QR Code
        //    if (!int.TryParse(qrCodeValue, out int guestId))
        //        return BadRequest("Invalid QR code format. Must contain numeric GuestID.");

        //    // Step 1: Get guest by ID
        //    var guest = _context.Guests
        //        .Include(g => g.Operators)
        //        .Include(g => g.NationalityEntity)
        //        .FirstOrDefault(g => g.id == guestId);

        //    if (guest == null)
        //        return NotFound("Guest not found for this QR code.");

        //    // Step 2: Get guest image by RFID code (if exists)
        //    var guestImage = _context.GuestImage
        //        .FirstOrDefault(i => i.WristbondGuestCode == guest.RFIDCode);

        //    string? imageBase64 = guestImage != null
        //        ? $"data:image/png;base64,{Convert.ToBase64String(guestImage.Image)}"
        //        : null;

        //    // Step 3: Generate padded wristband code (e.g., "011233283")
        //    string wristBondCode = guest.RFIDCode.PadLeft(11, '0');

        //    // Step 4: Check if briefing already exists
        //    var briefing = _context.GuestBriefings
        //        .FirstOrDefault(b => b.BWristBondCode == wristBondCode && b.BGuestName == guest.Fullname);

        //    if (briefing == null)
        //    {
        //        byte[]? imageBytes = null;
        //        if (!string.IsNullOrEmpty(imageBase64) && imageBase64.Contains(','))
        //        {
        //            try
        //            {
        //                imageBytes = Convert.FromBase64String(imageBase64.Split(',')[1]);
        //            }
        //            catch
        //            {
        //                imageBytes = null;
        //            }
        //        }

        //        briefing = new GuestBriefing
        //        {
        //            BWristBondCode = wristBondCode,
        //            BGuestName = guest.Fullname,
        //            BDateArrival = DateTime.TryParse(guest.Date, out DateTime parsedDate)
        //                            ? parsedDate
        //                            : DateTime.Now,
        //            BDateDeparture = DateTime.Now,
        //            BDateCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        //            BGuestImage = imageBytes
        //        };

        //        _context.GuestBriefings.Add(briefing);
        //        _context.SaveChanges();
        //    }

        //    // Step 5: Prepare view model
        //    var model = new GuestDetailsViewModel
        //    {
        //        FullName = guest.Fullname,
        //        ArrivalDate = DateTime.TryParse(guest.Date, out DateTime parsedArrival)
        //                        ? parsedArrival
        //                        : DateTime.Now,
        //        WristbandCode = wristBondCode,
        //        QRText = briefing.BDateCode,
        //        Operators = guest.Operators?.BusinessName ?? "No Operator",
        //        Age = guest.Age,
        //        Nationality = guest.NationalityEntity?.NatName ?? "Unknown",
        //        GuestImageBase64 = imageBase64
        //    };

        //    return View("ScanGuestInfo", model);
        //}


        //[HttpGet]
        //public IActionResult ScanGuestInfo(string? rfidCode)
        //{
        //    if (string.IsNullOrEmpty(rfidCode))
        //        return View("ScanGuestInfo"); // show empty scan page

        //    // Convert RFID to numeric 11-digit wristband code
        //    long numericWristBondCode;

        //    if (!long.TryParse(rfidCode, out numericWristBondCode))
        //    {
        //        // Generate numeric representation from RFID characters
        //        numericWristBondCode = rfidCode
        //            .Select(c => (int)c)
        //            .Aggregate(0L, (acc, val) => (acc * 100 + val) % 10000000000L);
        //    }

        //    // Ensure it's exactly 11 digits
        //    string wristBondCode = numericWristBondCode.ToString("D11");

        //    // Get guest info including Operators, Nationality, and optional Image
        //    var guestInfo = (from g in _context.Guests
        //                     join img in _context.GuestImage
        //                         on wristBondCode equals img.WristbondGuestCode into gj
        //                     from subImg in gj.DefaultIfEmpty() // LEFT JOIN
        //                     where g.RFIDCode == rfidCode
        //                     select new
        //                     {
        //                         g.Fullname,
        //                         g.ArrivalDate,
        //                         g.RFIDCode,
        //                         g.Date,
        //                         g.Age,
        //                         OperatorName = g.Operators != null ? g.Operators.BusinessName : "No Operator",
        //                         NationalityName = g.NationalityEntity != null ? g.NationalityEntity.NatName : "Unknown",
        //                         ImageBase64 = subImg != null
        //                             ? $"data:image/png;base64,{Convert.ToBase64String(subImg.Image)}"
        //                             : null
        //                     }).FirstOrDefault();

        //    if (guestInfo == null)
        //        return NotFound("Guest not found for this RFID.");

        //    // Save to GuestBriefing if not exists
        //    var briefing = _context.GuestBriefings
        //        .FirstOrDefault(b => b.BWristBondCode == wristBondCode);

        //    if (briefing == null)
        //    {
        //        briefing = new GuestBriefing
        //        {
        //            BWristBondCode = wristBondCode,
        //            BGuestName = guestInfo.Fullname,
        //            BDateArrival = DateTime.TryParse(guestInfo.Date, out DateTime parsedDate)
        //                            ? parsedDate
        //                            : DateTime.Now,
        //            BDateDeparture = DateTime.Now,
        //            BDateCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        //            // store the image bytes in GuestBriefing if available
        //            // store the image bytes in GuestBriefing if available, otherwise empty byte array
        //            BGuestImage = guestInfo.ImageBase64 != null
        //    ? Convert.FromBase64String(guestInfo.ImageBase64.Split(',')[1])
        //    : new byte[0] // empty blob to satisfy NOT NULL
        //        };

        //        _context.GuestBriefings.Add(briefing);
        //        _context.SaveChanges();
        //    }

        //    var model = new GuestDetailsViewModel
        //    {
        //        FullName = guestInfo.Fullname,
        //        ArrivalDate = DateTime.TryParse(guestInfo.Date, out DateTime parsedArrival)
        //                        ? parsedArrival
        //                        : DateTime.Now,
        //        WristbandCode = wristBondCode,
        //        QRText = briefing.BDateCode,
        //        Operators = guestInfo.OperatorName,
        //        Age = guestInfo.Age,
        //        Nationality = guestInfo.NationalityName,
        //        GuestImageBase64 = guestInfo.ImageBase64
        //    };

        //    return View("ScanGuestInfo", model);
        //}


        private DateTime? ParseUnixTimestamp(string? unixTimestamp)
        {
            if (string.IsNullOrEmpty(unixTimestamp))
                return null;

            if (long.TryParse(unixTimestamp, out long seconds))
            {
                // Convert from Unix timestamp (seconds) to DateTimeOffset
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(seconds);
                // Convert to local time
                return dateTimeOffset.ToLocalTime().DateTime;
            }
            return null;
        }






        // GET: Reserve/Index (Original View)
        public IActionResult Index()
        {
            var viewModel = new GuestListViewModel
            {
                ReservedGuests = new List<Guest>() // Empty for AJAX loading
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyBatchExistence(string Batch)
        {
            try
            {
                _logger.LogInformation($"🔍 VERIFY BATCH REQUEST: '{Batch}'");

                if (string.IsNullOrEmpty(Batch))
                {
                    return Json(new { exists = false, error = "Batch code is required" });
                }

                // ✅ FIXED: Extract only the numbers from "BATCH-21471"
                string batchNumbers = Batch;

                // Remove "BATCH-" prefix if present
                if (Batch.StartsWith("BATCH-"))
                {
                    batchNumbers = Batch.Substring(6); // Get only "21471"
                    _logger.LogInformation($"🔧 Extracted batch numbers: '{batchNumbers}'");
                }

                _logger.LogInformation($"🔍 SEARCHING FOR BATCH IN DB: '{batchNumbers}'");

                // ✅ FIXED: Search using only the numbers (21471)
                var batchExists = await _context.Guests
                    .AnyAsync(g => g.Batch == batchNumbers && g.BookingStatus == 2);

                _logger.LogInformation($"✅ BATCH EXISTS: {batchExists} for '{batchNumbers}'");

                return Json(new { exists = batchExists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 ERROR verifying batch {Batch}");
                return Json(new { exists = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBatchDetails(string Batch)
        {
            try
            {
                _logger.LogInformation($"🔍 GET BATCH DETAILS: '{Batch}'");

                if (string.IsNullOrEmpty(Batch))
                {
                    return Json(new { success = false, message = "Batch code is required" });
                }

                // ✅ FIXED: Extract only the numbers from "BATCH-21471"
                string batchNumbers = Batch;

                if (Batch.StartsWith("BATCH-"))
                {
                    batchNumbers = Batch.Substring(6); // Get only "21471"
                    _logger.LogInformation($"🔧 Extracted batch numbers: '{batchNumbers}'");
                }

                // ✅ FIXED: Search using only the numbers (21471)
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
                {
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            operatorName = batchDetails.operatorName,
                            totalGuests = batchDetails.totalGuests
                        }
                    });
                }

                return Json(new { success = false, message = "Batch not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 ERROR getting batch details for {Batch}");
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

                // Get current user info
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                int? currentOperatorId = null;
                if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                    currentOperatorId = parsedId;

                // Base query for reserved guests (status = 2)
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

                // Filter by operator role if applicable
                if (currentOperatorId.HasValue)
                {
                    query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(x =>
                        x.Guest.Fullname.ToLower().Contains(searchValue) ||
                        x.OperatorName.ToLower().Contains(searchValue) ||
                        x.Guest.Batch.ToLower().Contains(searchValue));
                }

                // ✅ FIXED: Execute operations sequentially
                var recordsTotal = await query.CountAsync();

                var totalBatchesQuery = query
                    .GroupBy(x => new { x.Guest.Batch, x.Guest.OperatorId })
                    .Select(grp => grp.Key);

                var recordsFiltered = await totalBatchesQuery.CountAsync();

                // Get paginated data
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

                // ✅ FIXED: Sequential processing
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

                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsFiltered,
                    recordsTotal = recordsTotal,
                    data = result
                });
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
        // ✅ Keep this method synchronous
        private string GenerateQrCode(string batchCode)
        {
            try
            {
                if (string.IsNullOrEmpty(batchCode)) return "";
                string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=BATCH-{Uri.EscapeDataString(batchCode)}&format=png";
                return qrUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QR Generation Error: {ex.Message}");
                return "";
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGuestOfTheDay(int pageNumber = 1, int pageSize = 50, string batchFilter = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            var today = DateTime.Today; // local date
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
                    string.Compare(g.ArrivalDate, tomorrowUnix) < 0
                );

            if (currentOperatorId.HasValue)
            {
                query = query.Where(g => g.OperatorId == currentOperatorId.Value);
            }

            if (!string.IsNullOrEmpty(batchFilter))
            {
                query = query.Where(g => g.Batch.Contains(batchFilter));
            }

            var totalGuests = await query.CountAsync();

            var pagedGuests = await query
                .OrderByDescending(g => g.ArrivalDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (!pagedGuests.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "No guest arrivals found today."
                });
            }



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


        // THIS IS MY OLD METHOD


        //[HttpGet]
        //public async Task<IActionResult> GetGuestOfTheDay(int pageNumber = 1, int pageSize = 50, string batchFilter = null)
        //{
        //    // ✅ Get current user's info
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userRole = User.FindFirstValue(ClaimTypes.Role);

        //    int? currentOperatorId = null;
        //    if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
        //        currentOperatorId = parsedId;

        //    // ✅ Define today's date range
        //    var today = DateTime.UtcNow.Date;
        //    var tomorrow = today.AddDays(1);

        //    // ✅ Base query (SQL-level filters only)
        //    var query = _context.Guests
        //        .Include(g => g.NationalityEntity)
        //        .Where(g => g.BookingStatus == 3 && !string.IsNullOrEmpty(g.ArrivalDate));

        //    // ✅ Filter by operator if applicable
        //    if (currentOperatorId.HasValue)
        //    {
        //        query = query.Where(g => g.OperatorId == currentOperatorId.Value);
        //    }

        //    // ✅ Optional batch filter
        //    if (!string.IsNullOrEmpty(batchFilter))
        //    {
        //        query = query.Where(g => g.Batch.Contains(batchFilter));
        //    }

        //    // ✅ Execute SQL first, then filter in-memory by today's date
        //    var allGuests = await query
        //        .AsNoTracking()
        //        .OrderByDescending(g => g.ArrivalDate)
        //        .ToListAsync();

        //    // ✅ Convert Unix timestamps & filter for today
        //    var todayGuests = allGuests
        //        .Where(g =>
        //        {
        //            if (long.TryParse(g.ArrivalDate, out var unix))
        //            {
        //                var arrival = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        //                return arrival >= today && arrival < tomorrow;
        //            }
        //            return false;
        //        })
        //        .ToList();

        //    // ✅ Pagination
        //    var totalGuests = todayGuests.Count;
        //    var pagedGuests = todayGuests
        //        .Skip((pageNumber - 1) * pageSize)
        //        .Take(pageSize)
        //        .ToList();

        //    if (!pagedGuests.Any())
        //        return Content("<p class='text-danger'>No guests found for today.</p>", "text/html");

        //    // ✅ Load operator names efficiently
        //    var operatorIds = pagedGuests.Select(g => g.OperatorId).Distinct().ToList();
        //    var operators = await _context.Operators
        //        .Where(o => operatorIds.Contains(o.Id))
        //        .Select(o => new { o.Id, o.BusinessName })
        //        .ToListAsync();

        //    // ✅ Map to ViewModel
        //    var vmList = pagedGuests.Select(g => new GuestWithOperatorVM
        //    {
        //        Guest = g,
        //        OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A"
        //    }).ToList();

        //    // ✅ Build model for partial
        //    var model = new GuestPaginationViewModel
        //    {
        //        Guests = vmList,
        //        CurrentPage = pageNumber,
        //        PageSize = pageSize,
        //        TotalCount = totalGuests,
        //        TotalPages = (int)Math.Ceiling(totalGuests / (double)pageSize),
        //        BatchFilter = batchFilter
        //    };

        //    return PartialView("_GuestDetailsPartial", model);
        //}




        [HttpGet]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("Batch code is required.");

            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            var guests = await _context.Guests
                      .Include(g => g.NationalityEntity) // Include Nationality
                .Where(g => g.Batch == batchCode)
                .ToListAsync();

            var guestsWithOperatorName = guests.Select(g => new GuestWithOperatorVM
            {
                Guest = g,
                OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "No Operator"
            }).ToList();

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


        // ---------- QR Helpers ----------

        private string GenerateQRText(Guest guest)
        {
            return $"Batch        : {guest.Batch}";
        }
        private string GenerateQRCodeBase64(string data)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(20);
            return "data:image/png;base64," + Convert.ToBase64String(qrBytes);
        }


   




        // GET: FinalBookingBatch
        public IActionResult BookedGuest()
        {
            return View();
        }



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

            long? startUnix = null;
            long? endUnix = null;
            if (DateTime.TryParse(startDate, out DateTime sd))
                startUnix = new DateTimeOffset(sd.Date).ToUnixTimeSeconds();
            if (DateTime.TryParse(endDate, out DateTime ed))
                endUnix = new DateTimeOffset(ed.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();

            // Query joined Guests + Operators with bookingStatus
            var query = from g in _context.Guests
                        join o in _context.Operators on g.OperatorId equals o.Id
                        where g.BookingStatus == 0
                        select new
                        {
                            Guest = g,
                            OperatorName = o.BusinessName
                        };

            if (currentOperatorId.HasValue)
                query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(x =>
                    x.Guest.Fullname.Contains(search) ||
                    x.Guest.Batch.Contains(search) ||
                    x.OperatorName.Contains(search));

            // Filter DB first for non-null ArrivalDate
            if (startUnix.HasValue && endUnix.HasValue)
            {
                query = query.Where(x => !string.IsNullOrEmpty(x.Guest.ArrivalDate));
            }

            var guestsList = await query.ToListAsync();

            // Apply date filter safely in-memory
            if (startUnix.HasValue && endUnix.HasValue)
            {
                guestsList = guestsList
                    .Where(x =>
                        long.TryParse(x.Guest.ArrivalDate, out var unix) &&
                        unix >= startUnix.Value &&
                        unix <= endUnix.Value
                    )
                    .ToList();
            }


            // Group data after filtering
            var groupedData = guestsList
                .GroupBy(x => new { x.Guest.Batch, x.OperatorName })
                .Select(g => new
                {
                    batch = g.Key.Batch,
                    operatorName = g.Key.OperatorName,
                    totalGuests = g.Count(),
                    arrivalDate = g.First().Guest.ArrivalDate, // Use stored Unix timestamp
                    status = "Confirmed"
                })
                .ToList();

            // Pagination after grouping
            var pagedData = groupedData.Skip(start).Take(length);

            return Json(new
            {
                draw,
                recordsTotal = groupedData.Count,
                recordsFiltered = groupedData.Count,
                data = pagedData
            });
        }






        //THIS IS MY OLD METHOD 
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> GetGuestsData(string? startDate, string? endDate)
        //{
        //    var draw = Request.Form["draw"].FirstOrDefault();
        //    var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
        //    var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
        //    var search = Request.Form["search[value]"].FirstOrDefault();

        //    // ✅ Get current user's ID and Role
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userRole = User.FindFirstValue(ClaimTypes.Role);

        //    int? currentOperatorId = null;
        //    if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
        //    {
        //        currentOperatorId = parsedId;
        //    }

        //    // 🔹 Convert date filters
        //    DateTime? startDateValue = null;
        //    DateTime? endDateValue = null;
        //    if (DateTime.TryParse(startDate, out DateTime sd))
        //        startDateValue = sd.Date;
        //    if (DateTime.TryParse(endDate, out DateTime ed))
        //        endDateValue = ed.Date.AddDays(1).AddTicks(-1);

        //    // 🔹 Query with join to ensure operator names are always available
        //    var query = from g in _context.Guests
        //                join o in _context.Operators on g.OperatorId equals o.Id
        //                where g.BookingStatus == 3
        //                select new
        //                {
        //                    Guest = g,
        //                    OperatorName = o.BusinessName
        //                };

        //    // ✅ Filter by operator role
        //    if (currentOperatorId.HasValue)
        //    {
        //        query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);
        //    }

        //    // 🔹 Search filter
        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        query = query.Where(x =>
        //            x.Guest.Fullname.Contains(search) ||
        //            x.Guest.Batch.Contains(search) ||
        //            x.OperatorName.Contains(search));
        //    }

        //    var guestsList = await query.ToListAsync();

        //    // 🔹 Date filter in-memory
        //    if (startDateValue.HasValue && endDateValue.HasValue)
        //    {
        //        guestsList = guestsList
        //            .Where(x =>
        //            {
        //                if (string.IsNullOrEmpty(x.Guest.ArrivalDate) || !long.TryParse(x.Guest.ArrivalDate, out var unix))
        //                    return false;

        //                var arrival = DateTimeOffset.FromUnixTimeSeconds(unix).DateTime;
        //                return arrival >= startDateValue.Value && arrival <= endDateValue.Value;
        //            })
        //            .ToList();
        //    }

        //    // 🔹 Group by Batch
        //    var groupedData = guestsList
        //        .GroupBy(x => new { x.Guest.Batch, x.OperatorName })
        //        .Select(g => new
        //        {
        //            batch = g.Key.Batch,
        //            operatorName = g.Key.OperatorName,
        //            totalGuests = g.Count(),
        //            arrivalDate = DateTimeOffset.FromUnixTimeSeconds(
        //                long.Parse(g.First().Guest.ArrivalDate ?? "0")).DateTime.ToString("yyyy-MM-dd"),
        //            status = "Confirmed"
        //        })
        //        .ToList();

        //    // 🔹 Apply pagination
        //    var pagedData = groupedData.Skip(start).Take(length);

        //    return Json(new
        //    {
        //        draw,
        //        recordsTotal = groupedData.Count,
        //        recordsFiltered = groupedData.Count,
        //        data = pagedData
        //    });
        //}

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
                    return Content(@"
                <div class='text-center p-4'>
                    <i class='fas fa-exclamation-triangle fa-2x text-warning mb-3'></i>
                    <h5 class='text-warning'>Guest Not Found</h5>
                    <p class='text-muted'>The requested guest could not be found.</p>
                </div>", "text/html");

                var guestsInBatch = await _context.Guests
                    .Where(g => g.Batch == guest.Batch && g.Id != guest.Id && g.BookingStatus != 1) // Exclude canceled guests
                    .Include(g => g.NationalityEntity)
                    .ToListAsync();

                var vm = new GuestDetailsViewModel
                {
                    Guest = guest,
                    GuestsInBatch = guestsInBatch
                };

                return PartialView("_ReserveBookingDetailsPartial", vm);
            }
            catch (Exception ex)
            {
                return Content(@"
            <div class='text-center p-4'>
                <i class='fas fa-exclamation-triangle fa-2x text-danger mb-3'></i>
                <h5 class='text-danger'>Error Loading Details</h5>
                <p class='text-muted'>An error occurred while loading guest details.</p>
                <p class='text-muted small'>Error: " + ex.Message + @"</p>
            </div>", "text/html");
            }
        }


        [HttpPost]
        [Authorize(Roles = "Super Admin")]
        public async Task<IActionResult> BookedGuest(string BatchCode)
        {
            try
            {
                // ✅ Get current user's info
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                int? currentOperatorId = null;
                if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                    currentOperatorId = parsedId;

                // ✅ If no batch code is provided, auto-generate starting at 10001
                if (string.IsNullOrEmpty(BatchCode))
                {
                    var lastBatch = await _context.Guests
                        .OrderByDescending(g => g.Id)
                        .Select(g => g.Batch)
                        .FirstOrDefaultAsync();

                    int newBatchNumber = 10000; // base value

                    if (int.TryParse(lastBatch, out int lastNum))
                        newBatchNumber = lastNum + 1; // increment last batch
                    else
                        newBatchNumber = 10001; // first batch ever

                    BatchCode = newBatchNumber.ToString();

                    return Json(new
                    {
                        success = false,
                        message = $"No batch code provided. Auto-generated batch code: {BatchCode}. Please scan a valid QR code."
                    });
                }

                // ✅ Find all guests with this batch that are reserved (status = 2)
                var guestsToFinalize = await _context.Guests
                    .Where(g => g.Batch == BatchCode && g.BookingStatus == 2) // Only reserved guests
                    .ToListAsync();

                if (!guestsToFinalize.Any())
                {
                    return Json(new { success = false, message = "No reserved guests found for this batch." });
                }

                // ✅ Optional: Filter by operator role if applicable
                if (currentOperatorId.HasValue)
                {
                    // Check if the operator has permission to confirm this batch
                    var operatorGuests = guestsToFinalize.Where(g => g.OperatorId == currentOperatorId.Value).ToList();
                    if (!operatorGuests.Any())
                    {
                        return Json(new
                        {
                            success = false,
                            message = "You don't have permission to confirm this batch. This batch belongs to another operator."
                        });
                    }
                }

                foreach (var guest in guestsToFinalize)
                {
                    guest.BookingStatus = 0; // Confirmed status
                                             // You can add additional fields like confirmation date if needed
                                             // guest.ConfirmedDate = DateTime.Now;
                                             // guest.ConfirmedBy = userId; // Track who confirmed the booking
                }

                await _context.SaveChangesAsync();

                // ✅ Log the confirmation (optional)
                Console.WriteLine($"Batch {BatchCode} confirmed by user {userId} (Role: {userRole})");

                return Json(new
                {
                    success = true,
                    message = $"Successfully confirmed"
                });
            }
            catch (Exception ex)
            {
                // Log the actual error
                Console.WriteLine($"Error in BookedGuest: {ex.Message}");
                return Json(new { success = false, message = "Error confirming booking. Please try again." });
            }
        }


        // =========================================================
        // ASSIGN DRIVER — GET modal form
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

                var today = DateTime.Now.ToString("yyyy-MM-dd");

                // ✅ LIFO — newest assigned = last in cycle (bottom of list)
                var priorityToday = await _context.DriverIdPriors
                    .Where(p => p.Date == today)
                    .OrderByDescending(p => p.Id)
                    .ToListAsync();

                // ✅ Build busyRefIds in LIFO order
                var busyRefIds = new List<string>();
                foreach (var p in priorityToday)
                {
                    var matched = allDrivers.FirstOrDefault(d =>
                        int.TryParse(d.RefId, out int rid) && rid == p.DriverIdPriorValue);
                    if (matched != null && !busyRefIds.Contains(matched.RefId))
                        busyRefIds.Add(matched.RefId);
                }

                // ✅ Pull ALL attendance today in-memory
                var allAttendanceToday = await _context.DriverAttendances
                    .Where(a => a.Date == today)
                    .ToListAsync();

                // ✅ Map RefId → total passengers
                var passengerMap = allAttendanceToday
                    .Where(a => !string.IsNullOrEmpty(a.DriverId))
                    .GroupBy(a => a.DriverId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Passenger));

                // ✅ ALL drivers in Available — free first, busy at bottom
                var freeDrivers = allDrivers
                    .Where(d => !busyRefIds.Contains(d.RefId))
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        isBusy = false,
                        passengers = 0
                    })
                    .ToList();

                // ✅ Busy drivers at bottom — LIFO order (newest assigned = bottom-most)
                var busyDriversForAvailable = allDrivers
                    .Where(d => busyRefIds.Contains(d.RefId))
                    .OrderByDescending(d => busyRefIds.IndexOf(d.RefId)) // newest = last
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        isBusy = true,
                        passengers = passengerMap.ContainsKey(d.RefId)
                                     ? passengerMap[d.RefId]
                                     : 0
                    })
                    .ToList();

                // ✅ Combine: free on top, busy at bottom
                var availableDrivers = freeDrivers
                    .Cast<dynamic>()
                    .Concat(busyDriversForAvailable.Cast<dynamic>())
                    .ToList();

                // ✅ Busy tab still shows busy drivers (LIFO — newest = #1)
                var busyDriversList = allDrivers
                    .Where(d => busyRefIds.Contains(d.RefId))
                    .OrderBy(d => busyRefIds.IndexOf(d.RefId))
                    .Select(d => new
                    {
                        d.DriverId,
                        d.RefId,
                        d.fullName,
                        Image = d.Image ?? "",
                        passengers = passengerMap.ContainsKey(d.RefId)
                                     ? passengerMap[d.RefId]
                                     : 0
                    })
                    .ToList();

                var guestCount = await _context.Guests
                    .CountAsync(g => g.Batch == batch);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.AvailableDrivers = availableDrivers;
                ViewBag.BusyDrivers = busyDriversList.Cast<dynamic>().ToList();

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
                <div class='alert alert-danger'>
                    <i class='fas fa-exclamation-triangle me-2'></i>
                    <strong>Error:</strong> {inner}
                </div>
            </div>
            <div class='modal-footer'>
                <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
            </div>", "text/html");
            }
        }

        // =========================================================
        // ASSIGN MULTIPLE DRIVERS — POST save
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDrivers(string batch, List<string> driverRefIds)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || driverRefIds == null || !driverRefIds.Any())
                    return Json(new { success = false, message = "Batch and at least one Driver are required." });

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var nowFull = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int driversCount = driverRefIds.Count;
                int basePassengers = guestCount / driversCount;
                int remainder = guestCount % driversCount;

                var assignedNames = new List<string>();

                for (int i = 0; i < driversCount; i++)
                {
                    var driverRefId = driverRefIds[i];

                    var driver = await _context.Drivers
                        .FirstOrDefaultAsync(d => d.RefId == driverRefId);

                    if (driver == null) continue;

                    int rfidValue = int.TryParse(driver.RefId, out int parsed) ? parsed : driver.DriverId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    // ✅ Always add new attendance record (allows re-assignment / cycling)
                    _context.DriverAttendances.Add(new DriverAttendance
                    {
                        DriverId = driverRefId,
                        Date = today,
                        Passenger = assignedPassengers
                    });

                    _context.DriverDtrs.Add(new DriverDtr
                    {
                        Rfid = rfidValue,
                        Date = today,
                        Passenger = assignedPassengers.ToString(),
                        ComDateDr = nowFull
                    });

                    // ✅ Add to priority — pushes driver down the cycle list
                    _context.DriverIdPriors.Add(new DriverIdPrior
                    {
                        DriverIdPriorValue = rfidValue,
                        Date = today,
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
        // CLEAR DRIVER — optional, kept for manual override
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearDriverAssignment(string driverRefId)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");

                var allAttendanceToday = await _context.DriverAttendances
                    .Where(a => a.Date == today)
                    .ToListAsync();

                var toRemove = allAttendanceToday
                    .Where(a => a.DriverId == driverRefId)
                    .ToList();

                if (toRemove.Any())
                    _context.DriverAttendances.RemoveRange(toRemove);

                int rfidValue = int.TryParse(driverRefId, out int rid) ? rid : 0;

                // ✅ Remove ALL priority entries for this driver today (full reset)
                var allPriority = await _context.DriverIdPriors
                    .Where(p => p.Date == today && p.DriverIdPriorValue == rfidValue)
                    .ToListAsync();

                if (allPriority.Any())
                    _context.DriverIdPriors.RemoveRange(allPriority);

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Driver is now available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // =========================================================
        // GET assigned batches
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
        // ASSIGN GUIDE — GET modal form
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

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                // ✅ LIFO — newest assigned shows last (bottom of cycle)
                var attendanceToday = await _context.TourGuideAttendances
                    .Where(a => a.Date == today)
                    .OrderByDescending(a => a.Id)
                    .ToListAsync();

                var busyRfids = new List<string>();
                foreach (var a in attendanceToday)
                {
                    if (!string.IsNullOrEmpty(a.TGId) && !busyRfids.Contains(a.TGId))
                        busyRfids.Add(a.TGId);
                }

                // ✅ Passenger map from tourguide_dtr
                var guideDtrToday = await _context.TourGuideDtrs
                    .Where(d => d.Date == todayLong)
                    .ToListAsync();

                var guidePassengerMap = guideDtrToday
                    .GroupBy(d => d.Rfid)
                    .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

                // ✅ Free guides on top
                var freeGuides = allGuides
                    .Where(g => !busyRfids.Contains(g.Rfid))
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        g.fullName,
                        Image = g.Image ?? "",
                        isBusy = false,
                        passengers = 0
                    })
                    .ToList();

                // ✅ Busy guides at bottom — LIFO (newest = bottom-most)
                var busyGuidesForAvailable = allGuides
                    .Where(g => busyRfids.Contains(g.Rfid))
                    .OrderByDescending(g => busyRfids.IndexOf(g.Rfid))
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        g.fullName,
                        Image = g.Image ?? "",
                        isBusy = true,
                        passengers = long.TryParse(g.Rfid, out long rfidLong) && guidePassengerMap.ContainsKey(rfidLong)
                                     ? guidePassengerMap[rfidLong]
                                     : 0
                    })
                    .ToList();

                // ✅ Combine: free on top, busy at bottom
                var availableGuides = freeGuides
                    .Cast<dynamic>()
                    .Concat(busyGuidesForAvailable.Cast<dynamic>())
                    .ToList();

                // ✅ Busy tab — info only (LIFO order)
                var busyGuidesList = allGuides
                    .Where(g => busyRfids.Contains(g.Rfid))
                    .OrderBy(g => busyRfids.IndexOf(g.Rfid))
                    .Select(g => new
                    {
                        g.GuideId,
                        g.Rfid,
                        g.fullName,
                        Image = g.Image ?? "",
                        passengers = long.TryParse(g.Rfid, out long rfidLong2) && guidePassengerMap.ContainsKey(rfidLong2)
                                     ? guidePassengerMap[rfidLong2]
                                     : 0
                    })
                    .ToList();

                var guestCount = await _context.Guests
                    .CountAsync(g => g.Batch == batch);

                ViewBag.Batch = batch ?? "";
                ViewBag.GuestCount = guestCount;
                ViewBag.AvailableGuides = availableGuides;
                ViewBag.BusyGuides = busyGuidesList.Cast<dynamic>().ToList();

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
                <div class='alert alert-danger'>
                    <strong>Error:</strong> {inner}
                </div>
            </div>
            <div class='modal-footer'>
                <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
            </div>", "text/html");
            }
        }

        // =========================================================
        // ASSIGN MULTIPLE GUIDES — POST save (no conflict block)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignGuides(string batch, List<string> guideRfids)
        {
            try
            {
                if (string.IsNullOrEmpty(batch) || guideRfids == null || !guideRfids.Any())
                    return Json(new { success = false, message = "Batch and at least one Guide are required." });

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var nowFull = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                long todayLong = long.Parse(DateTime.Now.ToString("yyyyMMdd"));

                var guestCount = await _context.Guests.CountAsync(g => g.Batch == batch);
                int guidesCount = guideRfids.Count;
                int basePassengers = guestCount / guidesCount;
                int remainder = guestCount % guidesCount;

                var assignedNames = new List<string>();

                for (int i = 0; i < guidesCount; i++)
                {
                    var guideRfid = guideRfids[i];

                    var guide = await _context.Guides
                        .FirstOrDefaultAsync(g => g.Rfid == guideRfid);

                    if (guide == null) continue;

                    long rfidLong = long.TryParse(guide.Rfid, out long parsed) ? parsed : guide.GuideId;
                    int assignedPassengers = basePassengers + (i == 0 ? remainder : 0);

                    // ✅ Always add — allows re-assignment / cycling
                    _context.TourGuideAttendances.Add(new TourGuideAttendance
                    {
                        TGId = guide.Rfid,
                        Date = today,
                        Rfid = guide.Rfid
                    });

                    _context.TourGuideDtrs.Add(new TourGuideDtr
                    {
                        Rfid = rfidLong,
                        Date = todayLong,
                        NoOfGuest = assignedPassengers.ToString(),
                        ComDate = nowFull
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
        // CLEAR GUIDE — optional manual reset
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearGuideAssignment(string guideRfid)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");

                // ✅ Remove ALL attendance entries for this guide today (full reset)
                var toRemove = await _context.TourGuideAttendances
                    .Where(a => a.Date == today && a.TGId == guideRfid)
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
    }

}