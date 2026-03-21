using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            _logger  = logger;
        }

        // =========================================================
        // INDEX
        // =========================================================
        public IActionResult Index() => View();

        // =========================================================
        // DATATABLE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetBatches()
        {
            try
            {
                var draw        = Request.Form["draw"].FirstOrDefault();
                var start       = Convert.ToInt32(Request.Form["start"].FirstOrDefault()  ?? "0");
                var length      = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault()?.ToLower();

                var query = _context.Batches
                    .Include(b => b.Operators)
                    .AsNoTracking();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    var pattern = $"%{searchValue}%";
                    query = query.Where(b =>
                        EF.Functions.Like(b.Operators.BusinessName!, pattern) ||
                        EF.Functions.Like(b.ArrivalDate!,            pattern)
                    );
                }

                var totalRecords    = await _context.Batches.CountAsync();
                var filteredRecords = await query.CountAsync();

                var data = await query
                    .OrderByDescending(b => b.BatchId)
                    .Skip(start)
                    .Take(length)
                    .Select(b => new
                    {
                        id            = b.BatchId,
                        operatorName  = b.Operators.BusinessName,
                        localGuests   = b.NoOfLocalGuest,
                        foreignGuests = b.NoOfForeignGuest,
                        guides        = b.NoOfTGuide,
                        drivers       = b.NoOfMDriver,
                        totalGuests   = b.TotalNoOfGuest,
                        arrivalUnix   = b.ArrivalDate
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw,
                    recordsTotal    = totalRecords,
                    recordsFiltered = filteredRecords,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading batches");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // =========================================================
        // DELETE — AJAX
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            try
            {
                var batch = await _context.Batches.FindAsync(id);
                if (batch == null)
                    return Json(new { success = false, message = "Batch not found." });

                // Check FK dependencies — add more checks here if needed
                // e.g. bool hasBookings = await _context.Bookings.AnyAsync(b => b.BatchId == id);
                // if (hasBookings) return Json(new { success = false, message = "Cannot delete — batch has bookings." });

                _context.Batches.Remove(batch);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Batch #{id} deleted successfully." });
            }
            catch (DbUpdateException ex) when (
                ex.InnerException?.Message.Contains("FOREIGN KEY")  == true ||
                ex.InnerException?.Message.Contains("REFERENCE")    == true)
            {
                return Json(new { success = false, message = "Cannot delete — this batch is linked to existing records." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting batch {Id}", id);
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }
    }
}