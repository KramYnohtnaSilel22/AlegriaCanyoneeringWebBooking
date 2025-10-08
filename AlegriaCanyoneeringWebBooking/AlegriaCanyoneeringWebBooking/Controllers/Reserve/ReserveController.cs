
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.Service;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using System.Text;

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


        [HttpGet]
        public async Task<IActionResult> GetBookingDetails(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.NationalityEntity)
                .Include(g => g.OperatorList) // optional if you use operator in partial
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
                return Content("<p class='text-danger'>Guest not found.</p>", "text/html");

            var guestsInBatch = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.Id != guest.Id)
                .Include(g => g.NationalityEntity)
                .ToListAsync();

            var vm = new GuestDetailsViewModel
            {
                Guest = guest,
                GuestsInBatch = guestsInBatch
            };

            // Ensure the partial name matches the file you created:
            return PartialView("_ReserveBookingDetailsPartial", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetGuestOfTheDay()
        {
            // 🟢 Fetch all guests ordered by ArrivalDate (latest first)
            var guests = await _context.Guests
                .Include(g => g.NationalityEntity)
                .OrderByDescending(g => g.ArrivalDate)
                .ToListAsync();

            if (guests == null || !guests.Any())
            {
                return Content("<p class='text-danger'>No guests found.</p>", "text/html");
            }

            // 🟢 Fetch all operators once (avoid N+1 queries)
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            // 🟢 Build a list of GuestWithOperatorVM
            var vmList = guests.Select(g => new GuestWithOperatorVM
            {
                Guest = g,
                OperatorName = operators.FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A"
            }).ToList();

            // ✅ Return all guests to the partial view
            return PartialView("_GuestDetailsPartial", vmList);
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

        // 2. Load reserved guests
        var reservedGuestsQuery = _context.Guests
            .Include(g => g.NationalityEntity)
            .Where(g => g.BookingStatus == "reserved");

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

                    RFID = grp.Count(x => x.BookingStatus != "canceled"),
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

    //[HttpGet]
    //public async Task<IActionResult> GetGuestOfTheDay()
    //{
    //    var guest = await _context.Guests
    //        .Include(g => g.NationalityEntity)
    //        .OrderByDescending(g => g.ArrivalDate)
    //        .FirstOrDefaultAsync();

    //    if (guest == null)
    //    {
    //        return Content("<p class='text-danger'>No guest of the day found.</p>", "text/html");
    //    }

    //    // 🟢 Use same working operator lookup pattern
    //    var operators = await _context.Operators
    //        .Select(o => new { o.Id, o.BusinessName })
    //        .ToListAsync();

    //    var operatorName = operators
    //        .FirstOrDefault(o => o.Id == guest.OperatorId)?.BusinessName ?? "N/A";

    //    var vm = new GuestWithOperatorVM
    //    {
    //        Guest = guest,
    //        OperatorName = operatorName
    //    };

    //    return PartialView("_GuestDetailsPartial", new List<GuestWithOperatorVM> { vm });
    //}


    //public async Task<IActionResult> reservebooking()
    //{
    //    // 1. Get all operators from tbl_operator_mobile
    //    var operators = await _context.Operators
    //        .Select(o => new { o.Id, o.BusinessName })
    //        .ToListAsync();

    //    // 2. Load reserved guests
    //    var reservedGuests = await _context.Guests
    //        .Include(g => g.NationalityEntity)
    //        .Where(g => g.BookingStatus == "reserved")
    //        .OrderBy(g => g.Id)
    //        .ToListAsync();

    //    if (!reservedGuests.Any())
    //        return View(new GuestListViewModel());

    //    // 3. Generate QR for each guest
    //    foreach (var guest in reservedGuests)
    //    {
    //        guest.QRText = GenerateQRText(guest);
    //        guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
    //    }

    //    // 4. Group guests by Batch and map Operator BusinessName
    //    var grouped = reservedGuests
    //        .GroupBy(g => g.Batch)
    //        .Select(grp =>
    //        {
    //            var first = grp.First();

    //            // ✅ Lookup Operator's BusinessName from tbl_operator_mobile
    //            var businessName = operators
    //                .FirstOrDefault(o => o.Id == first.OperatorId)?.BusinessName ?? "N/A";

    //            return new Guest
    //            {
    //                Id = first.Id,
    //                Fullname = first.Fullname,
    //                Gender = first.Gender,
    //                NationalityEntity = first.NationalityEntity,
    //                OperatorId = first.OperatorId,

    //                // ✅ Inject BusinessName using a stubbed OperatorList object
    //                OperatorList = new OperatorList
    //                {
    //                    BusinessName = businessName
    //                },

    //                RFID = grp.Count(x => x.BookingStatus != "canceled"),
    //                ArrivalDate = first.ArrivalDate,
    //                BookingStatus = first.BookingStatus,
    //                Date = first.Date,
    //                QRText = first.QRText,
    //                QRBase64 = first.QRBase64,
    //                Batch = first.Batch
    //            };
    //        })
    //        .ToList();

    //    // 5. Generate batch QR
    //    string batchCode = reservedGuests.First().Batch;
    //    string batchQrBase64 = GenerateQRCodeBase64(batchCode);

    //    var vm = new GuestListViewModel
    //    {
    //        ReservedGuests = grouped,
    //        BatchQrBase64 = batchQrBase64
    //    };

    //    return View(vm);
    //}

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


        public IActionResult Index()
        {
            return View();
        }
  





        // GET: FinalBookingBatch
        public IActionResult BookedGuest()
        {
            return View();
        }


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

            return PartialView("_GuestDetailsPartial", guestsWithOperatorName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetGuestsData(string startDate, string endDate)
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            DateTime? startDateValue = null;
            DateTime? endDateValue = null;

            if (DateTime.TryParse(startDate, out DateTime sd))
                startDateValue = sd.Date;

            if (DateTime.TryParse(endDate, out DateTime ed))
                endDateValue = ed.Date.AddDays(1).AddTicks(-1);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int operatorId))
                currentOperatorId = operatorId;

            var query = _context.Guests
                .Where(g => g.BookingStatus.ToLower() == "confirmed");

            if (currentOperatorId.HasValue)
                query = query.Where(g => g.OperatorId == currentOperatorId.Value);

            // Materialize query BEFORE parsing string dates
            var allGuests = await query.ToListAsync();

            if (startDateValue.HasValue && endDateValue.HasValue)
            {
                allGuests = allGuests
                    .Where(g => DateTime.TryParse(g.ArrivalDate, out DateTime arrival) &&
                                arrival >= startDateValue.Value &&
                                arrival <= endDateValue.Value)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                allGuests = allGuests
                    .Where(g => g.Batch.Contains(search) || g.Fullname.Contains(search))
                    .ToList();
            }

            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            var groupedRaw = allGuests
                .GroupBy(g => new { g.Batch, g.OperatorId })
                .Select(grp => new
                {
                    Batch = grp.Key.Batch,
                    OperatorId = grp.Key.OperatorId,
                    TotalGuests = grp.Count(g => g.BookingStatus != "canceled"),
                    ArrivalDate = grp.Min(x => x.ArrivalDate),
                    Status = "confirmed",
                    MainGuestId = grp.OrderBy(x => x.Id).First().Id
                })
                .OrderBy(g => g.OperatorId)
                .ThenBy(g => g.Batch)
                .Skip(start)
                .Take(length)
                .ToList();

            var recordsTotal = allGuests
                .Select(g => new { g.Batch, g.OperatorId })
                .Distinct()
                .Count();

            var grouped = groupedRaw.Select(g =>
            {
                var businessName = operators
                    .FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A";

                return new
                {
                    id = g.MainGuestId,
                    batchCode = g.Batch,
                    operatorName = businessName,
                    totalGuests = g.TotalGuests,
                    arrivalDate = g.ArrivalDate,
                    status = g.Status
                };
            });

            return Json(new
            {
                draw,
                recordsFiltered = recordsTotal,
                recordsTotal,
                data = grouped
            });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookedGuest(string BatchCode)
        {
            if (string.IsNullOrEmpty(BatchCode))
            {
                TempData["ToastMessage"] = "Batch code is required.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ReserveBooking");
            }

            // ✅ Get current user's ID and Role from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
            {
                currentOperatorId = parsedId;
            }

            // ✅ Get a sample guest from this batch
            var sampleGuest = await _context.Guests
                .FirstOrDefaultAsync(g => g.Batch == BatchCode);

            if (sampleGuest == null)
            {
                TempData["ToastMessage"] = $"Invalid batch code: {BatchCode}";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ReserveBooking");
            }

            // ✅ Prevent operators from confirming others' batches
            if (currentOperatorId.HasValue && sampleGuest.OperatorId != currentOperatorId.Value)
            {
                TempData["ToastMessage"] = "🚫 You are not authorized to confirm this batch.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ReserveBooking");
            }

            // ✅ Always fetch operator name directly from DB to avoid N/A issues
            var operatorName = await _context.Operators
                .Where(o => o.Id == sampleGuest.OperatorId)
                .Select(o => o.BusinessName)
                .FirstOrDefaultAsync() ?? "N/A";

            // ✅ Update all guests with same OperatorId + Batch
            var guestsToFinalize = await _context.Guests
                .Where(g => g.OperatorId == sampleGuest.OperatorId &&
                            g.Batch == BatchCode &&
                            g.BookingStatus != "confirmed")
                .ToListAsync();

            if (guestsToFinalize.Any())
            {
                foreach (var guest in guestsToFinalize)
                {
                    guest.BookingStatus = "confirmed";
                }

                await _context.SaveChangesAsync();

                TempData["ToastMessage"] = $"✅ Confirmed booking";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"ℹ️ No pending guests to confirm for batch <b>{BatchCode}</b>.";
                TempData["ToastType"] = "info";
            }

            // ✅ Optional: Populate OperatorList for dropdown in ReserveBooking view
            List<Operator> operators;
            if (userRole == "Operator" && currentOperatorId.HasValue)
            {
                operators = await _context.Operators
                    .Where(o => o.Id == currentOperatorId.Value)
                    .ToListAsync();
            }
            else
            {
                operators = await _context.Operators.ToListAsync();
            }

            ViewBag.OperatorList = new SelectList(operators, "Id", "BusinessName");

            return RedirectToAction("ReserveBooking", new { batchCode = BatchCode });
        }




    }



}

