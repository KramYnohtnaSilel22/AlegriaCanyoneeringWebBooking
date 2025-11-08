using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin")]
    public class OperatorListController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperatorListController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: OperatorList/Index
        public IActionResult Index()
        {
            return View();
        }

        // POST: DataTables server-side data
        [HttpPost]
        public async Task<IActionResult> GetOperatorsData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                var query = _context.OperatorLists.AsQueryable();

                // Apply search filter
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(o =>
                        o.OwnerName.Contains(searchValue) ||
                        o.Gender.Contains(searchValue) ||
                        o.BusinessName.Contains(searchValue) ||
                        o.BussPermit.Contains(searchValue) ||
                        o.Location.Contains(searchValue)
                    );
                }

                // Get filtered count
                var recordsFiltered = await query.CountAsync();

                // Get total count (before filtering)
                var recordsTotal = await _context.OperatorLists.CountAsync();

                // Apply pagination
                var data = await query
                    .OrderBy(o => o.OperatorId)
                    .Skip(start)
                    .Take(length)
                    .Select(o => new
                    {
                        operatorId = o.OperatorId,
                        ownerName = o.OwnerName,
                        gender = o.Gender,
                        businessName = o.BusinessName,
                        bussPermit = o.BussPermit,
                        location = o.Location,
                        status = o.Status
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

        // GET: OperatorList/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: OperatorList/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OperatorList operatorModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(operatorModel);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Operator '{operatorModel.OwnerName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the operator.";
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(operatorModel);
        }

        // GET: OperatorList/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Operator ID is required.";
                return RedirectToAction(nameof(Index));
            }

            var operatorModel = await _context.OperatorLists.FindAsync(id);

            if (operatorModel == null)
            {
                TempData["ErrorMessage"] = "Operator not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(operatorModel);
        }

        // POST: OperatorList/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OperatorList operatorModel)
        {
            if (id != operatorModel.OperatorId)
            {
                TempData["ErrorMessage"] = "Invalid operator ID.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(operatorModel);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Operator '{operatorModel.OwnerName}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OperatorExists(operatorModel.OperatorId))
                    {
                        TempData["ErrorMessage"] = "Operator not found.";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while updating the operator.";
                        throw;
                    }
                }
            }
            return View(operatorModel);
        }

        // POST: OperatorList/Delete (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var operatorModel = await _context.OperatorLists.FindAsync(id);

                if (operatorModel == null)
                {
                    return Json(new { success = false, message = "Operator not found." });
                }

                _context.OperatorLists.Remove(operatorModel);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Operator '{operatorModel.OwnerName}' deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: OperatorList/ToggleStatus (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var operatorModel = await _context.OperatorLists.FindAsync(id);

                if (operatorModel == null)
                {
                    return Json(new { success = false, message = "Operator not found." });
                }

                // Toggle status (0 -> 1 or 1 -> 0)
                operatorModel.Status = operatorModel.Status == 1 ? 0 : 1;

                await _context.SaveChangesAsync();

                string statusText = operatorModel.Status == 1 ? "Active" : "Inactive";
                return Json(new
                {
                    success = true,
                    message = $"Operator '{operatorModel.OwnerName}' is now {statusText}.",
                    newStatus = operatorModel.Status
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private bool OperatorExists(int id)
        {
            return _context.OperatorLists.Any(e => e.OperatorId == id);
        }
    }
}