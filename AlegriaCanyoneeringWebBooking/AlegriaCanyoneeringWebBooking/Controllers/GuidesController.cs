using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class GuidesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GuidesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Guides
        public async Task<IActionResult> Index()
        {
            return View(await _context.Guides.ToListAsync());
        }

        // GET: Guides/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            return View(guide);
        }

        // GET: Guides/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Guides/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Rfid,TPosition,FName,MName,LName,CNumber,Address,Nickname,Image")] Guide guide)
        {
            if (ModelState.IsValid)
            {
                _context.Add(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(guide);
        }

        // GET: Guides/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            return View(guide);
        }

        // POST: Guides/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Rfid,TPosition,FName,MName,LName,CNumber,Address,Nickname,Image")] Guide guide)
        {
            if (id != guide.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(guide);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GuideExists(guide.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(guide);
        }

        // GET: Guides/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            return View(guide);
        }

        // POST: Guides/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool GuideExists(int id)
        {
            return _context.Guides.Any(e => e.Id == id);
        }
    }
}
