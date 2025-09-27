using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;



using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin,Operator")]
    public class GuestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;

        public GuestController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<GuestController> logger)
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
        //[HttpPost]
        //public async Task<IActionResult> GetGuestsData()
        //{
        //    var draw = Request.Form["draw"].FirstOrDefault();
        //    var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
        //    var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
        //    var search = Request.Form["search[value]"].FirstOrDefault();

        //    var query = _context.Guests
        //        .Include(g => g.OperatorList) // Optional kung kailangan sa view
        //        .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved");

        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        query = query.Where(g =>
        //            g.Fullname.Contains(search) ||
        //            g.Batch.Contains(search));
        //    }

        //    var recordsTotal = await query.CountAsync();

        //    var data = await query
        //        .OrderBy(g => g.Id)
        //        .Skip(start)
        //        .Take(length)
        //        .ToListAsync();

        //    return Json(new
        //    {
        //        draw = draw,
        //        recordsFiltered = recordsTotal,
        //        recordsTotal = recordsTotal,
        //        data = data.Select(g => new {
        //            fullname = g.Fullname,
        //            rfid = g.RFID,
        //            batch = g.Batch,
        //            bookingStatus = g.BookingStatus
        //        })
        //    });
        //}
        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.Guests
                .Include(g => g.OperatorList) // Include operator data
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.Fullname.Contains(search) ||
                    g.Batch.Contains(search) ||
                    (g.OperatorList != null && g.OperatorList.BusinessName.Contains(search))
                );
            }

            var recordsTotal = await query.CountAsync();

            var data = await query
                .OrderBy(g => g.Id)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return Json(new
            {
                draw = draw,
                recordsFiltered = recordsTotal,
                recordsTotal = recordsTotal,
                data = data.Select(g => new {
                    g.Id,
                    OperatorName = g.OperatorList != null ? g.OperatorList.BusinessName : "N/A",
                    totalGuests = g.NumberOfGuests,    // Add this if your model has it
                    arrivalDate = g.ArrivalDate, // Format as needed
                    g.BookingStatus
                })
            });
        }

        private async Task PopulateDropdowns()
        {

            ViewBag.OperatorList = new SelectList(await _context.OperatorLists.ToListAsync(), "OperatorId", "BusinessName");
            ViewBag.NationalityList = new SelectList(await _context.Nationalities.ToListAsync(), "NationalityId", "NatName"); // Populate Nationality dropdown
        }

        // Fix for CS0029: Cannot implicitly convert type 'string' to 'int?'
        // The issue is likely in the assignment of `guest.RFID` where `GenerateRFID()` returns a string but `RFID` expects an int?.
        // Update the `GenerateRFID` method to return an int instead of a string.

        private int GenerateRFID()
        {
            var hexString = Guid.NewGuid().ToString("N").Substring(0, 8); // 8 chars fit in int
            return int.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
        }

        public async Task<IActionResult> NewBooking(string batch, int? id)
        {
            await PopulateDropdowns();

            // Your existing logic for filtered guests
            var filteredGuests = await _context.Guests
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                .GroupBy(g => g.Batch)
                .Select(grp => grp.OrderBy(x => x.Id).FirstOrDefault())
                .ToListAsync();

            var model = new GuestListViewModel
            {
                NewGuest = new Guest(),
                ReservedGuests = filteredGuests
            };

            ViewBag.CurrentBatch = batch;
            ViewBag.MainGuestId = id;

            // Your existing logic
            if (!string.IsNullOrEmpty(batch))
            {
                var batchDetails = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Where(g => g.Batch == batch)
                    .OrderBy(g => g.Id)
                    .Select(g => new
                    {
                        g.OperatorId,
                        OperatorName = g.OperatorList.BusinessName,
                        g.Date,
                        g.ArrivalDate,
                        g.Area
                    })
                    .FirstOrDefaultAsync();

                if (batchDetails != null)
                {
                    // Set your model properties here
                    model.NewGuest.OperatorId = batchDetails.OperatorId;
                    model.NewGuest.Date = batchDetails.Date;
                    model.NewGuest.ArrivalDate = batchDetails.ArrivalDate;
                    model.NewGuest.Area = batchDetails.Area;
                }
            }

            // Set ViewBag.IsReadonly based on batch presence
            ViewBag.IsReadonly = !string.IsNullOrEmpty(batch);

            // Other ViewBags or model assignments
            ViewBag.CurrentBatch = batch;
            // etc...

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(GuestListViewModel model, string batch = null, int? id = null)
        {
            string batchId = !string.IsNullOrEmpty(batch) ? batch : DateTime.Now.ToString("yyyyMMddHHmmss");

            if (!string.IsNullOrWhiteSpace(model.BatchGuestsJson))
            {
                var batchGuests = JsonSerializer.Deserialize<List<Guest>>(model.BatchGuestsJson);

                if (batchGuests != null && batchGuests.Count > 0)
                {
                    foreach (var guest in batchGuests)
                    {
                        guest.BookingStatus = "anticipated";
                        guest.Batch = batchId;

                        // Update the usage of `GenerateRFID` to ensure it matches the expected type.
                        guest.RFID = GenerateRFID();
                        guest.RFIDCode = guest.RFIDCode ?? GenerateRFIDCode(); // Optional random code

                        guest.Month = DateTime.Today.ToString("yyyy-MM");
                        guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");

                        _context.Guests.Add(guest);
                    }

                    // Save batch guests to database
                    await _context.SaveChangesAsync();

                    // Set number of guests for this batch
                    var guestsInBatch = await _context.Guests
                        .Where(g => g.Batch == batchId)
                        .ToListAsync();

                    int count = guestsInBatch.Count;
                    guestsInBatch.ForEach(g => g.NumberOfGuests = count);

                    await _context.SaveChangesAsync();

                    // Success message
                    TempData["ToastMessage"] = "Guests added successfully!";
                    TempData["ToastType"] = "success";

                    // Redirect depending on single or batch mode
                    if (id.HasValue && id.Value > 0)
                    {
                        return RedirectToAction("NewBooking", new { id = id.Value });
                    }
                    else
                    {
                        return RedirectToAction("NewBooking");
                    }
                }

                // No guests error
                TempData["ToastMessage"] = "Please add at least one guest before saving!";
                TempData["ToastType"] = "danger";
            }

            // In case of model error or no data
            await PopulateDropdowns();
            return View(model);

        }

        private string GenerateRFIDCode()
        {
            var bytes = new byte[6];
            new Random().NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
        public IActionResult saveguest()
        {
            var model = new GuestListViewModel
            {
                ReservedGuests = new List<Guest>() // empty list para di mag-null
            };

            return View(model);
        }




        private int GetCurrentOperatorId()
        {
            int operatorId;
            if (int.TryParse(User.Identity?.Name, out operatorId))
            {
                return operatorId;
            }
            return 0;
        }

        public async Task<IActionResult> GetNationalities()
        {
            try
            {
                var nationalities = await _context.Nationalities
                    .Where(n => n.NatName != "Within Cebu Province" &&
                                n.NatName != "Outside Cebu Province")
                    .OrderBy(n => n.NatName)
                    .Select(n => new
                    {
                        n.NationalityId,      // Return Id
                        n.NatName  // Return Name
                    })
                    .ToListAsync();

                return Json(nationalities);  // Return the list of nationalities as JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });  // Return error if something goes wrong
            }
        }



        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            guest.BookingStatus = status;

            try
            {
                await _context.SaveChangesAsync();
                // Toast message using TempData
                TempData["ToastMessage"] = "Submit successfully!";
                TempData["ToastType"] = "success";
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency conflict: guest may have been modified by another process.");
            }

            // If status is reserved, calculate the group index (page) and redirect to ReserveDetails
            if (status.Equals("reserved", StringComparison.OrdinalIgnoreCase))
            {
                // Get all guests in the batch ordered by GuestId
                var batchGuests = await _context.Guests
                    .Where(g => g.Batch == guest.Batch)
                    .OrderBy(g => g.Id)
                    .ToListAsync();

                int index = batchGuests.FindIndex(g => g.Id == id);

                if (index == -1)
                {
                    // Fallback: just redirect without page param if guest not found (should not happen)
                    return RedirectToAction(nameof(saveguest), new { id });
                }

                int groupIndex = index / 5;

                return RedirectToAction(nameof(saveguest), new { id, page = groupIndex });
            }

            if (status.Equals("accepted", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(saveguest));
            }

            return RedirectToAction(nameof(saveguest));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGuest(int GuestId)
        {
            var guest = await _context.Guests.FindAsync(GuestId);

            if (guest == null)
                return NotFound("Guest not found.");

            guest.BookingStatus = "canceled";
            await _context.SaveChangesAsync();

            // Recalculate number of active (non-canceled) guests in the same batch
            var updatedGuestCount = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.BookingStatus != "canceled")
                .CountAsync();

            // Update NumberOfGuests for all guests in the batch
            var guestsInBatch = await _context.Guests
                .Where(g => g.Batch == guest.Batch)
                .ToListAsync();

            foreach (var g in guestsInBatch)
            {
                g.NumberOfGuests = updatedGuestCount;
            }

            await _context.SaveChangesAsync();

            // Redirect back to the BookingDetails
            return RedirectToAction("saveguest", new { batch = guest.Batch });
        }
        public IActionResult DownloadQRCode(string base64Image, string fileName)
        {
            if (string.IsNullOrEmpty(base64Image))
            {
                TempData["ToastMessage"] = "No image data provided.";
                TempData["ToastType"] = "danger"; // can be 'success', 'danger', etc.
                return RedirectToAction("ReserveBookings"); // or whatever action/page, update as needed
            }

            try
            {
                // Remove the "data:image/png;base64," prefix if it exists
                var base64Data = base64Image.Substring(base64Image.IndexOf(",") + 1);
                var imageBytes = Convert.FromBase64String(base64Data);

                return File(imageBytes, "image/png", fileName);
            }
            catch (Exception ex)
            {
                TempData["ToastMessage"] = $"Error downloading QR code: {ex.Message}";
                TempData["ToastType"] = "danger";
                return RedirectToAction("reservebooking"); // fallback action
            }
        }

        private bool GuestExists(int id)
        {
            return _context.Guests.Any(e => e.Id == id);
        }



    }
}


