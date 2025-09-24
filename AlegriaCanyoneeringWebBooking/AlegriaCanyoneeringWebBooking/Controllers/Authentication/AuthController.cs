using AlegriaCanyoneeringWebBooking.Helpers;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AlegriaCanyoneeringWebBooking.Controllers
{

    public class AuthController : Controller
    {

        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }


        // Add this GET action for displaying the login form
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(o => o.Username == model.Username);

            if (op == null || op.Password == null ||
                !PasswordHelper.VerifyPassword(model.Password, op.Password))
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, op.Username!),
            new Claim(ClaimTypes.NameIdentifier, op.OperatorId.ToString()),
            new Claim(ClaimTypes.Role, op.Roles?.Name ?? "User")
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            // Set TempData for login success
            TempData["LoginSuccess"] = "Login successful! Welcome back.";
            return RedirectToAction("NewBooking", "Guest");


        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }


        // GET: Change Password
        [HttpGet]
        [Authorize(Roles = "Super Admin,Admin,Operator")]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: Change Password
        [Authorize(Roles = "Super Admin,Admin,Operator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Get current logged-in user
            var username = User.Identity?.Name;
            if (username == null)
            {
                return RedirectToAction("Login", "Authentication");
            }

            var op = await _context.Operators.FirstOrDefaultAsync(o => o.Username == username);
            if (op == null)
            {
                return RedirectToAction("Login", "Authentication");
            }

            // Verify current password
            if (!PasswordHelper.VerifyPassword(model.CurrentPassword, op.Password))
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return View(model);
            }

            // Update password
            op.Password = PasswordHelper.HashPassword(model.NewPassword);
            _context.Update(op);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("NewBooking", "Guest"); // Redirect wherever appropriate
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
