using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class RolesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RolesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Roles
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetRolesData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            var rolesQuery = _context.Roles.AsQueryable();

            if (!string.IsNullOrEmpty(searchValue))
            {
                rolesQuery = rolesQuery.Where(r => r.Name.ToLower().Contains(searchValue));
            }

            var recordsTotal = _context.Roles.Count();
            var recordsFiltered = rolesQuery.Count();

            var data = rolesQuery
                .OrderBy(r => r.RoleId)
                .Skip(start)
                .Take(length)
                .Select(r => new
                {
                    r.RoleId,
                    r.Name
                }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // GET: Roles/Create
        public IActionResult Create() => View();

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoleId,Name")] Role role)
        {
            if (ModelState.IsValid)
            {
                _context.Add(role);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Role '{role.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to create role. Please check your input.";
            return View(role);
        }

        // GET: Roles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound();

            return View(role);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RoleId,Name")] Role role)
        {
            if (id != role.RoleId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(role);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Role '{role.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoleExists(role.RoleId))
                    {
                        TempData["ErrorMessage"] = "Role not found.";
                        return RedirectToAction(nameof(Index));
                    }
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to update role. Please check your input.";
            return View(role);
        }

        // POST: Roles/DeleteAjax/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
                return Json(new { success = false, message = "Role not found." });

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Role '{role.Name}' deleted successfully.";
            return Json(new { success = true, message = $"Role '{role.Name}' deleted successfully." });
        }

        private bool RoleExists(int id) => _context.Roles.Any(e => e.RoleId == id);
    }
}
