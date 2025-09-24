
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
                .Include(g => g.Nationality)
                .Include(g => g.Driver)
                .Include(g => g.Guide)
                .Where(g => g.BookingStatus == "reserved" || g.BookingStatus == "confirmed")
                .OrderBy(g => g.GuestId)
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
                .Include(g => g.Nationality)
                .FirstOrDefaultAsync(g => g.GuestId == id);

            if (mainGuest == null)
            {
                return NotFound();
            }

            // Get all active (non-canceled) guests in the same batch
            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.Batch == mainGuest.Batch && g.BookingStatus != "canceled")
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            // Optionally, display main guest (current record) as companion or not
            // If you want to exclude main guest from companion list:
            guestsInBatch = guestsInBatch.Where(g => g.GuestId != mainGuest.GuestId).ToList();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
                // Add other view properties as needed
            };

            return View(model);
        }

        public async Task<IActionResult> FinalBookingBatch()
        {
            // Get all Reserve batches, order by CreatedDate DESC
            var allBatches = await _context.reserve.OrderByDescending(r => r.CreatedDate).ToListAsync();
            ViewBag.AllBatches = allBatches;

            // Gather all guests for each batch for the modal detail
            var batchGuestsDict = new Dictionary<string, List<Guest>>();
            foreach (var batch in allBatches)
            {
                var guests = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.Nationality)
                    .Where(g => g.Batch == batch.BatchCode && g.BookingStatus == "finalized")
                    .ToListAsync();
                batchGuestsDict[batch.BatchCode] = guests;
            }

            var model = new GuestListViewModel { BatchGuests = batchGuestsDict };
            return View(model);
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

            // Finalize guests for this batch
            var guestsToFinalize = await _context.Guests
                .Where(g => g.Batch == BatchCode && g.BookingStatus != "finalized")
                .ToListAsync();

            if (guestsToFinalize.Any())
            {
                foreach (var guest in guestsToFinalize)
                {
                    guest.BookingStatus = "finalized";
                }
                await _context.SaveChangesAsync();
            }

            // Add Reserve row for batch if it doesn't exist
            var batchGuests = await _context.Guests
            .Where(g => g.Batch == BatchCode)
                .ToListAsync();

            if (batchGuests.Count > 0 && !await _context.reserve.AnyAsync(r => r.BatchCode == BatchCode))
            {
                var first = batchGuests.First();
                DateTime arrivalDate;
                try { arrivalDate = Convert.ToDateTime(first.ArrivalDate); }
                catch { arrivalDate = DateTime.Now; }

                _context.reserve.Add(new Models.Reserve
                {
                    BatchCode = BatchCode,
                    OperatorId = first.OperatorId,
                    TotalGuests = batchGuests.Count,
                    ArrivalDate = arrivalDate,
                    Status = "finalized",
                    CreatedDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            // Add Toast, then redirect to GET version of same page to show toast message
            TempData["ToastMessage"] = "FinalBook Successfully!";
            TempData["ToastType"] = "success";
            return RedirectToAction("reservebooking", new { BatchCode }); // Adjust if your GET takes no param
        }


    }
}
