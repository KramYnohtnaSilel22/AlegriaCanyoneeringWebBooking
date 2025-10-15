
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
        public IActionResult Create()
        {
            var currentUserRole = User.FindFirst("Role")?.Value;

            if (currentUserRole == "Super Admin")
            {
                // Super Admin can create all types of users (Admin, Operator, Super Admin)
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name");
            }
            else if (currentUserRole == "Admin")
            {
                // Admin can only create Operators
                ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");
            }
            else
            {
                // No access for Operators or other roles
                return Forbid();
            }

            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            return View();
        }

        // POST: Operators/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,BusinessName,Age,Gender,Username,Password,EmailAddress,RoleId")] Operator op)
        {
            var currentUserRole = User.FindFirst("Role")?.Value;

            // Security check: Only Super Admin and Admin can create users
            if (currentUserRole != "Super Admin" && currentUserRole != "Admin")
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                // Security check: Admin can ONLY create Operators
                if (currentUserRole == "Admin")
                {
                    var operatorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Operator");
                    if (operatorRole == null)
                    {
                        ModelState.AddModelError("", "Operator role not found in the system.");
                        ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");
                        ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
                        return View(op);
                    }

                    // Force the role to be Operator, ignore whatever was submitted
                    op.RoleId = operatorRole.RoleId;
                }
                else if (currentUserRole == "Super Admin")
                {
                    // Super Admin can create any role, but validate that the selected role exists
                    var selectedRole = await _context.Roles.FindAsync(op.RoleId);
                    if (selectedRole == null)
                    {
                        ModelState.AddModelError("RoleId", "Invalid role selected.");
                        ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                        ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
                        return View(op);
                    }
                }

                // Check if username already exists
                var existingUser = await _context.Operators.FirstOrDefaultAsync(u => u.Username == op.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");

                    // Refill dropdowns
                    if (currentUserRole == "Super Admin")
                        ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                    else
                        ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name");

                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
                    return View(op);
                }

                // Hash password before saving
                op.Password = PasswordHelper.HashPassword(op.Password);

                _context.Add(op);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "User created successfully!";
                return RedirectToAction(nameof(Index));
            }

            // Refill dropdowns on validation failure
            if (currentUserRole == "Super Admin")
            {
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            }
            else if (currentUserRole == "Admin")
            {
                ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name", op.RoleId);
            }

            ViewBag.GenderList = new SelectList(new[] { "Male", "Female" });
            return View(op);
        }

        // GET: Operators/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var op = await _context.Operators.Include(o => o.Roles).FirstOrDefaultAsync(o => o.Id == id);
            if (op == null)
                return NotFound();

            // Get current user's role
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                               ?? User.FindFirst("Role")?.Value;

            if (string.IsNullOrEmpty(currentUserRole))
            {
                TempData["ErrorMessage"] = "Unable to determine user role. Please log in again.";
                return RedirectToAction("Login", "Authentication");
            }

            // Role-based dropdown restrictions
            if (currentUserRole == "Super Admin")
            {
                // Super Admin can assign any role
                ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
            }
            else if (currentUserRole == "Admin")
            {
                // Admin can only assign Operator role
                var operatorRoles = _context.Roles.Where(r => r.Name == "Operator").ToList();

                // Admins can only edit Operators, not other Admins or Super Admins
                if (op.Roles?.Name != "Operator")
                {
                    TempData["ErrorMessage"] = "You can only edit Operator accounts.";
                    return RedirectToAction(nameof(Index));
                }

                ViewData["RoleId"] = new SelectList(operatorRoles, "RoleId", "Name", op.RoleId);
            }
            else
            {
                // Operators cannot edit users
                return Forbid();
            }

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
            if (id != op.Id)
                return BadRequest();

            // Get current user's role
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value
                               ?? User.FindFirst("Role")?.Value;

            // Security check: Only Super Admin and Admin can edit users
            if (currentUserRole != "Super Admin" && currentUserRole != "Admin")
            {
                return Forbid();
            }

            // Get the existing user with their current role
            var existing = await _context.Operators.Include(o => o.Roles).FirstOrDefaultAsync(o => o.Id == id);
            if (existing == null)
                return NotFound();

            // Security check: Admin can ONLY edit Operators
            if (currentUserRole == "Admin")
            {
                // Check if trying to edit a non-Operator
                if (existing.Roles?.Name != "Operator")
                {
                    TempData["ErrorMessage"] = "You can only edit Operator accounts.";
                    return RedirectToAction(nameof(Index));
                }

                // Force role to remain Operator - Admin cannot change roles
                var operatorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Operator");
                if (operatorRole == null)
                {
                    ModelState.AddModelError("", "Operator role not found in the system.");
                    ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name", op.RoleId);
                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                    return View(op);
                }
                op.RoleId = operatorRole.RoleId; // Force to Operator role
            }
            else if (currentUserRole == "Super Admin")
            {
                // Super Admin can change roles, but validate the selected role exists
                var selectedRole = await _context.Roles.FindAsync(op.RoleId);
                if (selectedRole == null)
                {
                    ModelState.AddModelError("RoleId", "Invalid role selected.");
                    ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                    return View(op);
                }
            }

            if (!ModelState.IsValid)
            {
                // Refill dropdowns based on role
                if (currentUserRole == "Super Admin")
                    ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                else
                    ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name", op.RoleId);

                ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                return View(op);
            }

            // Check if username is being changed and if it already exists
            if (existing.Username != op.Username)
            {
                var usernameExists = await _context.Operators
                    .AnyAsync(u => u.Username == op.Username && u.Id != id);

                if (usernameExists)
                {
                    ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");

                    // Refill dropdowns
                    if (currentUserRole == "Super Admin")
                        ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                    else
                        ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name", op.RoleId);

                    ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                    return View(op);
                }
            }

            // Update fields
            existing.Name = op.Name;
            existing.BusinessName = op.BusinessName;
            existing.Age = op.Age;
            existing.Gender = op.Gender;
            existing.Username = op.Username;
            existing.EmailAddress = op.EmailAddress;
            existing.RoleId = op.RoleId;

            // Password handling - only update if a new password is provided
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                existing.Password = PasswordHelper.HashPassword(NewPassword);
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User updated successfully!";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving changes.");

                // Refill dropdowns
                if (currentUserRole == "Super Admin")
                    ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "Name", op.RoleId);
                else
                    ViewData["RoleId"] = new SelectList(_context.Roles.Where(r => r.Name == "Operator"), "RoleId", "Name", op.RoleId);

                ViewBag.GenderList = new SelectList(new[] { "Male", "Female" }, op.Gender);
                return View(op);
            }

            return RedirectToAction(nameof(Index));
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

        // POST: Operators/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (op == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Get current user's role and ID
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
                    TempData["ErrorMessage"] = "Cannot delete the last Super Admin account. At least one Super Admin must exist.";
                    return RedirectToAction(nameof(Index));
                }
            }

            try
            {
                _context.Operators.Remove(op);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"User '{op.Name}' deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the user.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
