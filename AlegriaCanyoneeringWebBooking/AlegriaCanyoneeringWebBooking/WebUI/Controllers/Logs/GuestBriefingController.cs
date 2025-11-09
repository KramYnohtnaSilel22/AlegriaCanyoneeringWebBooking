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

    // ✅ Server-side DataTable endpoint
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetGuestBriefings()
    {
        try
        {
            // --- DataTables params ---
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            var query = _context.GuestBriefings.AsNoTracking();

            // --- Search ---
            if (!string.IsNullOrEmpty(searchValue))
            {
                var pattern = $"%{searchValue}%";
                query = query.Where(g =>
                    EF.Functions.Like(g.BGuestName!, pattern) ||
                    EF.Functions.Like(g.BWristBondCode!, pattern) ||
                    EF.Functions.Like(g.BDateCode!, pattern)
                );
            }

            var totalRecords = await _context.GuestBriefings.CountAsync();
            var filteredRecords = await query.CountAsync();

            // --- Paging ---
            var guests = await query
                .OrderByDescending(g => g.BDateArrival)
                .Skip(start)
                .Take(length)
                .Select(g => new
                {
                    id = g.BGuestID,
                    wristband = g.BWristBondCode,
                    name = g.BGuestName,
                    arrival = g.BDateArrival.HasValue ? g.BDateArrival.Value.ToString("g") : "",
                    departure = g.BDateDeparture.HasValue ? g.BDateDeparture.Value.ToString("g") : "",
                    dateCode = g.BDateCode,
                    image = g.BGuestImage != null ? Convert.ToBase64String(g.BGuestImage) : null
                })
                .ToListAsync();

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
            return Json(new { error = "Server error occurred while loading guest briefings." });
        }
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
