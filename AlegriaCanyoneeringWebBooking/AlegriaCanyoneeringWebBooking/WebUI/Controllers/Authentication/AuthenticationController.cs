using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AlegriaCanyoneeringWebBooking.Helpers;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AuthenticationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthenticationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // LOGIN — GET
        // =========================================================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");
            return View();
        }

        // =========================================================
        // LOGIN — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            // ── Blank checks ───────────────────────────────────────
            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Please fill up both username and password.";
                return View();
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["ErrorMessage"] = "Please fill up your username.";
                return View();
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Please fill up your password.";
                return View();
            }

            // ── Find user ──────────────────────────────────────────
            var user = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Invalid username or password.";
                return View();
            }

            // ── Verify using SHA1 PasswordHelper ───────────────────
            // ✅ This now works for ALL passwords:
            //    - Original accounts (SHA1 hashed at registration)
            //    - Passwords changed via ChangePassword  (SHA1)
            //    - Passwords reset via ForgotPassword    (SHA1)
            if (!PasswordHelper.VerifyPassword(password, user.Password))
            {
                TempData["ErrorMessage"] = "Invalid username or password.";
                return View();
            }

            // ── Role check ─────────────────────────────────────────
            if (user.Roles == null)
            {
                TempData["ErrorMessage"] = "User role not found. Please contact the administrator.";
                return View();
            }

            // ── Build claims ───────────────────────────────────────
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Email,          user.EmailAddress ?? ""),
                new Claim(ClaimTypes.Role,           user.Roles.Name),
                new Claim("UserId",       user.Id.ToString()),
                new Claim("Role",         user.Roles.Name),
                new Claim("RoleName",     user.Roles.Name),
                new Claim("BusinessName", user.BusinessName ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                }
            );

            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Roles.Name);
            HttpContext.Session.SetInt32("UserId", user.Id);

            return RedirectToAction("Index", "Dashboard");
        }

        // =========================================================
        // LOGOUT
        // =========================================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("landingpage", "Home");
        }

        // =========================================================
        // CHANGE PASSWORD — GET
        // =========================================================
        [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // =========================================================
        // CHANGE PASSWORD — POST
        // =========================================================
        [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = User.Identity?.Name;
            if (username == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            var op = await _context.Operators.FirstOrDefaultAsync(o => o.Username == username);
            if (op == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // ✅ Verify current password with SHA1
            if (!PasswordHelper.VerifyPassword(model.CurrentPassword, op.Password))
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return View(model);
            }

            // ✅ Hash new password with SHA1
            op.Password = PasswordHelper.HashPassword(model.NewPassword);
            _context.Update(op);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("ChangePassword");
        }

        // =========================================================
        // FORGOT PASSWORD — GET
        // =========================================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // =========================================================
        // FORGOT PASSWORD — POST
        // =========================================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Operators
                .FirstOrDefaultAsync(u => u.EmailAddress == model.Email);

            if (user == null)
                return View("ForgotPasswordConfirmation");

            var resetToken = Guid.NewGuid().ToString("N");
            var resetLink = Url.Action("ResetPassword", "Authentication",
                new { email = user.EmailAddress, token = resetToken }, Request.Scheme);
            string imageUrl = $"{Request.Scheme}://{Request.Host}/images/alegrialogo2025.jpeg";

            var emailBody = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1' />
  <title>Reset Your Password</title>
</head>
<body style='margin:0;padding:0;background-color:#f4f7fe;font-family:Outfit,Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f7fe;padding:40px 0;'>
    <tr>
      <td align='center'>
        <table width='600' cellpadding='0' cellspacing='0'
               style='background:#ffffff;border-radius:16px;overflow:hidden;
                      box-shadow:0 4px 24px rgba(15,52,96,0.10);max-width:600px;width:100%;'>
          <tr>
            <td style='background:linear-gradient(135deg,#1a6ef5,#0f3460);padding:36px 40px;text-align:center;'>
              <img src='{imageUrl}' alt='Alegria Canyoneering' width='70' height='70'
                   style='border-radius:50%;border:3px solid rgba(255,255,255,0.3);object-fit:cover;margin-bottom:14px;' />
              <h1 style='margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:0.5px;'>Alegria Canyoneering</h1>
              <p style='margin:4px 0 0;color:rgba(255,255,255,0.7);font-size:12px;letter-spacing:0.1em;text-transform:uppercase;'>Web Booking System</p>
            </td>
          </tr>
          <tr>
            <td style='padding:40px 40px 32px;'>
              <div style='width:64px;height:64px;border-radius:50%;background:rgba(26,110,245,0.08);
                          margin:0 auto 24px;text-align:center;line-height:64px;'>
                <span style='font-size:28px;'>🔑</span>
              </div>
              <h2 style='margin:0 0 8px;color:#0f3460;font-size:20px;font-weight:700;text-align:center;'>Password Reset Request</h2>
              <p style='margin:0 0 24px;color:#64748b;font-size:14px;text-align:center;'>We received a request to reset your password.</p>
              <p style='margin:0 0 8px;color:#334155;font-size:15px;'>Hello <strong>{user.Name}</strong>,</p>
              <p style='margin:0 0 28px;color:#475569;font-size:15px;line-height:1.65;'>
                Click the button below to choose a new password for your account. This link is valid for a limited time.
              </p>
              <div style='text-align:center;margin-bottom:28px;'>
                <a href='{resetLink}'
                   style='display:inline-block;background:linear-gradient(135deg,#1a6ef5,#0f3460);
                          color:#ffffff;text-decoration:none;padding:14px 36px;
                          border-radius:50px;font-size:15px;font-weight:600;
                          box-shadow:0 4px 16px rgba(26,110,245,0.35);'>
                  Reset My Password
                </a>
              </div>
              <hr style='border:none;border-top:1px solid #e8edf5;margin:0 0 20px;' />
              <p style='margin:0;color:#94a3b8;font-size:13px;line-height:1.6;text-align:center;'>
                If you did not request a password reset, you can safely ignore this email.<br>
                Your password will remain unchanged.
              </p>
            </td>
          </tr>
          <tr>
            <td style='background:#f8faff;border-top:1px solid #e8edf5;padding:20px 40px;text-align:center;'>
              <p style='margin:0;color:#94a3b8;font-size:12px;line-height:1.6;'>
                &copy; {DateTime.Now.Year} <strong style='color:#64748b;'>Alegria Canyoneering Web Booking</strong>. All rights reserved.
              </p>
              <p style='margin:6px 0 0;color:#b0bec5;font-size:11px;'>This is an automated message. Please do not reply to this email.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            await new EmailService().SendEmailAsync(
                user.EmailAddress,
                "Reset your password — Alegria Canyoneering",
                emailBody
            );

            return View("ForgotPasswordConfirmation");
        }

        // =========================================================
        // RESET PASSWORD — GET
        // =========================================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        // =========================================================
        // RESET PASSWORD — POST
        // ✅ KEY FIX: use PasswordHelper.HashPassword (SHA1)
        //    so Login can verify it with PasswordHelper.VerifyPassword
        // =========================================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Operators
                .FirstOrDefaultAsync(u => u.EmailAddress == model.Email);

            if (user == null)
                return View("ResetPasswordConfirmation");

            // ✅ SHA1 hash — consistent with Login and ChangePassword
            user.Password = PasswordHelper.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            return View("ResetPasswordConfirmation");
        }

        // =========================================================
        // UPDATE INFO — GET
        // =========================================================
        [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
        [HttpGet("/Authentication/Update")]
        public async Task<IActionResult> Update()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim))
                return RedirectToAction("Login");

            if (!int.TryParse(idClaim, out int operatorId))
                return BadRequest("Invalid operator id.");

            var op = await _context.Operators
                .Include(o => o.Roles)
                .FirstOrDefaultAsync(o => o.Id == operatorId);

            if (op == null) return NotFound();

            return View(new OperatorUpdateViewModel
            {
                OperatorId = op.Id,
                Name = op.Name,
                BusinessName = op.BusinessName,
                Age = op.Age,
                Gender = op.Gender ?? string.Empty,
                Username = op.Username ?? string.Empty,
                EmailAddress = op.EmailAddress,
                RoleId = op.RoleId,
                RoleName = op.Roles?.Name
            });
        }

        // =========================================================
        // UPDATE INFO — POST
        // =========================================================
        [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
        [HttpPost("/Authentication/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(OperatorUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors in the form.";
                return View(model);
            }

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim) || model.OperatorId.ToString() != idClaim)
            {
                TempData["ErrorMessage"] = "Unauthorized operation.";
                return Forbid();
            }

            var op = await _context.Operators.FindAsync(model.OperatorId);
            if (op == null)
            {
                TempData["ErrorMessage"] = "Operator not found.";
                return RedirectToAction("Index", "Dashboard");
            }

            op.Name = model.Name;
            op.BusinessName = model.BusinessName;
            op.Age = model.Age;
            op.Gender = model.Gender;
            op.Username = model.Username;
            op.EmailAddress = model.EmailAddress;
            op.RoleId = model.RoleId;

            _context.Update(op);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Information updated successfully!";
            return RedirectToAction("Update");
        }

        // =========================================================
        // ACCESS DENIED
        // =========================================================
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}