using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class BatchesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BatchesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Batchs
        public async Task<IActionResult> Index()
        {
            var batches = await _context.Batches
                .Include(b => b.OperatorList)
                .ToListAsync();
            return View(batches);
        }

        // GET: Batchs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var batch = await _context.Batches
                .Include(b => b.OperatorList)
                .FirstOrDefaultAsync(m => m.BatchId == id);

            if (batch == null) return NotFound();

            return View(batch);
        }

        // GET: Batchs/Create
        public IActionResult Create()
        {
            ViewBag.OperatorId = new SelectList(_context.OperatorLists, "OperatorId", "OwnerName");
            return View();
        }


        // POST: Batchs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BatchId,OperatorId,NoOfLocalGuest,NoOfForeignGuest,NoOfTGuide,NoOfMDriver,TotalNoOfGuest,ArrivalDate")] Batch batch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(batch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(batch);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var batch = await _context.Batches.FindAsync(id);
            if (batch == null) return NotFound();

            ViewBag.OperatorId = new SelectList(_context.OperatorLists, "OperatorId", "OwnerName", batch.OperatorId);
            return View(batch);
        }

        // POST: Batchs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BatchId,OperatorId,NoOfLocalGuest,NoOfForeignGuest,NoOfTGuide,NoOfMDriver,TotalNoOfGuest,ArrivalDate")] Batch batch)
        {
            if (id != batch.BatchId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(batch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BatchExists(batch.BatchId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(batch);
        }

        // GET: Batches/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var batch = await _context.Batches
                .Include(b => b.OperatorList) // include Operator so OwnerName is available
                .FirstOrDefaultAsync(m => m.BatchId == id);

            if (batch == null) return NotFound();

            return View(batch);  // Returns Delete.cshtml confirmation page
        }

        // POST: Batches/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var batch = await _context.Batches.FindAsync(id);
            if (batch != null)
            {
                _context.Batches.Remove(batch);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BatchExists(int id)
        {
            return _context.Batches.Any(e => e.BatchId == id);
        }
    }
}
