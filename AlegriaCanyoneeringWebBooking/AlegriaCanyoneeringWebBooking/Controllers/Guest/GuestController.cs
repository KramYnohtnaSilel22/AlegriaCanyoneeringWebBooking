





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

using System.Globalization; // Add this at the top

using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Claims;


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
        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.Guests
                .Where(g => g.BookingStatus == "anticipated");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g =>
                    g.Fullname.Contains(search) ||
                    g.Batch.Contains(search)
                );
            }

            // ✅ Get a local copy of tbl_operator_mobile to join later
            var operators = await _context.Operators
                .Select(o => new { o.Id, o.BusinessName })
                .ToListAsync();

            // ✅ Group the guest records
            var groupedRaw = await query
                .GroupBy(g => new { g.Batch, g.OperatorId })
                .Select(grp => new
                {
                    Batch = grp.Key.Batch,
                    OperatorId = grp.Key.OperatorId,
                    TotalGuests = grp.Count(g => g.BookingStatus != "canceled"),
                    ArrivalDate = grp.Min(x => x.ArrivalDate),
                    Status = "anticipated",
                    MainGuestId = grp.OrderBy(x => x.Id).First().Id
                })
                .OrderBy(g => g.OperatorId)
                .ThenBy(g => g.Batch)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            // ✅ Get distinct total records (for DataTables)
            var recordsTotal = await query
                .Select(g => new { g.Batch, g.OperatorId })
                .Distinct()
                .CountAsync();

            // ✅ Map operator names from tbl_operator_mobile
            var grouped = groupedRaw.Select(g =>
            {
                var businessName = operators
                    .FirstOrDefault(o => o.Id == g.OperatorId)?.BusinessName ?? "N/A";

                return new
                {
                    id = g.MainGuestId,
                    batch = g.Batch,
                    operatorName = businessName,
                    totalGuests = g.TotalGuests,
                    arrivalDate = g.ArrivalDate,
                    bookingStatus = g.Status
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

        //[HttpPost]
        //public async Task<IActionResult> GetGuestsData()
        //{
        //    try
        //    {
        //        var draw = Request.Form["draw"].FirstOrDefault();
        //        var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
        //        var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
        //        var search = Request.Form["search[value]"].FirstOrDefault();

        //        var query = _context.Guests
        //            .Include(g => g.OperatorList)
        //            .Where(g => g.BookingStatus == "anticipated");

        //        // (Optionally: filter by operator role, as before)

        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            query = query.Where(g =>
        //                g.Fullname.Contains(search) ||
        //                g.Batch.Contains(search) ||
        //                (g.OperatorList != null && g.OperatorList.BusinessName.Contains(search))
        //            );
        //        }

        //        var groupedRaw = await query
        //            .GroupBy(g => new { g.Batch, g.OperatorId })
        //            .Select(grp => new
        //            {
        //                Batch = grp.Key.Batch,
        //                OperatorId = grp.Key.OperatorId,
        //                OperatorName = grp.First().OperatorList != null
        //                    ? grp.First().OperatorList.BusinessName : "N/A",
        //                TotalGuests = grp.Count(g => g.BookingStatus != "canceled"),
        //                ArrivalDate = grp.Min(x => x.ArrivalDate),
        //                Status = "anticipated",
        //                MainGuestId = grp.OrderBy(x => x.Id).First().Id
        //            })
        //            .OrderBy(g => g.OperatorName)
        //            .ThenBy(g => g.Batch)
        //            .Skip(start)
        //            .Take(length)
        //            .ToListAsync();

        //        var recordsTotal = await query
        //            .Select(g => new { g.Batch, g.OperatorId })
        //            .Distinct()
        //            .CountAsync();

        //        var grouped = groupedRaw.Select(g => new
        //        {
        //            id = g.MainGuestId,
        //            batch = g.Batch,
        //            operatorName = g.OperatorName,
        //            totalGuests = g.TotalGuests,
        //            arrivalDate = g.ArrivalDate,
        //            bookingStatus = g.Status
        //        });

        //        return Json(new
        //        {
        //            draw = draw,
        //            recordsFiltered = recordsTotal,
        //            recordsTotal = recordsTotal,
        //            data = grouped
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log error ex (to your logging framework)
        //        return Json(new
        //        {
        //            draw = 0,
        //            recordsFiltered = 0,
        //            recordsTotal = 0,
        //            data = new List<object>(),
        //            error = ex.Message
        //        });
        //    }
        //}





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
            return 1;
        }

        [HttpGet]
        public async Task<IActionResult> NewBooking(string batch, int? id)
        {
            // Get current user's ID and Role from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            List<Operator> operators;

            if (userRole == "Operator")
            {
                if (int.TryParse(userId, out int operatorId))


                {
                    // Only include the logged-in operator’s own business
                    operators = await _context.Operators
                        .Where(o => o.Id == operatorId)
                        .ToListAsync();
                }
                else
                {
                    operators = new List<Operator>();
                }
            }
            else
            {
                // Admin or others: show all businesses
                operators = await _context.Operators.ToListAsync();
            }

            // Pass to View as dropdown list (Id = value, BusinessName = text)
            ViewBag.OperatorList = new SelectList(operators, "Id", "BusinessName");

            // Fetch anticipated guests
            var anticipatedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == "anticipated")
                .ToListAsync();

            // Prepare ViewModel
            var model = new GuestListViewModel
            {
                NewGuest = new Guest(),
                ReservedGuests = anticipatedGuests
                    .GroupBy(g => g.OperatorId)
                    .Select(grp =>
                    {
                        var first = grp.First();
                        return new Guest
                        {
                            OperatorId = grp.Key,
                            OperatorList = first.OperatorList,
                            RFID = grp.Count(g => g.BookingStatus != "canceled"),
                            ArrivalDate = first.ArrivalDate,
                            Date = first.Date,
                            BookingStatus = first.BookingStatus
                        };
                    }).ToList()
            };

            ViewBag.CurrentBatch = batch;
            ViewBag.MainGuestId = id;

            // Prefill data if editing a batch
            if (!string.IsNullOrEmpty(batch))
            {
                var batchDetails = anticipatedGuests
                    .Where(g => g.Batch == batch)
                    .OrderBy(g => g.Id)
                    .FirstOrDefault();

                if (batchDetails != null)
                {
                    model.NewGuest.OperatorId = batchDetails.OperatorId;
                    model.NewGuest.Date = batchDetails.Date;
                    model.NewGuest.ArrivalDate = batchDetails.ArrivalDate;
                    model.NewGuest.Area = batchDetails.Area;
                }
            }

            ViewBag.IsReadonly = !string.IsNullOrEmpty(batch);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(GuestListViewModel model, string batch = null, int? id = null)
        {
            string batchId = !string.IsNullOrEmpty(batch) ? batch : await GenerateBatchCode();

            if (!string.IsNullOrWhiteSpace(model.BatchGuestsJson))
            {
                var batchGuests = JsonSerializer.Deserialize<List<Guest>>(model.BatchGuestsJson);

                if (batchGuests != null && batchGuests.Count > 0)
                {
                    int insertedCount = 0;

                    foreach (var guest in batchGuests)
                    {
                        guest.BookingStatus = "anticipated";
                        guest.Batch = batchId;
                        guest.RFID = null; // ✅ Set to null initially, will be updated after save
                        guest.RFIDCode = guest.RFIDCode ?? GenerateRFIDCode();
                        guest.Year = guest.Year ?? DateTime.Today.Year.ToString();
                        guest.Month = DateTime.Today.ToString("MMMM");
                        guest.ArrivalDate = DateTime.Today.ToString("MMM dd, yyyy");
                        guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");
                        guest.Date = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");

                        _context.Guests.Add(guest);
                        insertedCount++;
                    }

                    if (insertedCount > 0)
                    {
                        await _context.SaveChangesAsync(); // ✅ Save first to generate IDs

                        // ✅ Update RFID to match the Id for each newly added guest
                        var newlyAddedGuests = await _context.Guests
                            .Where(g => g.Batch == batchId && g.RFID == null)
                            .ToListAsync();

                        foreach (var guest in newlyAddedGuests)
                        {
                            guest.RFID = guest.Id; // ✅ Set RFID to the Id
                        }

                        await _context.SaveChangesAsync(); // ✅ Save the RFID updates

                        TempData["ToastMessage"] = $"Guests added successfully";
                        TempData["ToastType"] = "success";
                    }
                    else
                    {
                        TempData["ToastMessage"] = "Please add at least one guest before saving!";
                        TempData["ToastType"] = "warning";
                    }

                    return RedirectToAction("saveguest", new { batch = batchId, id = id });
                }

                TempData["ToastMessage"] = "Please add at least one guest before saving!";
                TempData["ToastType"] = "danger";
            }

            await PopulateDropdowns();
            return View(model);
        }

        private async Task<string> GenerateBatchCode()
        {
            // Get all batch codes that are valid integers only
            var numericBatches = _context.Guests
                .AsEnumerable()
                .Select(g => g.Batch)
                .Where(batch =>
                    !string.IsNullOrWhiteSpace(batch) &&
                    batch.All(char.IsDigit) &&
                    int.TryParse(batch, out _)) // ensure it's parseable
                .Select(batch => int.Parse(batch)) // now it's safe
                .OrderByDescending(x => x)
                .ToList();

            int nextBatchCode = 10000;

            if (numericBatches.Any())
            {
                nextBatchCode = numericBatches.First() + 1;
            }

            return nextBatchCode.ToString();
        }

        private string GenerateRFIDCode()
        {
            var bytes = new byte[6];
            new Random().NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
        //public IActionResult saveguest()
        //{
        //    var model = new GuestListViewModel
        //    {
        //        ReservedGuests = new List<Guest>() // empty list para di mag-null
        //    };

        //    return View(model);
        //}

        public async Task<IActionResult> saveguest()
        {
            var anticipatedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == "anticipated")
                .ToListAsync();

            var grouped = anticipatedGuests
                .GroupBy(g => g.OperatorId)
                .Select(grp =>
                {
                    var first = grp.First();
                    return new Guest
                    {
                        OperatorId = grp.Key,
                        OperatorList = first.OperatorList,
                        RFID = grp.Count(x => x.BookingStatus != "canceled"),
                        ArrivalDate = first.ArrivalDate,
                        BookingStatus = first.BookingStatus
                    };
                })
                .ToList();

            var model = new GuestListViewModel
            {
                ReservedGuests = grouped,




            };

            return View(model);
        }


        public async Task<IActionResult> SaveguestDetails(int id)
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
                .ToListAsync(); // Removed .Take(4)

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
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
                        n.id,      // Return Id
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

            // Get all guests with the same OperatorId and same BookingStatus as the current guest (ex: anticipated)
            var operatorGuests = await _context.Guests
// ✅ CORRECT: Updates ONLY guests with same Batch
.Where(g => g.Batch == guest.Batch && g.BookingStatus == guest.BookingStatus)
                .ToListAsync();

            if (!operatorGuests.Any())
            {
                return NotFound("No guests found for this operator.");
            }

            // **Do NOT generate a new batch code**
            // Keep the existing batch code from the first guest in the group
            string existingBatchCode = guest.Batch;

            // Update status and keep the same batch code
            foreach (var g in operatorGuests)
            {
                g.BookingStatus = status;
                g.Batch = existingBatchCode;  // Keep old batch code
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency conflict: guests may have been modified by another process.");
            }

            return RedirectToAction("reservebooking", "Reserve");



        }


        private string GenerateBatchCode(int? operatorId)
        {
            return $"OP{operatorId}-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
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
                g.RFID = updatedGuestCount;
            }

            await _context.SaveChangesAsync();


            // ✅ Redirect back to ReserveDetails
            return RedirectToAction("SaveguestDetails", "Guest", new { id = guest.Id });

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

