using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class OperatorListController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperatorListController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        public async Task<IActionResult> GetOperatorsData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                var search = Request.Form["search[value]"].FirstOrDefault();

                var query = _context.OperatorLists.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(o =>
                        o.OwnerName.Contains(search) ||
                        o.Gender.Contains(search) ||
                        o.BusinessName.Contains(search) ||
                        o.BussPermit.Contains(search) ||
                        o.Location.Contains(search)
                    );
                }

                var recordsTotal = await query.CountAsync();

                var data = await query
                    .OrderBy(o => o.OperatorId)
                    .Skip(start)
                    .Take(length)
                    .ToListAsync();

                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsTotal,
                    recordsTotal = recordsTotal,
                    data = data.Select(o => new
                    {
                        operatorId = o.OperatorId,
                        ownerName = o.OwnerName,
                        gender = o.Gender,
                        businessName = o.BusinessName,
                        bussPermit = o.BussPermit,
                        location = o.Location,
                        status = o.Status,
                        isActive = o.Status == 1
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Server Error: {ex.Message}" });
            }
        }


        // GET: Admin/Operators
        public async Task<IActionResult> Operators()
        {
            var operators = await _context.OperatorLists.ToListAsync();
            return View(operators);
        }

        // GET: Admin/CreateOperator
        public IActionResult CreateOperator()
        {
            return View();
        }

        // POST: Admin/CreateOperator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOperator(Models.OperatorList operatorModel)
        {
            if (ModelState.IsValid)
            {
                // You can add any custom validation here if needed

                _context.Add(operatorModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Operators));
            }
            return View(operatorModel);
        }

        // GET: Admin/EditOperator/5
        public async Task<IActionResult> EditOperator(int id)
        {
            var operatorModel = await _context.OperatorLists.FindAsync(id);
            if (operatorModel == null)
            {
                return NotFound();
            }
            return View(operatorModel);
        }

        // POST: Admin/EditOperator/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOperator(int id, Models.OperatorList operatorModel)
        {
            if (id != operatorModel.OperatorId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(operatorModel);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OperatorExists(operatorModel.OperatorId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Operators));
            }
            return View(operatorModel);
        }

        private bool OperatorExists(int id)
        {
            return _context.OperatorLists.Any(e => e.OperatorId == id);
        }
    }
}
