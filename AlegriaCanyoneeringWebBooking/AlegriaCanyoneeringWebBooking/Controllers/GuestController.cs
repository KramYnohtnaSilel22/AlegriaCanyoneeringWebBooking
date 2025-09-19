using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
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
using System.Threading.Tasks;
   

namespace AlegriaCanyoneeringWebBooking.Controllers
{
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

        // GET: Anticipate (Display only 1 guest per batch)
        public async Task<IActionResult> NewBooking()
        {
            await PopulateDropdowns(); // Load dropdowns for operators/nationalities

            // Load all anticipated/reserved guests
            var allGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                .ToListAsync();

            // Group by batch and only take 1 guest (leader) per batch
            var grouped = allGuests.GroupBy(g => g.Batch);

            var filteredGuests = grouped
                .Select(group => group
                    .OrderBy(g => g.GuestId) // Ensure the leader is the first guest (smallest ID)
                    .First()) // Only the first guest per batch
                .ToList();

            var model = new GuestListViewModel
            {
                NewGuest = new Guest(),
                ReservedGuests = filteredGuests // Only batch leaders will be here
            };

            return View(model);
        }



        // Controller action to get batch guests
        [HttpGet]
        public async Task<IActionResult> GetBatchGuests(string batch)
        {
            if (string.IsNullOrEmpty(batch))
                return BadRequest("Batch ID is required.");

            var groupMembers = await _context.Guests
                .Where(g => g.Batch == batch) // Get all guests in the same batch
                .OrderBy(g => g.GuestId) // Sort by GuestId or another field
                .ToListAsync();

            return PartialView("_BatchGroupMembers", groupMembers); // Return a partial view with the group members
        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(GuestListViewModel model)
        {
            if (ModelState.IsValid)
            {
                var guest = model.NewGuest;
                guest.BookingStatus = "anticipated";
                guest.RFID = GenerateRFID();
                guest.Month = DateTime.Today.ToString("yyyy-MM");
                guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");

                // ✅ Correct way to find available batch
                var batchToJoin = await _context.Guests
                    .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                    .GroupBy(g => g.Batch)
                    .Where(g => g.Count() < 5)
                    .OrderByDescending(g => g.Max(x => x.GuestId))
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync();

                string newBatchId = DateTime.Now.ToString("yyyyMMddHHmmss");

                guest.Batch = !string.IsNullOrEmpty(batchToJoin) ? batchToJoin : newBatchId;

                // Save guest
                _context.Add(guest);
                await _context.SaveChangesAsync();

                // Recalculate NumberOfGuests per batch
                var allGuests = await _context.Guests
                    .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                    .ToListAsync();

                var grouped = allGuests.GroupBy(g => g.Batch);
                foreach (var group in grouped)
                {
                    int count = group.Count();
                    foreach (var g in group)
                    {
                        g.NumberOfGuests = count;
                    }
                }
                await _context.SaveChangesAsync();

                // Display 1 guest per batch only
                var filteredGuests = grouped
                    .SelectMany(group =>
                        group
                            .OrderBy(g => g.GuestId)
                            .Select((g, index) => new { Guest = g, Index = index })
                            .Where(x => x.Index == 0) // batch leader only
                            .Select(x => x.Guest)
                    )
                    .ToList();

                model.ReservedGuests = filteredGuests;

                TempData["ToastMessage"] = "Guest added successfully!";
                TempData["ToastType"] = "success";

                await PopulateDropdowns();
                return View(model);
            }

            // On error
            await PopulateDropdowns();

            model.ReservedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                .ToListAsync();

            return View(model);
        }




        // Your existing RFID generator helper
        private string GenerateRFID()
        {
            return "RFID" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
        }




        private async Task PopulateDropdowns()
        {
            var operators = await _context.OperatorLists
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                }).ToListAsync();

