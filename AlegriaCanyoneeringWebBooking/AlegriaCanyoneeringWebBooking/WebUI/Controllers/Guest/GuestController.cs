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
using Microsoft.Extensions.Caching.Memory;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Http;


namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin,Operator")]
    public class GuestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;
        private readonly IMemoryCache _cache;
        public GuestController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<GuestController> logger, IMemoryCache cache)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _cache = cache;
            // Test connection
            if (!_context.Database.CanConnect())
            {
                throw new Exception("Cannot connect to database. Please check your connection string.");
            }

            _cache = cache;
        }


        //[HttpPost]
        //public async Task<IActionResult> GetGuestsData()
        //{
        //    var draw = Request.Form["draw"].FirstOrDefault();
        //    var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
        //    var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
        //    var search = Request.Form["search[value]"].FirstOrDefault();

        //    // ✅ Get current user's info
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userRole = User.FindFirstValue(ClaimTypes.Role);

        //    int? currentOperatorId = null;
        //    if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
        //        currentOperatorId = parsedId;

        //    // 🔹 Base query - no Include for performance
        //    var query = _context.Guests
        //        .AsNoTracking()
        //        .Where(g =>
        //            g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated &&
        //            g.BookingStatus != (int)Guest.BookingStatusEnum.canceled);

        //    // ✅ Filter by operator role
        //    if (currentOperatorId.HasValue)
        //    {
        //        query = query.Where(g => g.OperatorId == currentOperatorId.Value);
        //    }

        //    // 🔍 Apply search filter (on simple fields only)
        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        query = query.Where(g =>
        //            g.Fullname.Contains(search) ||
        //            g.Batch.Contains(search));
        //    }

        //    // 🧮 Total filtered count
        //    var recordsTotal = await query.CountAsync();

        //    // ⏱ Group + Paginate
        //    var groupedData = await query
        //        .OrderBy(g => g.OperatorId)
        //        .ThenBy(g => g.Batch)
        //        .GroupBy(g => new { g.Batch, g.OperatorId })
        //        .Select(grp => new
        //        {
        //            grp.Key.Batch,
        //            grp.Key.OperatorId,
        //            TotalGuests = grp.Count(x => x.BookingStatus != (int)Guest.BookingStatusEnum.canceled),
        //            ArrivalDate = grp.Min(x => x.ArrivalDate),
        //            MainGuestId = grp.OrderBy(x => x.Id).First().Id
        //        })
        //        .Skip(start)
        //        .Take(length)
        //        .ToListAsync();

        //    // 💾 Get cached operator names
        //    var operators = await _cache.GetOrCreateAsync("OperatorsDictionary", async entry =>
        //    {
        //        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

        //        return await _context.Operators
        //            .AsNoTracking()
        //            .Select(o => new { o.Id, o.BusinessName })
        //            .ToDictionaryAsync(o => o.Id, o => o.BusinessName);
        //    });

        //    // ✅ Flatten data with safe lookup
        //    var result = groupedData.Select(g => new
        //    {
        //        id = g.MainGuestId,
        //        batch = g.Batch,
        //        operatorName = (g.OperatorId.HasValue && operators.TryGetValue(g.OperatorId.Value, out var name))
        //            ? name
        //            : "No Operator",
        //        totalGuests = g.TotalGuests,
        //        arrivalDate = g.ArrivalDate,
        //        bookingStatus = "anticipated"
        //    }).ToList();

        //    // ✅ Return data in DataTables format
        //    return Json(new
        //    {
        //        draw,
        //        recordsFiltered = recordsTotal,
        //        recordsTotal,
        //        data = result
        //    });
        //}
        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            // Get current user's info
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            // Base query with join to Operators to enable searching operator name
            var query = from g in _context.Guests.AsNoTracking()
                        join o in _context.Operators.AsNoTracking()
                            on g.OperatorId equals o.Id into opGroup
                        from operatorItem in opGroup.DefaultIfEmpty()
                        where g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated
                              && g.BookingStatus != (int)Guest.BookingStatusEnum.canceled
                        select new
                        {
                            Guest = g,
                            OperatorName = operatorItem != null ? operatorItem.BusinessName : "No Operator"
                        };

            // Filter by operator role if applicable
            if (currentOperatorId.HasValue)
            {
                query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);
            }

            // Apply search filter including operator name
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.Guest.Fullname.ToLower().Contains(search) ||
                    x.Guest.Batch.ToLower().Contains(search) ||
                    x.OperatorName.ToLower().Contains(search));
            }

            // Group + paginate in DB
            var groupedData = await query
                .OrderBy(x => x.Guest.OperatorId)
                .ThenBy(x => x.Guest.Batch)
                .GroupBy(x => new { x.Guest.Batch, x.Guest.OperatorId, x.OperatorName })
                .Select(grp => new
                {
                    Batch = grp.Key.Batch,
                    OperatorId = grp.Key.OperatorId,
                    OperatorName = grp.Key.OperatorName,
                    TotalGuests = grp.Count(x => x.Guest.BookingStatus != (int)Guest.BookingStatusEnum.canceled),
                    ArrivalDate = grp.Min(x => x.Guest.ArrivalDate),
                    MainGuestId = grp.OrderBy(x => x.Guest.Id).First().Guest.Id
                })
                .Skip(start)
                .Take(length)
                .ToListAsync();

            // Get total count for filtering
            var recordsTotal = await query.CountAsync();

            // Prepare result
            var result = groupedData.Select(g => new
            {
                id = g.MainGuestId,
                batch = g.Batch,
                operatorName = g.OperatorName ?? "No Operator",
                totalGuests = g.TotalGuests,
                arrivalDate = g.ArrivalDate,
                bookingStatus = "anticipated"
            }).ToList();

            // Return data in DataTables format
            return Json(new
            {
                draw,
                recordsFiltered = recordsTotal,
                recordsTotal,
                data = result
            });
        }



        /// this is working method

        //[HttpPost]
        //public async Task<IActionResult> GetGuestsData()
        //{
        //    var draw = Request.Form["draw"].FirstOrDefault();
        //    var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
        //    var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
        //    var search = Request.Form["search[value]"].FirstOrDefault();

        //    // Base query - use AsNoTracking() for large data performance
        //    var query = _context.Guests
        //        .AsNoTracking()
        //        .Include(g => g.OperatorList)
        //        .Where(g => g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated
        //                    && g.BookingStatus != 3);

        //    // Apply search filter
        //    if (!string.IsNullOrEmpty(search))
        //    {
        //        query = query.Where(g =>
        //            g.Fullname.Contains(search) ||
        //            g.Batch.Contains(search) ||
        //            (g.OperatorList != null && g.OperatorList.BusinessName.Contains(search)));
        //    }

        //    // Get total count
        //    var recordsTotal = await query.CountAsync();

        //    // Group data for performance
        //    var groupedData = await query
        //        .OrderBy(g => g.OperatorId)
        //        .ThenBy(g => g.Batch)
        //        .GroupBy(g => new { g.Batch, g.OperatorId })
        //        .Select(grp => new
        //        {
        //            grp.Key.Batch,
        //            grp.Key.OperatorId,
        //            TotalGuests = grp.Count(x => x.BookingStatus != (int)Guest.BookingStatusEnum.canceled),
        //            ArrivalDate = grp.Min(x => x.ArrivalDate),
        //            MainGuestId = grp.OrderBy(x => x.Id).First().Id
        //        })
        //        .Skip(start)
        //        .Take(length)
        //        .ToListAsync();

        //    // Cache operator names for faster lookup
        //    var operators = await _context.Operators
        //        .AsNoTracking()
        //        .Select(o => new { o.Id, o.BusinessName })
        //        .ToDictionaryAsync(o => o.Id, o => o.BusinessName);

        //    // Map operator names and flatten result
        //    var result = groupedData.Select(g => new
        //    {
        //        id = g.MainGuestId,
        //        batch = g.Batch,
        //        operatorName = operators.TryGetValue(g.OperatorId ?? 0, out var name) ? name : "No Operator",
        //        totalGuests = g.TotalGuests,
        //        arrivalDate = g.ArrivalDate,
        //        bookingStatus = "anticipated"
        //    }).ToList();

        //    // Return JSON compatible with DataTables
        //    return Json(new
        //    {
        //        draw,
        //        recordsFiltered = recordsTotal,
        //        recordsTotal,
        //        data = result
        //    });
        //}





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
            return PartialView("_BookingDetailsPartial", vm);
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

            // ✅ FIX: Create SelectList with "N/A" display for empty BusinessName
            var operatorSelectList = operators.Select(o => new
            {
                Id = o.Id,
                DisplayName = string.IsNullOrWhiteSpace(o.BusinessName) ? "No Operator" : o.BusinessName
            }).ToList();

            ViewBag.OperatorList = new SelectList(operatorSelectList, "Id", "DisplayName");


            // Fetch anticipated guests
            var anticipatedGuests = await _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated)
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
                            RFID = grp.Count(g => g.BookingStatus != (int)Guest.BookingStatusEnum.canceled),
                            ArrivalDate = first.ArrivalDate, // This is still Unix timestamp
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
                    model.NewGuest.ArrivalDate = batchDetails.ArrivalDate; // Still in Unix timestamp format
                    model.NewGuest.Area = batchDetails.Area;
                    model.NewGuest.Batch = batchDetails.Batch;

                }
            }

            // Convert ArrivalDate (Unix timestamp) to a human-readable format for the view
            if (!string.IsNullOrEmpty(model.NewGuest.ArrivalDate) && long.TryParse(model.NewGuest.ArrivalDate, out long unixTimestamp))
            {
                model.NewGuest.ArrivalDate = ConvertUnixToDateTime(unixTimestamp).ToString("MMMM dd, yyyy"); // Human-readable format
            }

            ViewBag.IsReadonly = !string.IsNullOrEmpty(batch);

            return View(model);
        }

        // Helper method to convert Unix timestamp to DateTime
        private DateTime ConvertUnixToDateTime(long unixTimestamp)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
            return dateTime.ToLocalTime();  // Convert to server's local time
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(GuestListViewModel model, string batch = null, int? id = null, IFormFile Photo = null)
        {
            string batchId = !string.IsNullOrEmpty(batch) ? batch : await GenerateBatchCode();

            if (!string.IsNullOrWhiteSpace(model.BatchGuestsJson))
            {
                var batchGuests = JsonSerializer.Deserialize<List<Guest>>(model.BatchGuestsJson);

                if (batchGuests != null && batchGuests.Count > 0)
                {
                    int insertedCount = 0;
                    string generatedRFIDCode = GenerateRFIDCode(); // Will be reused for the image

                    foreach (var guest in batchGuests)
                    {
                        guest.BookingStatus = (int)Guest.BookingStatusEnum.anticipated;  // 0 = anticipated
                        guest.Batch = batchId;
                        guest.RFID = 1;
                        guest.RFIDCode = generatedRFIDCode;
                        guest.Year = guest.Year ?? DateTime.Today.Year.ToString();
                        guest.Month = DateTime.Today.ToString("MMMM");

                        // Generate the current Unix timestamp
                        long unixTimestamp = GetCurrentUnixTimestamp();
                        guest.ArrivalDate = unixTimestamp.ToString(); // Save Unix timestamp as string
                        Console.WriteLine($"Generated Unix Timestamp: {guest.ArrivalDate}");  // Debugging line

                        guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");
                        guest.Date = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");

                        _context.Guests.Add(guest);
                        insertedCount++;
                    }

                    // Save guests first
                    await _context.SaveChangesAsync();

                    // Save batch regardless of photo upload
                    int noOfLocal = batchGuests.Count(g => g.NationalityType?.ToLower() == "local");
                    int noOfForeign = batchGuests.Count(g => g.NationalityType?.ToLower() == "foreign");
                    int totalGuests = noOfLocal + noOfForeign;

                    string arrivalDate = batchGuests.FirstOrDefault()?.ArrivalDate ?? GetCurrentUnixTimestamp().ToString();


                    var batchRecord = new Batch
                    {
                        OperatorId = batchGuests.FirstOrDefault()?.OperatorId ?? 0,
                        NoOfLocalGuest = noOfLocal,
                        NoOfForeignGuest = noOfForeign,
                        NoOfTGuide = 0,
                        NoOfMDriver = 0,
                        TotalNoOfGuest = totalGuests,
                        ArrivalDate = arrivalDate
                    };

                    try
                    {
                        _context.Batches.Add(batchRecord);
                        await _context.SaveChangesAsync();
                        Console.WriteLine("✅ Batch saved: " + batchRecord.BatchId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("❌ Failed to save batch: " + ex.Message);
                    }

                    // Now handle photo upload if any
                    if (Photo != null && Photo.Length > 0)
                    {
                        using (var ms = new MemoryStream())
                        {
                            await Photo.CopyToAsync(ms);
                            var guestImage = new GuestImage
                            {
                                WristbondGuestCode = generatedRFIDCode,
                                Image = ms.ToArray()
                            };

                            _context.Add(guestImage);
                            await _context.SaveChangesAsync();
                        }
                    }

                    TempData["ToastMessage"] = "Guests added successfully";
                    TempData["ToastType"] = "success";


                    return RedirectToAction("SaveGuest"); // ✅ Clean redirect, no query string

                }

                TempData["ToastMessage"] = "Please add at least one guest before saving!";
                TempData["ToastType"] = "warning";
            }

            await PopulateDropdowns();
            return View(model);
        }


        // Helper method to generate the current Unix timestamp
        private long GetCurrentUnixTimestamp()
        {
            DateTime utcNow = DateTime.UtcNow; // Get current UTC time
            return (long)(utcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
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
            var bytes = new byte[26];  // 26 bytes * 2 characters per byte = 52 characters
            new Random().NextBytes(bytes);
            string rfidCode = BitConverter.ToString(bytes).Replace("-", " ");

            // Trim to exactly 51 characters
            return rfidCode.Substring(0, 51);
        }


        public async Task<IActionResult> SaveGuest()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
            {
                currentOperatorId = parsedId;
            }

            var anticipatedGuestsQuery = _context.Guests
                .Include(g => g.OperatorList)
                .Where(g => g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated
                            && g.BookingStatus != 3); // <-- exclude confirmed

            if (currentOperatorId.HasValue)
            {
                anticipatedGuestsQuery = anticipatedGuestsQuery
                    .Where(g => g.OperatorId == currentOperatorId.Value);
            }

            var anticipatedGuests = await anticipatedGuestsQuery.ToListAsync();

            var grouped = anticipatedGuests
                .GroupBy(g => g.OperatorId)
                .Select(grp =>
                {
                    var first = grp.First();
                    return new Guest
                    {
                        OperatorId = grp.Key,
                        OperatorList = first.OperatorList,
                        RFID = grp.Count(x => x.BookingStatus != (int)Guest.BookingStatusEnum.canceled),
                        ArrivalDate = first.ArrivalDate,
                        BookingStatus = first.BookingStatus
                    };
                })
                .ToList();

            var model = new GuestListViewModel
            {
                ReservedGuests = grouped
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



        private string GenerateBatchCode(int? operatorId)
        {
            return $"OP{operatorId}-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
        }
        public IActionResult BookingDetailsPartial(int id)
        {
            var model = new GuestDetailsViewModel
            {
                Guest = _context.Guests.FirstOrDefault(g => g.Id == id),
                GuestsInBatch = _context.Guests.Where(g => g.Batch == _context.Guests.FirstOrDefault(x => x.Id == id).Batch).ToList()
            };
            return PartialView("_BookingDetailsPartial", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGuest(int GuestId)
        {
            var guest = await _context.Guests.FindAsync(GuestId);
            if (guest == null)
                return Json(new { success = false, message = "Guest not found" });

            // Store batch before deleting
            var batch = guest.Batch;

            // Permanent delete the guest
            _context.Guests.Remove(guest);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "An error occurred while deleting the guest." });
            }

            // Recalculate RFID for remaining guests in the batch
            var updatedGuestCount = await _context.Guests
                .Where(g => g.Batch == batch)
                .CountAsync();

            var guestsInBatch = await _context.Guests
                .Where(g => g.Batch == batch)
                .ToListAsync();

            // Update RFID for all guests in the batch
            foreach (var g in guestsInBatch)
            {
                g.RFID = updatedGuestCount;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "An error occurred while updating RFID for guests." });
            }

            return Json(new { success = true, guestId = GuestId });
        }
        public async Task<IActionResult> UpdateStatus(int id, int status)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            // Change the status of the selected guest to Reserved (2) automatically
            guest.BookingStatus = 2;  // Set status to Reserved (2)

            // Fetch all guests in the same batch as the current guest
            var operatorGuests = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.BookingStatus != 2) // Don't update already reserved guests
                .ToListAsync();

            if (!operatorGuests.Any())
            {
                return NotFound("No other guests found for this batch.");
            }

            // Update the status of all guests in this batch to Reserved (2)
            foreach (var g in operatorGuests)
            {
                g.BookingStatus = 2;  // Set status to Reserved (2)
            }

            try
            {
                // Save all changes
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency conflict: guests may have been modified by another process.");
            }

            // Redirect back to the ReserveBooking page
            return RedirectToAction("ReserveBooking", "Reserve");
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

