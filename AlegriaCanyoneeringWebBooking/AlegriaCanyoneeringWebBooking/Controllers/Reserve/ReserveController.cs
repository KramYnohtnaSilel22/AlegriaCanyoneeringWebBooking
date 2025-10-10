
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
        public async Task<IActionResult> GetGuestsData(string? startDate, string? endDate)
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            // 🔹 Convert from string to DateTime
            DateTime? startDateValue = null;
            DateTime? endDateValue = null;

            if (DateTime.TryParse(startDate, out DateTime sd))
                startDateValue = sd.Date;

            if (DateTime.TryParse(endDate, out DateTime ed))
                endDateValue = ed.Date.AddDays(1).AddTicks(-1);

            // 🔹 Load Guests first (BookingStatus = 3 = confirmed)
            var guests = await _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == 3)
                .ToListAsync();

            // 🔹 Convert Unix timestamp to DateTime & filter in-memory
            var filteredGuests = guests
                .Where(g =>
                {
                    if (string.IsNullOrEmpty(g.ArrivalDate))
                        return false;

                    if (!long.TryParse(g.ArrivalDate, out var unix))
                        return false;

                    // Convert Unix → DateTime (UTC → local)
                    var arrival = DateTimeOffset.FromUnixTimeSeconds(unix).DateTime;

                    if (startDateValue.HasValue && endDateValue.HasValue)
                        return arrival >= startDateValue.Value && arrival <= endDateValue.Value;

                    return true;
                })
                .ToList();

            // 🔹 Group by Batch
            var groupedData = filteredGuests
                .GroupBy(g => g.Batch)
                .Select(g => new
                {
                    batch = g.Key,
                    totalGuests = g.Count(),
                    operatorName = g.FirstOrDefault()?.OperatorList?.BusinessName ?? "N/A",
                    arrivalDate = DateTimeOffset.FromUnixTimeSeconds(
                        long.Parse(g.FirstOrDefault()?.ArrivalDate ?? "0")).DateTime.ToString("yyyy-MM-dd"),
                    status = "Confirmed"
                })
                .ToList();

            // 🔹 Apply pagination
            var pagedData = groupedData.Skip(start).Take(length);

            return Json(new
            {
                draw,
                recordsTotal = groupedData.Count,
                recordsFiltered = groupedData.Count,
                data = pagedData
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

            // ✅ Update all guests with same OperatorId + Batch (BookingStatus == 3)
            var guestsToFinalize = await _context.Guests
                .Where(g => g.OperatorId == sampleGuest.OperatorId &&
                            g.Batch == BatchCode &&
                            g.BookingStatus != 3) // Filter for guests whose BookingStatus is not "Confirmed"
                .ToListAsync();

            if (guestsToFinalize.Any())
            {
                foreach (var guest in guestsToFinalize)
                {
                    guest.BookingStatus = 3;  // Set BookingStatus to "Confirmed" (integer value 3)
                }

                await _context.SaveChangesAsync();

                TempData["ToastMessage"] = $"✅ Confirmed booking for batch";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"ℹ️ No pending guests ";
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