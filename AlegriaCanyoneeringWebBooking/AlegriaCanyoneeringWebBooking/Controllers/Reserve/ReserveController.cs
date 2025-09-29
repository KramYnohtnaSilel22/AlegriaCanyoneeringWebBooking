
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
            var reservedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .Where(g => g.BookingStatus == "reserved")
                .OrderBy(g => g.Id)
                .ToListAsync();

            if (!reservedGuests.Any())
                return View(new GuestListViewModel());

            foreach (var guest in reservedGuests)
            {
                guest.QRText = GenerateQRText(guest);
                guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
            }

            var grouped = reservedGuests
                .GroupBy(g => g.OperatorId)
                .Select(grp =>
                {
                    var first = grp.First();
                    return new Guest
                    {
                        Id = first.Id,  // <--- Important for link routing
                        OperatorId = grp.Key,
                        OperatorList = first.OperatorList,
                        NumberOfGuests = grp.Count(x => x.BookingStatus != "canceled"),
                        ArrivalDate = first.ArrivalDate,
                        BookingStatus = first.BookingStatus,
                        QRText = GenerateQRText(first),
                        QRBase64 = GenerateQRCodeBase64(first.OperatorList?.BusinessName ?? ""),
                        Batch = first.Batch  // <-- ensure Batch is carried over
                    };
                })
                .ToList();

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
            // Retrieve the main guest by Id with related data
            var mainGuest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (mainGuest == null)
                return NotFound();

            // Get all reserved guests with the same OperatorId
            var guestsInOperator = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .Where(g => g.OperatorId == mainGuest.OperatorId && g.BookingStatus == "reserved")
                .OrderBy(g => g.Id)
                .ToListAsync();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInOperator
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

    var guests = await _context.Guests
        .Include(g => g.OperatorList)
        .Include(g => g.NationalityEntity)
        .Where(g => g.Batch == batchCode)
        .ToListAsync();

    return PartialView("_GuestDetailsPartial", guests);
}

        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == "confirmed");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.Batch.Contains(search) ||
                    g.OperatorList.BusinessName.Contains(search)
                );
            }

            var recordsTotal = await query.CountAsync();

            // Group guests by Batch, Operator and ArrivalDate and get total guests count
            var grouped = query
                .GroupBy(g => new { g.Batch, OperatorName = g.OperatorList.BusinessName, g.ArrivalDate, g.BookingStatus })
                .Select(grp => new
                {
                    batchCode = grp.Key.Batch,
                    operatorName = grp.Key.OperatorName,
                    arrivalDate = grp.Key.ArrivalDate, // format date as ISO string for JS
                    status = grp.Key.BookingStatus,
                    totalGuests = grp.Count()
                });

            var filteredData = await grouped
                .OrderBy(g => g.batchCode)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return Json(new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsTotal,
                data = filteredData
            });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalBookingBatch(string BatchCode)
        {
            if (string.IsNullOrEmpty(BatchCode))
            {
           
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

            if (startDate.HasValue && endDate.HasValue)
            {
                // Make sure to include entire end day
                var endDateInclusive = endDate.Value.Date.AddDays(1).AddTicks(-1);

                // Get all guests including navigation properties
                var guests = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .ToListAsync();

                // Helper function to convert Unix timestamp string to DateTime?
                DateTime? ConvertUnixTimestampToDateTime(string unixTimestamp)
                {
                    if (long.TryParse(unixTimestamp, out var seconds))
                    {
                        // Unix timestamp assumed to be seconds since epoch UTC
                        var dateTime = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                        return dateTime;
                    }
                    return null;
                }

                // Filter guests by ArrivalDate parsed as Unix timestamp and date range
                var filteredGuests = guests
                    .Select(g =>
                    {
                        var arrivalDate = ConvertUnixTimestampToDateTime(g.ArrivalDate);
                        return new { Guest = g, ArrivalDate = arrivalDate };
                    })
                    .Where(x => x.ArrivalDate.HasValue &&
                                x.ArrivalDate.Value >= startDate.Value.Date &&
                                x.ArrivalDate.Value <= endDateInclusive)
                    .OrderBy(x => x.ArrivalDate.Value)
                    .Select(x => x.Guest)
                    .ToList();

                // Group guests by BatchCode
                model.BatchGuests = filteredGuests
                    .GroupBy(g => g.Batch)  // Assuming g.Batch stores the BatchCode
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Pass the batch codes to ViewBag
                ViewBag.AllBatches = model.BatchGuests.Keys.ToList();
            }
            else
            {
                // No filter, empty batch guest dictionary & batch list
                model.BatchGuests = new Dictionary<string, List<Guest>>();
                ViewBag.AllBatches = new List<string>();
            }

            return View(model);
        }


    }



}

