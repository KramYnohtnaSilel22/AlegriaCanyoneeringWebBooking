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
        public IActionResult Create()
        {
            ViewData["Action"] = "Create";
            return PartialView("_CreatePartial", new Role());
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Role role)
        {
            if (ModelState.IsValid)
            {
                var exists = await _context.Roles
                    .AnyAsync(r => r.Name.ToLower() == role.Name.ToLower());

                if (exists)
                {
                    ModelState.AddModelError("Name", "This role already exists.");
                    return PartialView("_CreatePartial", role);
                }

                _context.Add(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Role '{role.Name}' created successfully!" });
            }

            ViewData["Action"] = "Create";
            return PartialView("_CreatePartial", role);
        }

        // GET: Roles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return Json(new { success = false, message = "Role ID is required." });

            var role = await _context.Roles.FindAsync(id);
            if (role == null) return Json(new { success = false, message = "Role not found." });

            ViewData["Action"] = "Edit";
            return PartialView("_EditPartial", role);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Role role)
        {
            if (!ModelState.IsValid)
                return PartialView("_EditPartial", role);

            var exists = await _context.Roles
                .AnyAsync(r => r.Name.ToLower() == role.Name.ToLower() && r.RoleId != role.RoleId);

            if (exists)
            {
                ModelState.AddModelError("Name", "This role already exists.");
                return PartialView("_EditPartial", role);
            }

            try
            {
                _context.Update(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Role '{role.Name}' updated successfully!" });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "An error occurred while updating." });
            }
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
