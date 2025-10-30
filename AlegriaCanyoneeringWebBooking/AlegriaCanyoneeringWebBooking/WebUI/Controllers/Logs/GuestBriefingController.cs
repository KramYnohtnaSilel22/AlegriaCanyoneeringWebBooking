using AlegriaCanyoneeringWebBooking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class GuestBriefingController : Controller
{
    private readonly ApplicationDbContext _context;

    public GuestBriefingController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: GuestBriefing
    public async Task<IActionResult> Index()
    {
        var guestBriefings = await _context.GuestBriefings.ToListAsync();
        return View(guestBriefings);
    }

    // POST: GuestBriefing/DeleteConfirmed/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var guest = await _context.GuestBriefings.FindAsync(id);
        if (guest != null)
        {
            _context.GuestBriefings.Remove(guest);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }




}

