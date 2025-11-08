using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlegriaCanyoneeringWebBooking.Models;
using System.Security.Claims;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class NationalitiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NationalitiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Nationalities/Index
        public IActionResult Index()
        {
            return View();
        }

        // POST: DataTables server-side data
        [HttpPost]
        public async Task<IActionResult> GetNationalitiesData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                var query = _context.Nationalities.AsQueryable();

                // Apply search filter
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(n => n.NatName.Contains(searchValue));
                }

                // Get counts
                var recordsFiltered = await query.CountAsync();
                var recordsTotal = await _context.Nationalities.CountAsync();

                // Apply sorting and pagination
                var data = await query
                    .OrderBy(n => n.NatName)
                    .Skip(start)
                    .Take(length)
                    .Select(n => new
                    {
                        id = n.id,
                        natName = n.NatName
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsFiltered,
                    recordsTotal = recordsTotal,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server Error: {ex.Message}" });
            }
        }

        // GET: Nationalities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Nationality ID is required.";
                return RedirectToAction(nameof(Index));
            }

            var nationality = await _context.Nationalities.FindAsync(id);

            if (nationality == null)
            {
                TempData["ErrorMessage"] = "Nationality not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(nationality);
        }

        // GET: Nationalities/Create
        public IActionResult Create()
        {
            // Set the form action for the partial view
            ViewData["Action"] = "Create";

            // Return the partial view with a new Nationality model
            return PartialView("_CreatePartial", new Nationality());
        }

        // POST: Nationalities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Nationality nationality)
        {
            // Make sure the model is valid
            if (!ModelState.IsValid)
            {
                ViewData["Action"] = "Create";
                return PartialView("_CreatePartial", nationality);
            }

            // Check if the nationality already exists (case-insensitive)
            bool exists = await _context.Nationalities
                .AnyAsync(n => n.NatName.ToLower() == nationality.NatName.ToLower());

            if (exists)
            {
                ModelState.AddModelError("NatName", "This nationality already exists.");
                ViewData["Action"] = "Create";
                return PartialView("_CreatePartial", nationality);
            }

            try
            {
                // Add and save the new nationality
                _context.Nationalities.Add(nationality);
                await _context.SaveChangesAsync();

                // Return JSON success for AJAX
                return Json(new
                {
                    success = true,
                    message = $"Nationality '{nationality.NatName}' created successfully!"
                });
            }
            catch (Exception ex)
            {
                // Log the exception (optional)
                Console.Error.WriteLine(ex.Message);

                // Return JSON error for AJAX
                return Json(new
                {
                    success = false,
                    message = "An error occurred while saving the nationality."
                });
            }
        }


        // GET: Nationalities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return Json(new { success = false, message = "Nationality ID is required." });
            }

            var nationality = await _context.Nationalities.FindAsync(id);
            if (nationality == null)
            {
                return Json(new { success = false, message = "Nationality not found." });
            }

            // Pass the model to the partial view
            ViewData["Action"] = "Edit";
            return PartialView("_EditPartial", nationality);
        }

        // POST: Nationalities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Nationality nationality)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Action"] = "Edit";
                return PartialView("_EditPartial", nationality);
            }

            // Check for duplicate nationality (case-insensitive) excluding current record
            bool exists = await _context.Nationalities
                .AnyAsync(n => n.NatName.ToLower() == nationality.NatName.ToLower() && n.id != nationality.id);

            if (exists)
            {
                ModelState.AddModelError("NatName", "This nationality already exists.");
                ViewData["Action"] = "Edit";
                return PartialView("_EditPartial", nationality);
            }

            try
            {
                _context.Update(nationality);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Nationality '{nationality.NatName}' updated successfully!"
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new
                {
                    success = false,
                    message = "An error occurred while updating the nationality."
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return Json(new
                {
                    success = false,
                    message = "An unexpected error occurred."
                });
            }
        }


        // POST: Nationalities/DeleteAjax (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var nationality = await _context.Nationalities.FindAsync(id);

                if (nationality == null)
                {
                    return Json(new { success = false, message = "Nationality not found." });
                }

                // Check if nationality is being used by any guests
                var isUsed = await _context.Guests.AnyAsync(g => g.NationalityId == id);

                if (isUsed)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete '{nationality.NatName}' because it is being used by guests."
                    });
                }

                _context.Nationalities.Remove(nationality);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Nationality '{nationality.NatName}' deleted successfully!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        private bool NationalityExists(int id)
        {
            return _context.Nationalities.Any(e => e.id == id);
        }
    }
}