using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using AlegriaCanyoneeringWebBooking.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Operators
        public async Task<IActionResult> Operators()
        {
            var operators = await _context.Operators.ToListAsync();
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
        public async Task<IActionResult> CreateOperator(Operator operatorModel)
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
            var operatorModel = await _context.Operators.FindAsync(id);
            if (operatorModel == null)
            {
                return NotFound();
            }
            return View(operatorModel);
        }

        // POST: Admin/EditOperator/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOperator(int id, Operator operatorModel)
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
            return _context.Operators.Any(e => e.OperatorId == id);
        }
    }
}
