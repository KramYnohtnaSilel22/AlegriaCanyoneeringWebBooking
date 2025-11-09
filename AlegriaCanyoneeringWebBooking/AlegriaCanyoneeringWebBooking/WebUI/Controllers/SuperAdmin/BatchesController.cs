using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class BatchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BatchesController> _logger;

        public BatchesController(ApplicationDbContext context, ILogger<BatchesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // MAIN VIEW
        public IActionResult Index() => View();

        // ✅ DataTables endpoint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetBatches()
        {
            try
            {
                // DataTables params
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

                var query = _context.Batches
                    .Include(b => b.Operators)
                    .AsNoTracking();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    var pattern = $"%{searchValue}%";
                    query = query.Where(b =>
                        EF.Functions.Like(b.Operators.BusinessName!, pattern) ||
                        EF.Functions.Like(b.ArrivalDate!, pattern)
                    );
                }

                var totalRecords = await _context.Batches.CountAsync();
                var filteredRecords = await query.CountAsync();

                var data = await query
                    .OrderByDescending(b => b.BatchId)
                    .Skip(start)
                    .Take(length)
                    .Select(b => new
                    {
                        id = b.BatchId,
                        operatorName = b.Operators.BusinessName,
                        localGuests = b.NoOfLocalGuest,
                        foreignGuests = b.NoOfForeignGuest,
                        guides = b.NoOfTGuide,
                        drivers = b.NoOfMDriver,
                        totalGuests = b.TotalNoOfGuest,
                        arrivalUnix = b.ArrivalDate // 👈 keep as raw Unix timestamp (string or long)
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = filteredRecords,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading batches");
                return Json(new { error = "Error loading batch data." });
            }
        }

        // ✅ AJAX Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var batch = await _context.Batches.FindAsync(id);
                if (batch == null)
                    return Json(new { success = false, message = "Batch not found." });

                _context.Batches.Remove(batch);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Batch deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting batch");
                return Json(new { success = false, message = "Error deleting batch." });
            }
        }
    }
}
