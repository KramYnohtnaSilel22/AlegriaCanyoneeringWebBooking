
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using System.Text;
using AlegriaCanyoneeringWebBooking.Models;
namespace AlegriaCanyoneeringWebBooking.Controllers
{
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

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var todayUnix = ((DateTimeOffset)today).ToUnixTimeSeconds().ToString();
            var tomorrowUnix = ((DateTimeOffset)tomorrow).ToUnixTimeSeconds().ToString();

            var query = _context.Guests
                .AsNoTracking()
                .Include(g => g.NationalityEntity)
                .Where(g =>
                    g.BookingStatus == 3 &&
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
                return Content("<p class='text-danger'>No guests found for today.</p>", "text/html");

            var operatorIds = pagedGuests.Select(g => g.OperatorId).Distinct().ToList();
            var operators = await _context.Operators
                .Where(o => operatorIds.Contains(o.Id))
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            var vmList = pagedGuests.Select(g => new GuestWithOperatorVM
            {
                Guest = g,
                OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A"
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
                OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A"
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
                        .FirstOrDefault(o => o.Id == first.OperatorId)?.BusinessName ?? "N/A";

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

            DateTime? startDateValue = null;
            DateTime? endDateValue = null;
            if (DateTime.TryParse(startDate, out DateTime sd))
                startDateValue = sd.Date;
            if (DateTime.TryParse(endDate, out DateTime ed))
                endDateValue = ed.Date.AddDays(1).AddTicks(-1);

            // Query joined Guests + Operators with bookingStatus
            var query = from g in _context.Guests
                        join o in _context.Operators on g.OperatorId equals o.Id
                        where g.BookingStatus == 3
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

            var guestsList = await query.ToListAsync();

            // Apply date filtering in-memory on all filtered guests
            if (startDateValue.HasValue && endDateValue.HasValue)
            {
                guestsList = guestsList
                    .Where(x =>
                    {
                        if (string.IsNullOrEmpty(x.Guest.ArrivalDate) || !long.TryParse(x.Guest.ArrivalDate, out var unix))
                            return false;

                        var arrival = DateTimeOffset.FromUnixTimeSeconds(unix).DateTime;
                        return arrival >= startDateValue.Value && arrival <= endDateValue.Value;
                    })
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
                    arrivalDate = DateTimeOffset.FromUnixTimeSeconds(
                        long.Parse(g.First().Guest.ArrivalDate ?? "0")).DateTime.ToString("yyyy-MM-dd"),
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
                    guest.BookingStatus = 3; // Confirmed status
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
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> BookedGuest(string? BatchCode)
        //{
        //    // ✅ Get current user's info
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userRole = User.FindFirstValue(ClaimTypes.Role);

        //    int? currentOperatorId = null;
        //    if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
        //        currentOperatorId = parsedId;

        //    // ✅ If no batch code is provided, auto-generate starting at 10001
        //    if (string.IsNullOrEmpty(BatchCode))
        //    {
        //        var lastBatch = await _context.Guests
        //            .OrderByDescending(g => g.Id)
        //            .Select(g => g.Batch)
        //            .FirstOrDefaultAsync();

        //        int newBatchNumber = 10000; // base value

        //        if (int.TryParse(lastBatch, out int lastNum))
        //            newBatchNumber = lastNum + 1; // increment last batch
        //        else
        //            newBatchNumber = 10001; // first batch ever

        //        BatchCode = newBatchNumber.ToString();

        //        TempData["ToastMessage"] = $"⚠️ No batch code provided. Auto-generated batch code: {BatchCode}";
        //        TempData["ToastType"] = "warning";
        //    }

        //    // ✅ Find all guests with this batch that are not yet confirmed
        //    var guestsToFinalize = await _context.Guests
        //        .Where(g => g.Batch == BatchCode && g.BookingStatus != 3)
        //        .ToListAsync();

        //    if (guestsToFinalize.Any())
        //    {
        //        foreach (var guest in guestsToFinalize)
        //        {
        //            guest.BookingStatus = 3; // Confirmed
        //        }

        //        await _context.SaveChangesAsync();

        //        TempData["ToastMessage"] = $"✅ Successfully confirmed ";
        //        TempData["ToastType"] = "success";
        //    }
        //    else
        //    {
        //        // If no guests found, still mark confirmation success
        //        TempData["ToastMessage"] = $"✅  Confirmed successfully ";
        //        TempData["ToastType"] = "success";
        //    }

        //    // ✅ Optional: reload operators list
        //    var operators = await _context.Operators.ToListAsync();
        //    ViewBag.OperatorList = new SelectList(operators, "Id", "BusinessName");

        //    // ✅ Redirect to ReserveBooking with batchCode
        //    return RedirectToAction("ReserveBooking", new { batch = BatchCode });
        //}





    }

}