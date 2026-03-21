using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Super Admin")]
public class GuestBriefingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuestBriefingController> _logger;

    public GuestBriefingController(ApplicationDbContext context, ILogger<GuestBriefingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IActionResult Index() => View();

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

            var query = _context.GuestBriefings
                .AsNoTracking()
                .Where(g => !string.IsNullOrEmpty(g.BGuestName));

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

            var guests = query
                .AsEnumerable()
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
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

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

            return Json(new { success = true, message = "Guest briefing deleted successfully." });
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("FOREIGN KEY") == true ||
            ex.InnerException?.Message.Contains("REFERENCE") == true)
        {
            return Json(new { success = false, message = "Cannot delete — this record is linked to other data." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guest {Id}", id);
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
        }
    }

    private DateTime? ParseDateForOrdering(string? dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt)) return dt;
        return null;
    }

    private string ParseDateString(string? dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt))
            return dt.ToString("ddd MMM dd yyyy HH:mm:ss");
        return dateStr ?? "—";
    }
}