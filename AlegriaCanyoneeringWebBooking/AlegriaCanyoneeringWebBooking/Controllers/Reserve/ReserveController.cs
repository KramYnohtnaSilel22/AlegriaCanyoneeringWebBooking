
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Amqp.Framing;
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

            // Generate QR for each guest
            foreach (var guest in reservedGuests)
            {
                guest.QRText = GenerateQRText(guest);
                guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
            }

            // ✅ Group guests by Batch
            var grouped = reservedGuests
                .GroupBy(g => g.Batch)
                .Select(grp =>
                {
                    var first = grp.First();
                    return new Guest
                    {
                        Id = first.Id,
                        Fullname = first.Fullname,
                        Gender = first.Gender,
                        NationalityEntity = first.NationalityEntity,
                   
                        OperatorId = first.OperatorId,
                        OperatorList = first.OperatorList,
                        NumberOfGuests = grp.Count(x => x.BookingStatus != "canceled"),
                        ArrivalDate = first.ArrivalDate,
                        BookingStatus = first.BookingStatus,
                        Date = first.Date, // <== Make sure this is assigned
                   
                        QRText = first.QRText,
                        QRBase64 = first.QRBase64,
                        Batch = first.Batch
                    };
                })
                .ToList();


            // Optional: Use first batch for QR display (not really needed per-row)
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
        public async Task<IActionResult> ReserveDetails(int id )
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

            // Get other guests in the same batch, excluding the main guest
            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)  // Ensure Nationality is included
                .Where(g => g.Batch == mainGuest.Batch && g.Id != mainGuest.Id)
                .OrderBy(g => g.Id)
                .Take(4)
                .ToListAsync();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch,
             

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

            // Parse startDate and endDate from the request form
            DateTime? startDate = null;
            DateTime? endDate = null;

            if (DateTime.TryParse(Request.Form["startDate"].FirstOrDefault(), out var parsedStartDate))
            {
                startDate = parsedStartDate.Date;
            }

            if (DateTime.TryParse(Request.Form["endDate"].FirstOrDefault(), out var parsedEndDate))
            {
                // End of day for inclusive filtering
                endDate = parsedEndDate.Date.AddDays(1).AddTicks(-1);
            }

            // Base query for confirmed bookings
            var query = _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus.ToLower() == "confirmed");

            // Apply search filter if exists
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.Batch.Contains(search) ||
                    g.OperatorList.BusinessName.Contains(search)
                );
            }

            // Fetch all matching guests into memory for date filtering
            var guestList = await query.ToListAsync();

            // Filter guests by arrivalDate in memory (ArrivalDate is string, so parse it)
            if (startDate.HasValue)
            {
                guestList = guestList.Where(g =>
                {
                    if (DateTime.TryParse(g.ArrivalDate, out var arrival))
                    {
                        return arrival >= startDate.Value;
                    }
                    return false;
                }).ToList();
            }

            if (endDate.HasValue)
            {
                guestList = guestList.Where(g =>
                {
                    if (DateTime.TryParse(g.ArrivalDate, out var arrival))
                    {
                        return arrival <= endDate.Value;
                    }
                    return false;
                }).ToList();
            }

            // Group the filtered guests by Batch, Operator, ArrivalDate, BookingStatus
            var grouped = guestList
                .GroupBy(g => new { g.Batch, OperatorName = g.OperatorList.BusinessName, g.ArrivalDate, g.BookingStatus })
                .Select(grp => new
                {
                    batchCode = grp.Key.Batch,
                    operatorName = grp.Key.OperatorName,
                    arrivalDate = grp.Key.ArrivalDate,
                    status = grp.Key.BookingStatus,
                    totalGuests = grp.Count()
                })
                .OrderBy(g => g.batchCode)
                .ToList();

            // Pagination
            var pagedData = grouped
                .Skip(start)
                .Take(length)
                .ToList();

            // Return JSON result with DataTables parameters
            return Json(new
            {
                draw = draw,
                recordsTotal = grouped.Count,
                recordsFiltered = grouped.Count,
                data = pagedData
            });
        }



        //        [HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> FinalBookingBatch(string BatchCode)
        //{
        //    if (string.IsNullOrEmpty(BatchCode))
        //    {
        //        TempData["ToastType"] = "danger";
        //        return RedirectToAction("reservebooking");
        //    }

        //    // Get any guest from the batch to get the OperatorId
        //    var sampleGuest = await _context.Guests
        //        .FirstOrDefaultAsync(g => g.Batch == BatchCode);

        //    if (sampleGuest == null)
        //    {
        //        TempData["ToastMessage"] = "Invalid batch code.";
        //        TempData["ToastType"] = "danger";
        //        return RedirectToAction("reservebooking");
        //    }

        //    // Update all guests with same OperatorId and Batch to 'confirmed'
        //    var guestsToFinalize = await _context.Guests
        //        .Where(g => g.OperatorId == sampleGuest.OperatorId &&
        //                    g.Batch == BatchCode &&
        //                    g.BookingStatus != "confirmed")
        //        .ToListAsync();

        //    if (guestsToFinalize.Any())
        //    {
        //        foreach (var guest in guestsToFinalize)
        //        {
        //            guest.BookingStatus = "confirmed";
        //        }

        //        await _context.SaveChangesAsync();

        //        TempData["ToastMessage"] = "Guests confirmed successfully!";
        //        TempData["ToastType"] = "success";
        //    }
        //    else
        //    {
        //        TempData["ToastMessage"] = "No guests to confirm for this batch and operator.";
        //        TempData["ToastType"] = "info";
        //    }

        //    return RedirectToAction("reservebooking", new { BatchCode });
        //}

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

            // Get a sample guest from this batch
            var sampleGuest = await _context.Guests
                .FirstOrDefaultAsync(g => g.Batch == BatchCode);

            if (sampleGuest == null)
            {
                TempData["ToastMessage"] = $"Invalid batch code: {BatchCode}";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ReserveBooking");
            }

            // Update all guests with same OperatorId + Batch
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

                TempData["ToastMessage"] = "Confirm Successfully";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = $"ℹ️ No pending guests to confirm for batch {BatchCode}.";
                TempData["ToastType"] = "info";
            }

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