            var nationalities = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.NatName
                }).ToListAsync();

            ViewBag.OperatorList = operators.Any()
                ? operators
                : new List<SelectListItem> { new SelectListItem { Text = "No operators available", Value = "" } };

            ViewBag.NationalityList = nationalities.Any()
                ? nationalities
                : new List<SelectListItem> { new SelectListItem { Text = "No nationalities available", Value = "" } };
        }



        //// GET: Guest/ReserveDetails/5
        //public async Task<IActionResult> ReserveDetails(int id)
        //{
        //    // Eagerly load the related entities
        //    var guest = await _context.Guests
        //        .Include(g => g.Operator)
        //        .Include(g => g.Nationality)
        //        .Include(g => g.Driver) // ✅ Include Driver
        //        .FirstOrDefaultAsync(g => g.GuestId == id);

        //    if (guest == null)
        //    {
        //        return NotFound();
        //    }
        //    // ✅ Load drivers for dropdown
        //    ViewBag.DriverList = await _context.Drivers
        //        .Select(d => new SelectListItem
        //        {
        //            Value = d.DriverId.ToString(),
        //            Text = d.FName
        //        })
        //        .ToListAsync();


        //    return View(guest);
        //}

        public async Task<IActionResult> ReserveDetails(int id)
        {
            // Get the main guest
            var mainGuest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .FirstOrDefaultAsync(g => g.GuestId == id);

            if (mainGuest == null)
            {
                return NotFound();
            }

            // Get 4 other guests in the same batch, excluding main guest
            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.Batch == mainGuest.Batch && g.GuestId != mainGuest.GuestId)
                .OrderBy(g => g.GuestId)
                .Take(4)
                .ToListAsync();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
            };

            return View(model);
        }


        // GET: Guest/EditReserve/5
        public async Task<IActionResult> EditReserve(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.OperatorList)
                .FirstOrDefaultAsync(g => g.GuestId == id);

            if (guest == null)
            {
                return NotFound();
            }

            // Operators dropdown
            var operators = await _context.OperatorLists
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToListAsync();

            if (!operators.Any())
            {
                operators = new List<SelectListItem>
            {
                new SelectListItem { Text = "No operators available", Value = "" }
            };
            }
            ViewBag.OperatorList = operators;

            // Nationalities dropdown
            var nationalities = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),     // ✅ correct property
                    Text = n.NatName             // ✅ correct property
                })
                .ToListAsync();

            if (!nationalities.Any())
            {
                nationalities = new List<SelectListItem>
            {
                new SelectListItem { Text = "No countries available", Value = "" }
            };
            }
            ViewBag.NationalityList = nationalities;

            return View(guest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReserve(int id, Guest formGuest)
        {
            if (id != formGuest.GuestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // ✅ Fetch the original guest from DB
                    var guest = await _context.Guests.FindAsync(id);
                    if (guest == null)
                    {
                        return NotFound();
                    }

                    // ✅ Update only editable fields
                    guest.Fullname = formGuest.Fullname;
                    guest.Age = formGuest.Age;
                    guest.Gender = formGuest.Gender;
                    guest.NationalityType = formGuest.NationalityType;
                    guest.NationalityId = formGuest.NationalityId;
                    guest.OperatorId = formGuest.OperatorId;
                    guest.Date = formGuest.Date;
                    guest.ArrivalDate = formGuest.ArrivalDate;
                    guest.Month = formGuest.Month;
                    guest.DateShort = formGuest.DateShort;
                    guest.BookingStatus = formGuest.BookingStatus;
                    guest.NumberOfGuests = formGuest.NumberOfGuests;
                    // Keep RFID or regenerate if needed
                    guest.RFID = string.IsNullOrEmpty(formGuest.RFID) ? GenerateRFID() : formGuest.RFID;

                    // Note: You probably want to skip Batch or RFID if they are system-generated

                    await _context.SaveChangesAsync();

                    TempData["ToastMessage"] = "Guest updated successfully!";
                    TempData["ToastType"] = "success";
                    return RedirectToAction("Anticipate");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Guests.Any(e => e.GuestId == formGuest.GuestId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Repopulate dropdowns in case of validation failure
            ViewBag.OperatorList = await _context.OperatorLists
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToListAsync();

            ViewBag.NationalityList = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.NatName
                })
                .ToListAsync();

            return View(formGuest);
        }


        //public async Task<IActionResult> reservebooking()
        //{
        //    var savedGuests = await _context.Guests
        //        .Include(g => g.OperatorList)
        //        .Include(g => g.Nationality)
        //        .Where(g => g.BookingStatus == "reserved" || g.BookingStatus == "confirmed")
        //        .ToListAsync();

        //    // Debug: Output the count
        //    System.Diagnostics.Debug.WriteLine($"Guest count: {savedGuests.Count}");

        //    var model = new GuestListViewModel
        //    {
        //        ReservedGuests = savedGuests
        //    };
        //    return View(model);
        //}
        // Controller: BookingController.cs

        // === Show all reserved/confirmed guests and a single Batch QR ===
        public async Task<IActionResult> reservebooking()
        {
            // pull all reserved/confirmed guests
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

            // individual guest QR codes
            foreach (var guest in reservedGuests)
            {
                guest.QRText = GenerateQRText(guest);
                guest.QRBase64 = GenerateQRCodeBase64(guest.QRText);
            }

            // assume they all share the same batch
            string batchCode = reservedGuests.First().Batch;
            string batchQrBase64 = GenerateQRCodeBase64($"BATCH:{batchCode}");

            var vm = new GuestListViewModel
            {
                ReservedGuests = reservedGuests,
                BatchQrBase64 = batchQrBase64
            };
            return View(vm);
        }

        // === Scan URL: /Guests/ByBatch/{batch} ===
        [HttpGet("Guests/ByBatch/{batch}")]
        public async Task<IActionResult> ByBatch(string batch)
        {
            var guests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Include(g => g.Driver)
                .Include(g => g.Guide)
                .Where(g => g.Batch == batch)
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            return View("BatchGuests", guests);
        }

        // ---------- QR Helpers ----------

        private string GenerateQRText(Guest guest)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Guest Details");
            sb.AppendLine("------------------------");
            sb.AppendLine($"ID           : {guest.GuestId}");
            sb.AppendLine($"Full Name    : {guest.Fullname}");
            sb.AppendLine($"Age          : {guest.Age}");
            sb.AppendLine($"Gender       : {guest.Gender}");
            sb.AppendLine($"Nationality  : {guest.NationalityType}");
            sb.AppendLine($"Guests Count : {guest.NumberOfGuests}");
            sb.AppendLine($"Nationality Status : {guest.Nationality?.NatName}");
            sb.AppendLine($"Operator     : {guest.OperatorList?.BusinessName ?? "N/A"}");
            sb.AppendLine($"Driver       : {guest.Driver?.FName ?? "None"}");
            sb.AppendLine($"Guide        : {guest.Guide?.FName ?? "None"}");
            sb.AppendLine($"Booking Date : {guest.Date:yyyy-MM-dd}");
            sb.AppendLine($"Arrival Date : {guest.ArrivalDate:yyyy-MM-dd}");
            sb.AppendLine($"Month        : {guest.Month}");
            sb.AppendLine($"Batch        : {guest.Batch}");
            sb.AppendLine($"RFID         : {guest.RFID}");
            sb.AppendLine($"Status       : {guest.BookingStatus?.ToUpper() ?? "N/A"}");
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


        //public async Task<IActionResult> ScanQR(int id)
        //{
        //    var guest = await _context.Guests
        //        .Include(g => g.OperatorList)
        //        .Include(g => g.Nationality)
        //        .Include(g => g.Guide)
        //        .Include(g => g.Driver)
        //        .FirstOrDefaultAsync(g => g.GuestId == id);

        //    if (guest == null) return NotFound();

        //    // generate QR text + QR image (base64)
        //    string qrText = GenerateQRText(guest);
        //    string qrBase64 = GenerateQRCodeBase64(qrText);

        //    ViewBag.QRCodeImage = qrBase64;
        //    ViewBag.QRData = qrText;

        //    return View(guest);
        //}


        //public IActionResult DownloadQr(int id)
        //{
        //    var guest = _context.Guests.FirstOrDefault(g => g.GuestId == id);
        //    if (guest == null) return NotFound();

        //    // Generate QR image content for download
        //    string qrBase64 = GenerateQRCodeBase64($"GuestID:{guest.GuestId}, Name:{guest.Fullname}");
        //    if (qrBase64.StartsWith("data:"))
        //        qrBase64 = qrBase64.Substring(qrBase64.IndexOf(",") + 1);

        //    var bytes = Convert.FromBase64String(qrBase64);

        //    // This will trigger a file download on mobile once the QR link is opened
        //    return File(bytes, "image/png", $"{guest.Fullname}_QRCode.png");
        //}




        // POST: Guest/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            _context.Guests.Remove(guest);
            await _context.SaveChangesAsync();

            // Return a JSON response to let the JavaScript know it was successful
            return Json(new { success = true, redirectUrl = Url.Action(nameof(reservebooking)) });
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ConfirmQR(int id, int? driverId)
        //{
        //    var guest = await _context.Guests
        //        .Include(g => g.Nationality)
        //        .Include(g => g.OperatorList)
        //        .Include(g => g.Driver)
        //        .FirstOrDefaultAsync(g => g.GuestId == id);

        //    if (guest == null)
        //    {
        //        return NotFound();
        //    }

        //    // Assign Driver if provided
        //    if (driverId.HasValue)
        //    {
        //        var driver = await _context.Drivers.FindAsync(driverId.Value);
        //        if (driver != null)
        //        {
        //            guest.DriverId = driverId;
        //        }
        //    }

        //    // ✅ FIXED: Load driver after potential assignment
        //    if (guest.DriverId.HasValue)
        //    {
        //        guest.Driver = await _context.Drivers.FindAsync(guest.DriverId.Value);
        //    }

        //    // ✅ FIXED: Generate QR Data with proper driver display
        //    string qrData =
        //        $"Guest Details\n" +
        //        $"-----------------------------------\n" +
        //        $"ID             : {guest.GuestId}\n" +
        //        $"Full Name      : {guest.Fullname}\n" +
        //        $"Age            : {guest.Age}\n" +
        //        $"Gender         : {guest.Gender}\n" +
        //        $"Nationality    : {guest.NationalityType}\n" +
        //        $"No. of Guests  : {guest.NumberOfGuests}\n" +
        //        $"Nat. Status    : {guest.NationalityId}\n" +
        //        $"Operator       : {guest.OperatorList?.BusinessName ?? "N/A"}\n" +
        //        $"Driver         : {(guest.Driver != null ? $"{guest.Driver.RefId} - {guest.Driver.FName} {guest.Driver.LName}" : "None")}\n" +
        //        $"Booking Date   : {guest.Date}\n" +
        //        $"Arrival Date   : {guest.ArrivalDate}\n" +
        //        $"Month          : {guest.Month}\n" +
        //        $"Batch          : {guest.Batch}\n" +
        //        $"RFID           : {guest.RFID}\n" +
        //        $"Status         : confirm\n";

        //    guest.QrCode = GenerateQRCodeBase64(qrData);
        //    guest.BookingStatus = "confirm";

        //    try
        //    {
        //        _context.Update(guest);
        //        await _context.SaveChangesAsync();

        //        TempData["SuccessMessage"] = "Guest confirmed with Driver!";
        //        return RedirectToAction(nameof(reservebooking));
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        _logger.LogError(ex, "Error confirming guest with ID: {GuestId}", id);
        //        TempData["ErrorMessage"] = "Error confirming guest. Please try again.";
        //        return RedirectToAction(nameof(ScanQR), new { id = id });
        //    }
        //}

        private int GetCurrentOperatorId()
        {
            int operatorId;
            if (int.TryParse(User.Identity?.Name, out operatorId))
            {
                return operatorId;
            }
            return 0;
        }
        [HttpGet]
        public async Task<IActionResult> GetNationalities()
        {
            try
            {
                var nationalities = await _context.Nationalities
                    .Where(n => n.NatName != "Within Cebu Province" &&
                                n.NatName != "Outside Cebu Province")
                    .OrderBy(n => n.NatName)
                    .Select(n => n.NatName)
                    .ToListAsync();

                return Json(nationalities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpPost]
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
                    .OrderBy(g => g.GuestId)
                    .ToListAsync();

                int index = batchGuests.FindIndex(g => g.GuestId == id);

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
        public async Task<IActionResult> ConfirmBooking(int id, List<int> driverIds, List<int> guideIds)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            // ✅ Store selected driver and guide IDs(you might need to modify your Guest model)
            // For now, we'll store the first selected IDs for backward compatibility
            if (driverIds != null && driverIds.Count > 0)
            {
                guest.DriverId = driverIds.First();
                // If you want to store all selected drivers, you'll need a separate relationship table
            }

            if (guideIds != null && guideIds.Count > 0)
            {
                guest.GuideId = guideIds.First();
                // If you want to store all selected guides, you'll need a separate relationship table
            }

            // ✅ Set booking status to confirmed
            guest.BookingStatus = "reserved";

            _context.Update(guest);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(reservebooking)); // Go back to Final Bookings
        }

        [HttpPost]
        public async Task<IActionResult> FinalBookingBatch(string BatchCode)
        {
            // Get all guests in the batch
            var guests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.Batch == BatchCode)
                .ToListAsync();

            if (guests == null || !guests.Any())
                return NotFound();

            // Finalize all guests by setting BookingStatus
            foreach (var guest in guests)
            {
                guest.BookingStatus = "finalized";
            }
            await _context.SaveChangesAsync();

            // Prepare a view model listing all finalized guests in the batch
            var model = new GuestListViewModel
            {
                ReservedGuests = guests
            };

            // Return a view that shows the finalized guest list for this batch
            return View("FinalBookingBatch", model);
        }


        public async Task<IActionResult> saveguest()
        {
            var reservedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.BookingStatus == "reserved" || g.BookingStatus == "anticipated")
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            var model = new GuestListViewModel
            {
                ReservedGuests = reservedGuests
            };

            return View("saveguest", model);
        }



        private bool GuestExists(int id)
        {
            return _context.Guests.Any(e => e.GuestId == id);
        }
    }
}


