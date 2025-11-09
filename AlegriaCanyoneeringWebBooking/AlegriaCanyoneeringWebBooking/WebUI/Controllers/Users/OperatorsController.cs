
using AlegriaCanyoneeringWebBooking.Helpers;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin")]
    public class OperatorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperatorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]

        public async Task<IActionResult> GetUserData()
        {
            try
            {
                // --- Get user role ---
                var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                if (string.IsNullOrEmpty(currentUserRole))
                    return Json(new { error = "Unable to determine user role." });

                // --- DataTables parameters ---
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

                IQueryable<Operator> query = _context.Operators.Include(o => o.Roles);

                // --- Role-based filtering ---
                if (currentUserRole == "Admin")
                {
                    query = query.Where(o => o.Roles != null && o.Roles.Name == "Operator");
                }
                else if (currentUserRole != "Super Admin")
                {
                    return Json(new { error = "Access denied." });
                }

                // --- Global search ---
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(o =>
                        (o.Name != null && o.Name.ToLower().Contains(searchValue)) ||
                        (o.Username != null && o.Username.ToLower().Contains(searchValue)) ||
                        (o.EmailAddress != null && o.EmailAddress.ToLower().Contains(searchValue)) ||
                        (o.Gender != null && o.Gender.ToLower().Contains(searchValue)) ||
                        (o.BusinessName != null && o.BusinessName.ToLower().Contains(searchValue)) ||
                        (o.Roles != null && o.Roles.Name.ToLower().Contains(searchValue))
                    );
                }

                // --- Total / filtered records ---
                var recordsFiltered = await query.CountAsync();
                var recordsTotal = currentUserRole == "Super Admin"
                    ? await _context.Operators.CountAsync()
                    : await _context.Operators.Include(o => o.Roles)
                        .CountAsync(o => o.Roles != null && o.Roles.Name == "Operator");

                // --- Paging & projection ---
                var data = await query
                    .OrderBy(o => o.Name)
                    .Skip(start)
                    .Take(length)
                    .Select(o => new
                    {
                        id = o.Id,
                        ownerName = o.Name ?? "",
                        gender = o.Gender ?? "",
                        businessName = o.BusinessName ?? "",
                        age = o.Age,
                        username = o.Username ?? "",
                        emailAddress = o.EmailAddress ?? "",
                        role = o.Roles != null ? o.Roles.Name : "",
                        status = 1
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw,
                    recordsFiltered,
                    recordsTotal,
                    data
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server Error: {ex.Message}" });
            }
        }


        // GET: Operators
        public async Task<IActionResult> Index()
        {
            // Get current user's role
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                               ?? User.FindFirst("Role")?.Value;

            if (string.IsNullOrEmpty(currentUserRole))
            {
                TempData["ErrorMessage"] = "Unable to determine user role. Please log in again.";
                return RedirectToAction("Login", "Authentication");
            }

            IQueryable<Operator> operators;

            if (currentUserRole == "Super Admin")
            {
                // Super Admin can see ALL users (Super Admin, Admin, Operator)
                operators = _context.Operators.Include(o => o.Roles);
            }
            else if (currentUserRole == "Admin")
            {
                // Admin can ONLY see Operators
                operators = _context.Operators
                    .Include(o => o.Roles)
                    .Where(o => o.Roles.Name == "Operator");
            }
            else
            {
                // Operators cannot access this page
                return Forbid();
            }

            var operatorsList = await operators.OrderBy(o => o.Name).ToListAsync();

            // Pass the current user role to the view for conditional rendering
            ViewBag.CurrentUserRole = currentUserRole;

            return View(operatorsList);
        }


        // GET: Operators/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (op == null)
                return NotFound();

            // Get current user's role
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                               ?? User.FindFirst("Role")?.Value;

            // Security check: Admin can only view Operator details
            if (currentUserRole == "Admin" && op.Roles?.Name != "Operator")
            {
                TempData["ErrorMessage"] = "You can only view Operator account details.";
                return RedirectToAction(nameof(Index));
            }

            return View(op);
        }

        // GET: Operators/Create 
        [HttpGet]
        public IActionResult Create()
        {
            var currentUserRole = User.FindFirst("Role")?.Value;

            if (currentUserRole == "Super Admin")
            {
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name");
            }
            else if (currentUserRole == "Admin")
            {
                ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");
            }
            else
            {
                return Forbid();
            }

            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            ViewData["Action"] = "Create";
            return PartialView("_CreatePartial", new Operator());
        }

        // POST: Operators/Create - Handle AJAX Form Submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,BusinessName,Age,Gender,Username,Password,EmailAddress,RoleId")] Operator op)
        {
            var currentUserRole = User.FindFirst("Role")?.Value;

            if (currentUserRole != "Super Admin" && currentUserRole != "Admin")
            {
                return Json(new { success = false, message = "Access denied." });
            }

            if (ModelState.IsValid)
            {
                // Set BusinessName default if empty
                if (string.IsNullOrWhiteSpace(op.BusinessName))
                {
                    op.BusinessName = "No Operator";
                }

                // Role validation for Admin
                if (currentUserRole == "Admin")
                {
                    var operatorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Operator");
                    if (operatorRole == null)
                    {
                        return Json(new { success = false, message = "Operator role not found in the system." });
                    }
                    op.RoleId = operatorRole.RoleId;
                }
                else if (currentUserRole == "Super Admin")
                {
                    var selectedRole = await _context.Roles.FindAsync(op.RoleId);
                    if (selectedRole == null)
                    {
                        ModelState.AddModelError("RoleId", "Invalid role selected.");
                        ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                        ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
                        ViewData["Action"] = "Create";
                        return PartialView("_CreatePartial", op);
                    }
                }

                // Check username uniqueness
                var existingUser = await _context.Operators.FirstOrDefaultAsync(u => u.Username == op.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");

                    if (currentUserRole == "Super Admin")
                        ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                    else
                        ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");

                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
                    ViewData["Action"] = "Create";
                    return PartialView("_CreatePartial", op);
                }

                // Hash password
                op.Password = PasswordHelper.HashPassword(op.Password);

                _context.Add(op);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"User '{op.Name}' created successfully!" });
            }

            // Return partial view with validation errors
            if (currentUserRole == "Super Admin")
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            else
                ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");

            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            ViewData["Action"] = "Create";
            return PartialView("_CreatePartial", op);
        }


        // ======================
        // GET: Operators/Edit/5
        // ======================
        [HttpGet]
        [Route("Operators/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (op == null)
                return NotFound("Operator not found.");

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
            if (string.IsNullOrEmpty(currentUserRole))
                return BadRequest("Unable to determine user role. Please log in again.");

            // 🔹 Role-based filtering
            IQueryable<Role> availableRoles;
            if (currentUserRole == "Super Admin")
            {
                availableRoles = _context.Roles;
            }
            else if (currentUserRole == "Admin")
            {
                // Admins can only edit Operator accounts
                if (op.Roles?.Name != "Operator")
                    return BadRequest("You can only edit Operator accounts.");

                availableRoles = _context.Roles.Where(r => r.Name == "Operator");
            }
            else
            {
                return Forbid();
            }

            ViewData["RoleId"] = new SelectList(availableRoles, "RoleId", "Name", op.RoleId);
            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);

            return PartialView("_EditPartial", op);
        }

        // ======================
        // POST: Operators/Edit/5
        // ======================
        [HttpPost]
        [Route("Operators/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,BusinessName,Age,Gender,Username,EmailAddress,RoleId")] Operator op, string? NewPassword)
        {
            if (id != op.Id)
                return Json(new { success = false, message = "Invalid request." });

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
            if (currentUserRole != "Super Admin" && currentUserRole != "Admin")
                return Json(new { success = false, message = "Access denied." });

            var existing = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existing == null)
                return Json(new { success = false, message = "Operator not found." });

            // 🔹 Role restriction for Admin
            if (currentUserRole == "Admin")
            {
                if (existing.Roles?.Name != "Operator")
                    return Json(new { success = false, message = "You can only edit Operator accounts." });

                var operatorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Operator");
                if (operatorRole == null)
                    return Json(new { success = false, message = "Operator role not found in the system." });
                op.RoleId = operatorRole.RoleId;
            }

            // 🔹 Validate role for Super Admin
            if (currentUserRole == "Super Admin")
            {
                var selectedRole = await _context.Roles.FindAsync(op.RoleId);
                if (selectedRole == null)
                {
                    IQueryable<Role> availableRoles = _context.Roles;
                    ViewData["RoleId"] = new SelectList(availableRoles, "RoleId", "Name", op.RoleId);
                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);

                    ModelState.AddModelError("RoleId", "Invalid role selected.");
                    return PartialView("_EditPartial", op);
                }
            }

            // 🔹 Uniqueness check for Username
            bool usernameExists = await _context.Operators
                .AnyAsync(u => u.Username == op.Username && u.Id != id);
            if (usernameExists)
                ModelState.AddModelError("Username", "Username already exists.");

            // 🔹 Return PartialView on validation error
            if (!ModelState.IsValid)
            {
                IQueryable<Role> availableRoles =
                    currentUserRole == "Super Admin"
                        ? _context.Roles
                        : _context.Roles.Where(r => r.Name == "Operator");

                ViewData["RoleId"] = new SelectList(availableRoles, "RoleId", "Name", op.RoleId);
                ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);

                return PartialView("_EditPartial", op);
            }

            // 🔹 Update fields
            existing.Name = op.Name;
            existing.BusinessName = op.BusinessName;
            existing.Age = op.Age;
            existing.Gender = op.Gender;
            existing.Username = op.Username;
            existing.EmailAddress = op.EmailAddress;
            existing.RoleId = op.RoleId;
            if (!string.IsNullOrWhiteSpace(NewPassword))
                existing.Password = PasswordHelper.HashPassword(NewPassword);

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Operator updated successfully!" });
            }
            catch (Exception)
            {
                IQueryable<Role> availableRoles =
                    currentUserRole == "Super Admin"
                        ? _context.Roles
                        : _context.Roles.Where(r => r.Name == "Operator");

                ViewData["RoleId"] = new SelectList(availableRoles, "RoleId", "Name", op.RoleId);
                ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);

                ModelState.AddModelError("", "An error occurred while saving changes.");
                return PartialView("_EditPartial", op);
            }
        }



        // GET: Operators/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (op == null)
                return NotFound();

            // Get current user's role
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                               ?? User.FindFirst("Role")?.Value;

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Prevent deleting yourself
            if (currentUserId != null && op.Id.ToString() == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            // Security check: Admin can only delete Operators
            if (currentUserRole == "Admin" && op.Roles?.Name != "Operator")
            {
                TempData["ErrorMessage"] = "You can only delete Operator accounts.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent deleting the last Super Admin
            if (op.Roles?.Name == "Super Admin")
            {
                var superAdminCount = await _context.Operators
                    .Include(o => o.Roles)
                    .CountAsync(o => o.Roles.Name == "Super Admin");

                if (superAdminCount <= 1)
                {
                    TempData["ErrorMessage"] = "Cannot delete the last Super Admin account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(op);
        }


        // DELETE THE OLD Delete() and DeleteConfirmed() methods
        // KEEP ONLY THIS ONE:

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var op = await _context.Operators
                    .Include(o => o.Roles)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (op == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Get current user's role and ID
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                                   ?? User.FindFirst("Role")?.Value;
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Prevent deleting yourself
                if (currentUserId != null && op.Id.ToString() == currentUserId)
                {
                    return Json(new { success = false, message = "You cannot delete your own account." });
                }

                // Security check: Admin can only delete Operators
                if (currentUserRole == "Admin" && op.Roles?.Name != "Operator")
                {
                    return Json(new { success = false, message = "You can only delete Operator accounts." });
                }

                // Prevent deleting the last Super Admin
                if (op.Roles?.Name == "Super Admin")
                {
                    var superAdminCount = await _context.Operators
                        .Include(o => o.Roles)
                        .CountAsync(o => o.Roles.Name == "Super Admin");

                    if (superAdminCount <= 1)
                    {
                        return Json(new { success = false, message = "Cannot delete the last Super Admin account." });
                    }
                }

                _context.Operators.Remove(op);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"User '{op.Name}' deleted successfully." });
            }
            catch (Exception ex)
            {
                // Log the actual exception for debugging
                System.Diagnostics.Debug.WriteLine($"Delete Error: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }

        }
    }
}
