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
using System.Text.Json;
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
        private async Task PopulateDropdowns()
        {
            ViewBag.OperatorList = await _context.OperatorLists
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),   // always int as string
                    Text = o.BusinessName
                }).ToListAsync();

            ViewBag.NationalityList = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),            // always int as string
                    Text = n.NatName
                }).ToListAsync();
        }

        private string GenerateRFID() => "RFID" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        public async Task<IActionResult> NewBooking()
        {
            await PopulateDropdowns();
            var filteredGuests = await _context.Guests
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                .GroupBy(g => g.Batch)
                .Select(grp => grp.OrderBy(x => x.GuestId).FirstOrDefault())
                .ToListAsync();

            var model = new GuestListViewModel
            {
                NewGuest = new Guest(),
                ReservedGuests = filteredGuests
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(GuestListViewModel model)
        {
            // Handle valid batch only
            if (!string.IsNullOrWhiteSpace(model.BatchGuestsJson))
            {
                var batchGuests = JsonSerializer.Deserialize<List<Guest>>(model.BatchGuestsJson);
                if (batchGuests != null && batchGuests.Count > 0)
                {
                    string batchId = DateTime.Now.ToString("yyyyMMddHHmmss");
                    foreach (var guest in batchGuests)
                    {
                        guest.BookingStatus = "anticipated";
                        guest.Batch = batchId;
                        guest.RFID = GenerateRFID();
                        guest.Month = DateTime.Today.ToString("yyyy-MM");
                        guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");
                        _context.Guests.Add(guest);
                    }
                    await _context.SaveChangesAsync();

                    var guestsInBatch = await _context.Guests.Where(g => g.Batch == batchId).ToListAsync();
                    int count = guestsInBatch.Count;
                    guestsInBatch.ForEach(g => g.NumberOfGuests = count);
                    await _context.SaveChangesAsync();

                    TempData["ToastMessage"] = "Guests added successfully!";
                    TempData["ToastType"] = "success";
                    return RedirectToAction("saveguest");
                }
                TempData["ToastMessage"] = "Please add at least one guest before saving!";
                TempData["ToastType"] = "danger";
            }

            await PopulateDropdowns();
            return View(model);
        }

        //public async Task<IActionResult> saveguest()
        //{
        //    var reservedGuests = await _context.Guests
        //        .Where(g => g.BookingStatus == "reserved" || g.BookingStatus == "anticipated")
        //        .OrderBy(g => g.GuestId)
        //        .ToListAsync();

        //    // Only include one (the first) guest per batch
        //    var batchLeaders = reservedGuests
        //        .GroupBy(g => g.Batch)
        //        .Select(batch => batch.OrderBy(x => x.GuestId).First()) // Only first guest in batch
        //        .ToList();

        //    var model = new GuestListViewModel
        //    {
        //        ReservedGuests = batchLeaders
        //    };
        //    return View(model);
        //}

        public async Task<IActionResult> saveguest()
        {
            var reservedGuests = await _context.Guests
                .Include(g => g.OperatorList) // <-- THIS IS CRUCIAL
                .Where(g => g.BookingStatus == "reserved" || g.BookingStatus == "anticipated")
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            var batchLeaders = reservedGuests
                .GroupBy(g => g.Batch)
                .Select(batch => batch.OrderBy(x => x.GuestId).First())
                .ToList();

            var model = new GuestListViewModel
            {
                ReservedGuests = batchLeaders
            };
            return View(model);
        }


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

        // GET: FinalBookingBatch - Renders empty view (optional)
        [HttpGet]
        public IActionResult FinalBookingBatch()
        {
            return View(new GuestListViewModel { ReservedGuests = new List<Guest>() });
        }

        [HttpPost]
        public async Task<IActionResult> FinalBookingBatch(string BatchCode)
        {
            if (string.IsNullOrEmpty(BatchCode))
            {
                return BadRequest("BatchCode is required.");
            }

            // Finalize guests in batch if not already finalized
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

            // Select only the batch leader (first entered guest)
            var batchLeader = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.Batch == BatchCode)
                .OrderBy(g => g.GuestId)
                .FirstOrDefaultAsync();

            // Load all guests for view/detail/modal purposes
            var allGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .Where(g => g.Batch == BatchCode)
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            var model = new GuestListViewModel
            {
                ReservedGuests = batchLeader != null ? new List<Guest> { batchLeader } : new List<Guest>(),
                BatchGuests = new Dictionary<string, List<Guest>> { { BatchCode, allGuests } }
            };
            return View("FinalBookingBatch", model);
        }

        [HttpGet]
        public async Task<IActionResult> BookingDetails(int id)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid guest id.");
            }

            var guest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.Nationality)
                .FirstOrDefaultAsync(g => g.GuestId == id);

            if (guest == null)
            {
                return NotFound();
            }

            var guestsInBatch = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.GuestId != guest.GuestId)
                .ToListAsync();

            var model = new GuestDetailsViewModel
            {
                Guest = guest,
                GuestsInBatch = guestsInBatch
            };

            return View(model);
        }




        private bool GuestExists(int id)
        {
            return _context.Guests.Any(e => e.GuestId == id);
        }
    }
}









