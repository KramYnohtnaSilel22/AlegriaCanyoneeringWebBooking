
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class ReserveController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;

        public ReserveController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<GuestController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;

            // Test connection
            if (!_context.Database.CanConnect())
            {
                throw new Exception("Cannot connect to database. Please check your connection string.");
            }
        }


        public async Task<IActionResult> reservebooking()
        {
            // Get all reserved/confirmed guests
            var reservedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)

                .Where(g => g.BookingStatus == "reserved" )
                .OrderBy(g => g.Id)
                .ToListAsync();

            if (!reservedGuests.Any())
                return View(new GuestListViewModel());   // nothing to show

            // Generate individual guest QR codes (already exists, but you can modify for batch QR)
            foreach (var guest in reservedGuests)
            {
                guest.QRText = GenerateQRText(guest);
                guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
            }

            // Generate Batch QR code for batchCode (for modal display)
            string batchCode = reservedGuests.First().Batch;
            string batchQrBase64 = GenerateQRCodeBase64(batchCode); // Only BatchCode in QR

            var vm = new GuestListViewModel
            {
                ReservedGuests = reservedGuests,
                BatchQrBase64 = batchQrBase64 // Batch QR Code to be used in modal
            };
            return View(vm);
        }

        // ---------- QR Helpers ----------

        private string GenerateQRText(Guest guest)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Batch        : {guest.Batch}");

            return sb.ToString();
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
            // Retrieve main guest, including related entities
            var mainGuest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (mainGuest == null)
            {
                return NotFound();
            }

            // Get all active (non-canceled) guests in the same batch
            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .Where(g => g.Batch == mainGuest.Batch && g.BookingStatus != "canceled")
                .OrderBy(g => g.Id)
                .ToListAsync();

            // Optionally, display main guest (current record) as companion or not
            // If you want to exclude main guest from companion list:
            guestsInBatch = guestsInBatch.Where(g => g.Id != mainGuest.Id).ToList();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
                // Add other view properties as needed
            };

            return View(model);
        }


        // GET: The main page that shows the table and modals
        public IActionResult FinalBookingBatch()
        {
            return View();
        }

        // DataTables AJAX POST endpoint for guests batches
        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            // Query confirmed guests grouped by batch
            var batchQuery = _context.Guests
                .Where(g => g.BookingStatus == "confirmed")
                .GroupBy(g => g.Batch)
                .Select(g => new
                {
                    BatchCode = g.Key,
                    TotalGuests = g.Count(),
                    OperatorName = g.FirstOrDefault().OperatorList.BusinessName,
                    ArrivalDate = g.FirstOrDefault().ArrivalDate,
                    Status = "Confirmed"
                });

            if (!string.IsNullOrEmpty(search))
            {
                batchQuery = batchQuery.Where(b =>
                    b.BatchCode.Contains(search) ||
                    b.OperatorName.Contains(search)
                );
            }

            var recordsTotal = await batchQuery.CountAsync();

            var data = await batchQuery
                .OrderByDescending(b => b.ArrivalDate)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return Json(new
            {
                draw = draw,
                recordsFiltered = recordsTotal,
                recordsTotal = recordsTotal,
                data = data
            });
        }

        // Endpoint to get guests by batch (for modal details)
        [HttpGet]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode)
        {
            if (string.IsNullOrEmpty(batchCode))
                return BadRequest("BatchCode is required");

            var guests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .Where(g => g.Batch == batchCode)
                .ToListAsync();

            return PartialView("_GuestDetailsPartial", guests);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalBookingBatch(string BatchCode)
        {
            if (string.IsNullOrEmpty(BatchCode))
            {
                TempData["ToastMessage"] = "BatchCode is required.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("reservebooking");
            }

            // Update all guests in the batch to confirmed
            var guestsToFinalize = await _context.Guests
                .Where(g => g.Batch == BatchCode && g.BookingStatus != "confirmed")
                .ToListAsync();

            if (guestsToFinalize.Any())
            {
                foreach (var guest in guestsToFinalize)
                {
                    guest.BookingStatus = "confirmed";
                }
                await _context.SaveChangesAsync();

                TempData["ToastMessage"] = "Batch confirmed successfully!";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = "No guests to confirm in this batch.";
                TempData["ToastType"] = "info";
            }

            return RedirectToAction("reservebooking", new { BatchCode });
        }


        public async Task<IActionResult> SaveGuest(DateTime? startDate, DateTime? endDate)
        {
            var model = new GuestListViewModel();
            var allBatches = new List<Reserve>();

            if (startDate.HasValue && endDate.HasValue)
            {
                // Ensure endDate includes the whole day by adding time to end of day
                var endDateInclusive = endDate.Value.Date.AddDays(1).AddTicks(-1);

                allBatches = await _context.reserve
                    .Where(r => r.ArrivalDate >= startDate.Value.Date && r.ArrivalDate <= endDateInclusive)
                    .OrderBy(r => r.ArrivalDate)
                    .ToListAsync();

                var batchCodes = allBatches.Select(b => b.BatchCode).ToList();

                var batchGuests = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .Where(g => batchCodes.Contains(g.Batch))
                    .ToListAsync();

                model.BatchGuests = batchGuests
                    .GroupBy(g => g.Batch)
                    .ToDictionary(g => g.Key, g => g.ToList());

                ViewBag.AllBatches = allBatches;
            }
            else
            {
                ViewBag.AllBatches = new List<Reserve>();
            }

            return View(model);
        }


    }
}
