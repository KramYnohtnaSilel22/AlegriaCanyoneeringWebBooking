using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // =========================================================
        // INDEX
        // =========================================================
        public IActionResult Index() => View();

        // =========================================================
        // DATATABLE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetRolesData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

            var query = _context.Roles.AsQueryable();

            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(r => r.Name.ToLower().Contains(searchValue));

            var recordsTotal = _context.Roles.Count();
            var recordsFiltered = query.Count();

            var data = query
                .OrderBy(r => r.RoleId)
                .Skip(start)
                .Take(length)
                .Select(r => new { r.RoleId, r.Name })
                .ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // =========================================================
        // CREATE — GET
        // =========================================================
        [HttpGet]
        public IActionResult Create()
        {
            ViewData["Action"] = "Create";
            return PartialView("_RoleForm", new Role());
        }

        // =========================================================
        // CREATE — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Role role)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewData["Action"] = "Create";
                    return PartialView("_RoleForm", role);
                }

                bool exists = await _context.Roles
                    .AnyAsync(r => r.Name.ToLower() == role.Name.ToLower());

                if (exists)
                {
                    ModelState.AddModelError("Name", "This role already exists.");
                    ViewData["Action"] = "Create";
                    return PartialView("_RoleForm", role);
                }

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Role '{role.Name}' created successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // =========================================================
        // EDIT — GET  (/Roles/Edit/5)
        // =========================================================
        [HttpGet("/Roles/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                    return Json(new { success = false, message = "Role not found." });

                ViewData["Action"] = "Edit";
                return PartialView("_RoleForm", role);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // =========================================================
        // EDIT — POST  (/Roles/Edit/5)
        // =========================================================
        [HttpPost("/Roles/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Role role)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewData["Action"] = "Edit";
                    return PartialView("_RoleForm", role);
                }

                bool exists = await _context.Roles
                    .AnyAsync(r => r.Name.ToLower() == role.Name.ToLower()
                                && r.RoleId != id);

                if (exists)
                {
                    ModelState.AddModelError("Name", "This role already exists.");
                    ViewData["Action"] = "Edit";
                    return PartialView("_RoleForm", role);
                }

                var existing = await _context.Roles.FindAsync(id);
                if (existing == null)
                    return Json(new { success = false, message = "Role not found." });

                existing.Name = role.Name;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Role '{existing.Name}' updated successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // =========================================================
        // DELETE — AJAX POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                    return Json(new { success = false, message = "Role not found." });

                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Role '{role.Name}' deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }
    }
}