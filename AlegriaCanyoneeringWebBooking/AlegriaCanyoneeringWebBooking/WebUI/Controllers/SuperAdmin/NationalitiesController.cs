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
            return View();
        }

        // POST: Nationalities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Nationality nationality)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check for duplicate nationality name
                    var existingNationality = await _context.Nationalities
                        .FirstOrDefaultAsync(n => n.NatName.ToLower() == nationality.NatName.ToLower());

                    if (existingNationality != null)
                    {
                        ModelState.AddModelError("NatName", "This nationality already exists.");
                        return View(nationality);
                    }

                    _context.Add(nationality);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Nationality '{nationality.NatName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the nationality.";
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(nationality);
        }

        // GET: Nationalities/Edit/5
        public async Task<IActionResult> Edit(int? id)
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

        // POST: Nationalities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Nationality nationality)
        {
            if (id != nationality.id)
            {
                TempData["ErrorMessage"] = "Invalid nationality ID.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check for duplicate nationality name (excluding current record)
                    var existingNationality = await _context.Nationalities
                        .FirstOrDefaultAsync(n => n.NatName.ToLower() == nationality.NatName.ToLower()
                                                && n.id != id);

                    if (existingNationality != null)
                    {
                        ModelState.AddModelError("NatName", "This nationality already exists.");
                        return View(nationality);
                    }

                    _context.Update(nationality);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Nationality '{nationality.NatName}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NationalityExists(nationality.id))
                    {
                        TempData["ErrorMessage"] = "Nationality not found.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while updating the nationality.";
                        throw;
                    }
                }
            }
            return View(nationality);
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