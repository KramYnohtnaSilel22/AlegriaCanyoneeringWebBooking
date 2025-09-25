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

    public class AuthenticationController : Controller
    {

        private readonly ApplicationDbContext _context;

        public AuthenticationController(ApplicationDbContext context)
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

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Operators.FirstOrDefaultAsync(u => u.EmailAddress == model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return View("ForgotPasswordConfirmation");
            }

            // Generate reset token (for demonstration — use your own mechanism if not using Identity)
            var resetToken = Guid.NewGuid().ToString();
            var resetLink = Url.Action("ResetPassword", "Authentication",
                new { email = user.EmailAddress, token = resetToken }, Request.Scheme);

            // SEND EMAIL — integrate here
            // Build a simple but styled HTML email body
            var emailBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset='utf-8'>
                  <style>
                    body {{
                        font-family: Arial, sans-serif;
                        background-color: #f7f7f7;
                        margin: 0;
                        padding: 0;
                    }}
                    .container {{
                        max-width: 600px;
                        margin: 40px auto;
                        background: #ffffff;
                        padding: 30px;
                        border-radius: 8px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.1);
                    }}
                    h2 {{
                        color: #2c3e50;
                        margin-top: 0;
                    }}
                    p {{
                        font-size: 15px;
                        color: #333333;
                        line-height: 1.5;
                    }}
                    a.button {{
                        display: inline-block;
                        margin-top: 20px;
                        background: #0d6efd;
                        color: #ffffff !important;
                        text-decoration: none;
                        padding: 12px 20px;
                        border-radius: 5px;
                        font-weight: bold;
                    }}
                    a.button:hover {{
                        background: #0b5ed7;
                    }}
                    .footer {{
                        margin-top: 30px;
                        font-size: 12px;
                        color: #888888;
                        text-align: center;
                    }}
                  </style>
                </head>
                <body>
                  <div class='container'>
                    <h2>Password Reset Request</h2>
                    <p>Hello {user.Name},</p>
                    <p>We received a request to reset your password. Click the button below to choose a new one:</p>
                    <p><a class='button' href='{resetLink}'>Reset Password</a></p>
                    <p>If you did not request a password reset, you can safely ignore this email.</p>
                    <div class='footer'>
                      © {DateTime.Now.Year} Alegria Canyoneering Web Booking.
                    </div>
                  </div>
                </body>
                </html>";

            await new EmailService().SendEmailAsync(
                user.EmailAddress,
                "Reset your password",
                emailBody
            );


            return View("ForgotPasswordConfirmation");
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _context.Operators.FirstOrDefaultAsync(u => u.EmailAddress == model.Email);
            if (user == null)
            {
                // Not found — show confirmation anyway
                return View("ResetPasswordConfirmation");
            }
            // Validate token and expiry here

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            return View("ResetPasswordConfirmation");
        }



        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
