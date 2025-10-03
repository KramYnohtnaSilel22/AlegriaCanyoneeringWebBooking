
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
        public IActionResult GetGuestOfTheDay()
        {
            var guest = _guestService.GetGuestOfTheDay();
            if (guest == null)
            {
                return Content("<p>No guest of the day found.</p>", "text/html");
            }
            // Return a partial view with a list (because your existing partial expects List<Guest>)
            return PartialView("_GuestDetailsPartial", new List<Guest> { guest });
        }


        public async Task<IActionResult> reservebooking()
        {
            // 1. Get all operators from tbl_operator_mobile
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            // 2. Load reserved guests
            var reservedGuests = await _context.Guests
                .Include(g => g.NationalityEntity)
                .Where(g => g.BookingStatus == "reserved")
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
        public async Task<IActionResult> ReserveDetails(int id)
        {
            // Get the main guest, including the nationality (make sure to include Nationality)
            var mainGuest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)  // Ensure Nationality is loaded
                .FirstOrDefaultAsync(g => g.Id == id);

            if (mainGuest == null)
            {
                return NotFound(); // Return if no guest found
            }

            // Get all other guests in the same batch, excluding the main guest
            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)  // Ensure Nationality is included
                .Where(g => g.Batch == mainGuest.Batch && g.Id != mainGuest.Id)
                .OrderBy(g => g.Id)
                .ToListAsync(); // ✅ removed .Take(4)

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
            };

            return View(model);
        }





        //public async Task<IActionResult> ReserveDetails(int id)
        //{
        //    // Retrieve main guest, including related entities
        //    var mainGuest = await _context.Guests
        //        .Include(g => g.OperatorList)
        //        .Include(g => g.NationalityEntity)
        //        .FirstOrDefaultAsync(g => g.Id == id);

        //    if (mainGuest == null)
        //    {
        //        return NotFound();
        //    }

        //    // Get all active (non-canceled) guests in the same batch
        //    var guestsInBatch = await _context.Guests
        //        .Include(g => g.OperatorList)
        //        .Include(g => g.NationalityEntity)
        //        .Where(g => g.Batch == mainGuest.Batch && g.BookingStatus != "canceled")
        //        .OrderBy(g => g.Id)
        //        .ToListAsync();

        //    // Optionally, display main guest (current record) as companion or not
        //    // If you want to exclude main guest from companion list:
        //    guestsInBatch = guestsInBatch.Where(g => g.Id != mainGuest.Id).ToList();

        //    var model = new GuestDetailsViewModel
        //    {
        //        Guest = mainGuest,
        //        GuestsInBatch = guestsInBatch
        //        // Add other view properties as needed
        //    };

        //    return View(model);
        //}



        // GET: FinalBookingBatch
        public IActionResult FinalBookingBatch()
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
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            // Get user id & role
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;

            if (userRole == "Operator" && int.TryParse(userId, out int operatorId))
            {
                currentOperatorId = operatorId;
            }

            // Base query with BookingStatus = "confirmed"
            var query = _context.Guests
                .Where(g => g.BookingStatus.ToLower() == "confirmed");

            // Filter by operator if current user is operator
            if (currentOperatorId.HasValue)
            {
                query = query.Where(g => g.OperatorId == currentOperatorId.Value);
            }

            // Search filter on Batch or Fullname
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.Batch.Contains(search) ||
                    g.Fullname.Contains(search)
                );
            }

            // Get operators list for name mapping
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            // Group by Batch + OperatorId + BookingStatus (confirmed)
            var groupedRaw = await query
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
                .ToListAsync();

            // Total records count
            var recordsTotal = await query
                .Select(g => new { g.Batch, g.OperatorId })
                .Distinct()
                .CountAsync();

            // Map operator names
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
        public async Task<IActionResult> FinalBookingBatch(string BatchCode)
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

                TempData["ToastMessage"] = $"✅ Confirmed booking</i>";
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


        public async Task<IActionResult> SaveGuest(DateTime? startDate, DateTime? endDate)
        {
            var model = new GuestListViewModel();

            if (startDate.HasValue && endDate.HasValue)
            {
                // Include full day for endDate
                var endDateInclusive = endDate.Value.Date.AddDays(1).AddTicks(-1);

                // Load guests with related data
                var guests = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .ToListAsync();

                // Helper: convert Unix timestamp string to DateTime?
                DateTime? ConvertUnixTimestampToDateTime(string unixTimestamp)
                {
                    if (long.TryParse(unixTimestamp, out var seconds))
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                    }
                    return null;
                }

                // Filter guests by ArrivalDate inside range
                var filteredGuests = guests
                    .Select(g => new
                    {
                        Guest = g,
                        ArrivalDate = ConvertUnixTimestampToDateTime(g.ArrivalDate)
                    })
                    .Where(x => x.ArrivalDate.HasValue
                                && x.ArrivalDate.Value >= startDate.Value.Date
                                && x.ArrivalDate.Value <= endDateInclusive)
                    .OrderBy(x => x.ArrivalDate.Value)
                    .Select(x => x.Guest)
                    .ToList();

                // Group by Batch code
                model.BatchGuests = filteredGuests
                    .GroupBy(g => g.Batch)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Pass batch codes to ViewBag
                ViewBag.AllBatches = model.BatchGuests.Keys.ToList();

                // If no bookings found, add a flag for frontend
                ViewBag.HasBookings = model.BatchGuests.Any();
            }
            else
            {
                model.BatchGuests = new Dictionary<string, List<Guest>>();
                ViewBag.AllBatches = new List<string>();
                ViewBag.HasBookings = false;
            }

            return View(model);
        }



    }



}

