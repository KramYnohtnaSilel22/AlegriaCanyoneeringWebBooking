
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking
{
    [Authorize(Roles = "Super Admin")]
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
            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            return View();
        }

        // POST: Operators/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,BusinessName,Age,Gender,Username,Password,EmailAddress,RoleId")] Operator op)
        {

            if (ModelState.IsValid)
            {

                // ✅ Hashes the plain text password before saving
                op.Password = PasswordHelper.HashPassword(op.Password);
                _context.Add(op);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            return View(op);
        }

        // GET: Operators/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var op = await _context.Operators.FindAsync(id);
            if (op == null)
                return NotFound();

            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);

            return View(op);
        }


        // POST: Operators/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
     int id,
     [Bind("Id,Name,BusinessName,Age,Gender,Username,EmailAddress,RoleId")] Operator op,
     string? NewPassword)
        {
            if (id != op.Id) // Ensure property name matches your model
                return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                return View(op);
            }

            var existing = await _context.Operators.FindAsync(id);
            if (existing == null)
                return NotFound();

            // Update fields
            existing.Name = op.Name;
            existing.BusinessName = op.BusinessName;
            existing.Age = op.Age;
            existing.Gender = op.Gender;
            existing.Username = op.Username;
            existing.EmailAddress = op.EmailAddress;
            existing.RoleId = op.RoleId;

            // Password handling
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                existing.Password = PasswordHelper.HashPassword(NewPassword);
            }
            // Do not re-hash if password was unchanged

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
