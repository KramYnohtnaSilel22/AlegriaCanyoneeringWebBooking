using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
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

            if (!_context.Database.CanConnect())
                throw new Exception("Cannot connect to database. Please check your connection string.");
        }

        // ─── Helper: hydrate OperatorList onto a list of guests ───────────────────
        private async Task HydrateOperatorList(List<Guest> guests)
        {
            var operatorIds = guests
                .Where(g => g.OperatorId.HasValue)
                .Select(g => g.OperatorId!.Value)
                .Distinct()
                .ToList();

            if (!operatorIds.Any()) return;

            var operatorMap = await _context.OperatorLists
                .Where(o => operatorIds.Contains(o.OperatorId))
                .ToDictionaryAsync(o => o.OperatorId);

            foreach (var g in guests)
            {
                if (g.OperatorId.HasValue && operatorMap.TryGetValue(g.OperatorId.Value, out var op))
                    g.OperatorList = op;
            }
        }

        // ─── GetGuestsData ────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GetGuestsData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
            var search = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            var query = from g in _context.Guests.AsNoTracking()
                        join o in _context.OperatorLists.AsNoTracking()
                            on g.OperatorId equals o.OperatorId into opGroup
                        from operatorItem in opGroup.DefaultIfEmpty()
                        where g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated
                        select new
                        {
                            Guest = g,
                            OperatorName = operatorItem != null
                                ? (string.IsNullOrWhiteSpace(operatorItem.BusinessName)
                                    ? operatorItem.OwnerName
                                    : operatorItem.BusinessName)
                                : "No Operator"
                        };

            if (currentOperatorId.HasValue)
                query = query.Where(x => x.Guest.OperatorId == currentOperatorId.Value);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    (x.Guest.Fullname != null && x.Guest.Fullname.ToLower().Contains(search)) ||
                    (x.Guest.Batch != null && x.Guest.Batch.ToLower().Contains(search)) ||
                    x.OperatorName.ToLower().Contains(search));
            }

            var recordsTotal = await query.CountAsync();

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

            var result = groupedData.Select(g => new
            {
                id = g.MainGuestId,
                batch = g.Batch,
                operatorName = g.OperatorName ?? "No Operator",
                totalGuests = g.TotalGuests,
                arrivalDate = g.ArrivalDate,
                bookingStatus = "anticipated"
            }).ToList();

            return Json(new { draw, recordsFiltered = recordsTotal, recordsTotal, data = result });
        }

        // ─── GetBookingDetails ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetBookingDetails(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.NationalityEntity)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
                return Content("<p class='text-danger'>Guest not found.</p>", "text/html");

            // Manually hydrate OperatorList
            await HydrateOperatorList(new List<Guest> { guest });

            var guestsInBatch = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.Id != guest.Id)
                .Include(g => g.NationalityEntity)
                .ToListAsync();

            var vm = new GuestDetailsViewModel
            {
                Guest = guest,
                GuestsInBatch = guestsInBatch
            };

            return PartialView("_BookingDetailsPartial", vm);
        }

        // ─── PopulateDropdowns ────────────────────────────────────────────────────
        private async Task PopulateDropdowns()
        {
            ViewBag.OperatorList = new SelectList(await _context.OperatorLists.ToListAsync(), "OperatorId", "BusinessName");
            ViewBag.NationalityList = new SelectList(await _context.Nationalities.ToListAsync(), "NationalityId", "NatName");
        }

        private int GenerateRFID() => 1;

        // ─── NewBooking GET ───────────────────────────────────────────────────────
        public async Task<IActionResult> NewBooking(string batch, int? id)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var username = User.Identity!.Name;

            List<OperatorList> operators;
            bool operatorLocked = false;

            if (userRole == "Operator")
            {
                // Step 1: Find the login record in tbl_operator_mobile by username
                var mobileOp = await _context.Operators
                    .FirstOrDefaultAsync(o => o.Username == username);

                if (mobileOp != null)
                {
                    // Step 2: Match to operator_list by the same Id
                    var matchedOp = await _context.OperatorLists
                        .FirstOrDefaultAsync(o => o.OperatorId == mobileOp.Id);

                    operators = matchedOp != null
                        ? new List<OperatorList> { matchedOp }
                        : new List<OperatorList>();
                }
                else
                {
                    operators = new List<OperatorList>();
                }

                operatorLocked = true;
            }
            else
            {
                // Admin / Super Admin — read from operator_list
                operators = await _context.OperatorLists
                    .Where(o => o.Status == 1)
                    .OrderBy(o => o.BusinessName)
                    .ToListAsync();

                operatorLocked = false;
            }

            var operatorSelectList = operators.Select(o => new
            {
                Id = o.OperatorId,
                DisplayName = !string.IsNullOrWhiteSpace(o.BusinessName)
                                ? o.BusinessName
                                : o.OwnerName ?? "No Operator"
            }).ToList();

            ViewBag.OperatorList = new SelectList(operatorSelectList, "Id", "DisplayName");
            ViewBag.OperatorLocked = operatorLocked;

            // Load anticipated guests — NO Include for OperatorList
            var anticipatedGuests = await _context.Guests
                .Where(g => g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated)
                .ToListAsync();

            // Manually hydrate OperatorList
            await HydrateOperatorList(anticipatedGuests);

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
                            ArrivalDate = first.ArrivalDate,
                            Date = first.Date,
                            BookingStatus = first.BookingStatus
                        };
                    }).ToList()
            };

            ViewBag.CurrentBatch = batch;
            ViewBag.MainGuestId = id;

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
                    model.NewGuest.Batch = batchDetails.Batch;
                }
            }

            if (!string.IsNullOrEmpty(model.NewGuest.Date) &&
                DateTime.TryParse(model.NewGuest.Date, out DateTime bookingDate))
                model.Html5BookingDate = bookingDate.ToString("yyyy-MM-ddTHH:mm");
            else
                model.Html5BookingDate = null;

            if (!string.IsNullOrEmpty(model.NewGuest.ArrivalDate) &&
                long.TryParse(model.NewGuest.ArrivalDate, out long unixTimestamp))
            {
                var dtArrival = ConvertUnixToDateTime(unixTimestamp);
                model.Html5ArrivalDate = dtArrival.ToString("yyyy-MM-ddTHH:mm");
            }

            ViewBag.IsReadonly = !string.IsNullOrEmpty(batch);

            return View(model);
        }

        private DateTime ConvertUnixToDateTime(long unixTimestamp)
            => DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime.ToLocalTime();

        // ─── NewBooking POST ──────────────────────────────────────────────────────
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
                    long baseUnixTimestamp = GetCurrentUnixTimestamp();
                    string firstGuestRFIDCode = null;

                    foreach (var guest in batchGuests)
                    {
                        long guestArrivalTimestamp = baseUnixTimestamp + (insertedCount * 60L);
                        long rfidTimestamp = guestArrivalTimestamp + 500L + (insertedCount * 100L);
                        string generatedRFIDCode = rfidTimestamp.ToString();

                        if (insertedCount == 0)
                            firstGuestRFIDCode = generatedRFIDCode;

                        guest.BookingStatus = (int)Guest.BookingStatusEnum.anticipated;
                        guest.Batch = batchId;
                        guest.RFID = 1;
                        guest.RFIDCode = generatedRFIDCode;
                        guest.Year = guest.Year ?? DateTime.Today.Year.ToString();
                        guest.Month = DateTime.Today.ToString("MMMM");

                        if (!string.IsNullOrEmpty(guest.ArrivalDate))
                        {
                            if (DateTime.TryParse(guest.ArrivalDate, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTime dt))
                            {
                                long userArrival = new DateTimeOffset(dt).ToUnixTimeSeconds();
                                long uniqueArrival = userArrival + (insertedCount * 60L);
                                guest.ArrivalDate = uniqueArrival.ToString();

                                rfidTimestamp = uniqueArrival + 500L + (insertedCount * 100L);
                                guest.RFIDCode = rfidTimestamp.ToString();

                                if (insertedCount == 0)
                                    firstGuestRFIDCode = guest.RFIDCode;
                            }
                        }
                        else
                        {
                            guest.ArrivalDate = guestArrivalTimestamp.ToString();
                        }

                        guest.Date = DateTime.Now.ToString("ddd, dd MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
                        guest.DateShort = DateTime.Today.ToString("MMMM dd yyyy");

                        _context.Guests.Add(guest);
                        insertedCount++;
                    }

                    await _context.SaveChangesAsync();

                    if (Photo != null && Photo.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await Photo.CopyToAsync(ms);
                        _context.Add(new GuestImage
                        {
                            WristbondGuestCode = firstGuestRFIDCode,
                            Image = ms.ToArray()
                        });
                        await _context.SaveChangesAsync();
                    }

                    TempData["ToastMessage"] = "Guests added successfully";
                    TempData["ToastType"] = "success";

                    return RedirectToAction("SaveGuest");
                }

                TempData["ToastMessage"] = "Please add at least one guest before saving!";
                TempData["ToastType"] = "warning";
            }

            await PopulateDropdowns();
            return View(model);
        }

        private long GetCurrentUnixTimestamp()
            => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        // ─── GenerateBatchCode ────────────────────────────────────────────────────
        private async Task<string> GenerateBatchCode()
        {
            var numericBatches = _context.Guests
                .AsEnumerable()
                .Select(g => g.Batch)
                .Where(b => !string.IsNullOrWhiteSpace(b) && b.All(char.IsDigit) && int.TryParse(b, out _))
                .Select(b => int.Parse(b))
                .OrderByDescending(x => x)
                .ToList();

            int next = numericBatches.Any() ? numericBatches.First() + 1 : 10000;
            return next.ToString();
        }

        private string GenerateBatchCode(int? operatorId)
            => $"OP{operatorId}-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        private string GenerateRFIDCode() => GetCurrentUnixTimestamp().ToString();

        // ─── SaveGuest ────────────────────────────────────────────────────────────
        public async Task<IActionResult> SaveGuest()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            int? currentOperatorId = null;
            if (userRole == "Operator" && int.TryParse(userId, out int parsedId))
                currentOperatorId = parsedId;

            var query = _context.Guests
                .Where(g => g.BookingStatus == (int)Guest.BookingStatusEnum.anticipated);

            if (currentOperatorId.HasValue)
                query = query.Where(g => g.OperatorId == currentOperatorId.Value);

            var anticipatedGuests = await query.ToListAsync();

            // ✅ Manually hydrate OperatorList
            await HydrateOperatorList(anticipatedGuests);

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

            return View(new GuestListViewModel { ReservedGuests = grouped });
        }

        // ─── GetNationalities ─────────────────────────────────────────────────────
        public async Task<IActionResult> GetNationalities()
        {
            try
            {
                var nationalities = await _context.Nationalities
                    .Where(n => n.NatName != "Within Cebu Province" && n.NatName != "Outside Cebu Province")
                    .OrderBy(n => n.NatName)
                    .Select(n => new { n.id, n.NatName })
                    .ToListAsync();

                return Json(nationalities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ─── BookingDetailsPartial ────────────────────────────────────────────────
        public IActionResult BookingDetailsPartial(int id)
        {
            var guest = _context.Guests.FirstOrDefault(g => g.Id == id);
            var model = new GuestDetailsViewModel
            {
                Guest = guest,
                GuestsInBatch = _context.Guests.Where(g => g.Batch == guest.Batch).ToList()
            };
            return PartialView("_BookingDetailsPartial", model);
        }

        // ─── CancelGuest ──────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGuest(int GuestId)
        {
            var guest = await _context.Guests.FindAsync(GuestId);
            if (guest == null)
                return Json(new { success = false, message = "Guest not found" });

            var batch = guest.Batch;
            _context.Guests.Remove(guest);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "An error occurred while deleting the guest." });
            }

            return Json(new { success = true, guestId = GuestId });
        }

        // ─── GetUpdatedGuestList ──────────────────────────────────────────────────
        [HttpGet]
        public IActionResult GetUpdatedGuestList(string batchId)
        {
            var allGuests = _context.Guests
                .Where(g => g.Batch == batchId && g.BookingStatus != (int)Guest.BookingStatusEnum.canceled)
                .ToList();

            return PartialView("_BookingDetailsPartial", allGuests);
        }

        // ─── UpdateStatus ─────────────────────────────────────────────────────────
        public async Task<IActionResult> UpdateStatus(int id, int status)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
                return NotFound();

            guest.BookingStatus = 2;

            var batchGuests = await _context.Guests
                .Where(g => g.Batch == guest.Batch && g.BookingStatus != 2)
                .ToListAsync();

            if (!batchGuests.Any())
                return NotFound("No other guests found for this batch.");

            foreach (var g in batchGuests)
                g.BookingStatus = 2;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency conflict.");
            }

            return RedirectToAction("ReserveBooking", "Reserve");
        }

        // ─── DownloadQRCode ───────────────────────────────────────────────────────
        public IActionResult DownloadQRCode(string base64Image, string fileName)
        {
            if (string.IsNullOrEmpty(base64Image))
            {
                TempData["ToastMessage"] = "No image data provided.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ReserveBookings");
            }

            try
            {
                var base64Data = base64Image.Substring(base64Image.IndexOf(",") + 1);
                return File(Convert.FromBase64String(base64Data), "image/png", fileName);
            }
            catch (Exception ex)
            {
                TempData["ToastMessage"] = $"Error downloading QR code: {ex.Message}";
                TempData["ToastType"] = "danger";
                return RedirectToAction("reservebooking");
            }
        }

        private bool GuestExists(int id) => _context.Guests.Any(e => e.Id == id);

        private int GetCurrentOperatorId()
            => int.TryParse(User.Identity?.Name, out int operatorId) ? operatorId : 0;
    }
}