using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class OperatorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperatorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Operators
        public async Task<IActionResult> Index()
        {
            var operators = _context.Operators.Include(o => o.Roles);
            return View(await operators.ToListAsync());
        }

        // GET: Operators/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var op = await _context.Operators
                                   .Include(o => o.Roles)
                                   .FirstOrDefaultAsync(m => m.Id == id);
            if (op == null) return NotFound();

            return View(op);
        }

        // GET: Operators/Create
        public IActionResult Create()
        {
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name");
            return View();
        }

        // POST: Operators/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,BusinessName,Age,Gender,Username,Password,RoleId")] Operator op)
        {
            if (ModelState.IsValid)
            {
                _context.Add(op);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            return View(op);
        }

        // GET: Operators/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var op = await _context.Operators.FindAsync(id);
            if (op == null) return NotFound();

            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            return View(op);
        }

        // POST: Operators/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,BusinessName,Age,Gender,Username,Password,RoleId")] Operator op)
        {
            if (id != op.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(op);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Operators.Any(e => e.Id == id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            return View(op);
        }

        // GET: Operators/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var op = await _context.Operators
                                   .Include(o => o.Roles)
                                   .FirstOrDefaultAsync(m => m.Id == id);
            if (op == null) return NotFound();

            return View(op);
        }

        // POST: Operators/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var op = await _context.Operators.FindAsync(id);
            if (op != null)
            {
                _context.Operators.Remove(op);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
