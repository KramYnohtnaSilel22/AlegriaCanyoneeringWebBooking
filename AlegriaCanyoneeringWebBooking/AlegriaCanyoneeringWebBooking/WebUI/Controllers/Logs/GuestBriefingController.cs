using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class GuestBriefingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuestBriefingController> _logger;

    public GuestBriefingController(ApplicationDbContext context, ILogger<GuestBriefingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: GuestBriefing
    public IActionResult Index() => View();
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> GetGuestBriefings()
    //{
    //    try
    //    {
    //        var draw = Request.Form["draw"].FirstOrDefault();
    //        var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
    //        var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
    //        var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

    //        // keep your original filter logic (do not touch)
    //        var query = _context.GuestBriefings.AsNoTracking()
    //            .Where(g =>
    //                g.BGuestName == "Dendo Pulgo" ||
    //                g.BGuestName == "----" ||
    //                (g.BGuestName != null && g.BGuestName.StartsWith("Guest Number "))
    //            );

    //        if (!string.IsNullOrEmpty(searchValue))
    //        {
    //            var pattern = $"%{searchValue}%";
    //            query = query.Where(g =>
    //                (!string.IsNullOrEmpty(g.BGuestName) && EF.Functions.Like(g.BGuestName!, pattern)) ||
    //                (!string.IsNullOrEmpty(g.BWristBondCode) && EF.Functions.Like(g.BWristBondCode!, pattern)) ||
    //                (!string.IsNullOrEmpty(g.BDateCode) && EF.Functions.Like(g.BDateCode!, pattern))
    //            );
    //        }

    //        var totalRecords = await _context.GuestBriefings.CountAsync();
    //        var filteredRecords = await query.CountAsync();

    //        var guests = await query
    //            .OrderByDescending(g => g.BDateArrival) // still okay even if varchar (will order lexically)
    //            .Skip(start)
    //            .Take(length)
    //            .Select(g => new
    //            {
    //                id = g.BGuestID,
    //                wristband = g.BWristBondCode ?? string.Empty,
    //                guestNameHuman = g.BGuestName ?? "",

    //                // ✅ Parse safely string date to readable text
    //                arrival = ParseDateString(g.BDateArrival),
    //                departure = ParseDateString(g.BDateDeparture),

    //                dateCode = g.BDateCode ?? string.Empty,
    //                image = (g.BGuestImage != null && g.BGuestImage.Length > 0)
    //                    ? Convert.ToBase64String(g.BGuestImage)
    //                    : null
    //            })
    //            .ToListAsync();

    //        return Json(new
    //        {
    //            draw,
    //            recordsTotal = totalRecords,
    //            recordsFiltered = filteredRecords,
    //            data = guests
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error loading guest briefings.");
    //        return Json(new { error = $"Server error: {ex.Message}" });
    //    }
    //}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetGuestBriefings()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            var query = _context.GuestBriefings.AsNoTracking()
              .Where(g => !string.IsNullOrEmpty(g.BGuestName));


            // Search filter
            if (!string.IsNullOrEmpty(searchValue))
            {
                var pattern = $"%{searchValue}%";
                query = query.Where(g =>
                    (!string.IsNullOrEmpty(g.BGuestName) && EF.Functions.Like(g.BGuestName!, pattern)) ||
                    (!string.IsNullOrEmpty(g.BWristBondCode) && EF.Functions.Like(g.BWristBondCode!, pattern)) ||
                    (!string.IsNullOrEmpty(g.BDateCode) && EF.Functions.Like(g.BDateCode!, pattern))
                );
            }

            var totalRecords = await _context.GuestBriefings.CountAsync();
            var filteredRecords = await query.CountAsync();

            // Fetch data and parse string dates safely
            var guests = query
                .AsEnumerable() // move to in-memory to parse string dates
                .OrderByDescending(g => ParseDateForOrdering(g.BDateArrival))
                .Skip(start)
                .Take(length)
                .Select(g => new
                {
                    id = g.BGuestID,
                    wristband = g.BWristBondCode ?? string.Empty,
                    guestNameHuman = g.BGuestName ?? "",
                    arrival = ParseDateString(g.BDateArrival),
                    departure = ParseDateString(g.BDateDeparture),
                    dateCode = g.BDateCode ?? string.Empty,
                    image = (g.BGuestImage != null && g.BGuestImage.Length > 0)
                        ? Convert.ToBase64String(g.BGuestImage)
                        : null
                })
                .ToList();

            return Json(new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = guests
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading guest briefings.");
            return Json(new { error = $"Server error: {ex.Message}" });
        }
    }

    // Helper to parse string date for ordering
    private DateTime? ParseDateForOrdering(string? dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt))
            return dt;
        return null;
    }

    // Helper to display string date nicely
    private string ParseDateString(string? dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt))
            return dt.ToString("ddd MMM dd yyyy HH:mm:ss");
        return dateStr ?? "-----";
    }



    // ✅ AJAX Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        try
        {
            var guest = await _context.GuestBriefings.FindAsync(id);
            if (guest == null)
                return Json(new { success = false, message = "Guest not found." });

            _context.GuestBriefings.Remove(guest);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Guest deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guest.");
            return Json(new { success = false, message = "Error deleting guest." });
        }
    }
}
