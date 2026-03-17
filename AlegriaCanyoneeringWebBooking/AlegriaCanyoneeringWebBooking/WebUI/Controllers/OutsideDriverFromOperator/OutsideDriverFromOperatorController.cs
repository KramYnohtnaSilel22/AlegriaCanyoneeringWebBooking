using AlegriaCanyoneeringWebBooking.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.WebUI.Controllers
{
    public class OutsideDriverFromOperatorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OutsideDriverFromOperatorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // INDEX
        // =========================================================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // =========================================================
        // GET DATA — DataTables server-side
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetOutsideDriverData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.TryParse(Request.Form["start"].FirstOrDefault(), out var s) ? s : 0;
            var length = int.TryParse(Request.Form["length"].FirstOrDefault(), out var l) ? l : 10;
            var search = Request.Form["search[value]"].FirstOrDefault()?.ToLower() ?? "";

            try
            {
                var query = _context.OutsideDriverFromOperators.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.OperatorId.ToLower().Contains(search) ||
                        x.OperatorName.ToLower().Contains(search) ||
                        x.DriverId.ToLower().Contains(search) ||
                        x.DriverName.ToLower().Contains(search));
                }

                var totalRecords = await _context.OutsideDriverFromOperators.CountAsync();
                var filteredCount = await query.CountAsync();

                var records = await query
                    .OrderBy(x => x.OperatorName)
                    .ThenBy(x => x.DriverName)
                    .Skip(start)
                    .Take(length)
                    .Select(x => new
                    {
                        id = x.Id,
                        operatorId = x.OperatorId,
                        operatorName = x.OperatorName,
                        driverId = x.DriverId,
                        driverName = x.DriverName
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = filteredCount,
                    data = records
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // =========================================================
        // CREATE — GET (partial form for modal)
        // =========================================================
        [HttpGet]
        public IActionResult Create()
        {
            return PartialView("_OutsideDriverForm", new OutsideDriverFromOperator());
        }

        // =========================================================
        // CREATE — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string operatorId, string operatorName,
            string driverId, string driverName)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || string.IsNullOrWhiteSpace(driverId))
                return Json(new { success = false, message = "Please select both an operator and a driver." });

            bool exists = await _context.OutsideDriverFromOperators
                .AnyAsync(x => x.OperatorId == operatorId && x.DriverId == driverId);
            if (exists)
                return Json(new { success = false, message = "This driver is already assigned to that operator." });

            _context.OutsideDriverFromOperators.Add(new OutsideDriverFromOperator
            {
                OperatorId = operatorId,
                OperatorName = operatorName,
                DriverId = driverId,
                DriverName = driverName
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Record added successfully." });
        }

        // =========================================================
        // EDIT — GET (partial form for modal)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var record = await _context.OutsideDriverFromOperators.FindAsync(id);
            if (record == null) return NotFound();
            return PartialView("_OutsideDriverForm", record);
        }

        // =========================================================
        // EDIT — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string operatorId, string operatorName,
            string driverId, string driverName)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || string.IsNullOrWhiteSpace(driverId))
                return Json(new { success = false, message = "Please select both an operator and a driver." });

            var existing = await _context.OutsideDriverFromOperators.FindAsync(id);
            if (existing == null)
                return Json(new { success = false, message = "Record not found." });

            bool duplicate = await _context.OutsideDriverFromOperators
                .AnyAsync(x => x.OperatorId == operatorId && x.DriverId == driverId && x.Id != id);
            if (duplicate)
                return Json(new { success = false, message = "This driver is already assigned to that operator." });

            existing.OperatorId = operatorId;
            existing.OperatorName = operatorName;
            existing.DriverId = driverId;
            existing.DriverName = driverName;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Record updated successfully." });
        }

        // =========================================================
        // DELETE — AJAX POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var record = await _context.OutsideDriverFromOperators.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Record not found." });

            _context.OutsideDriverFromOperators.Remove(record);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Record deleted successfully." });
        }

        // =========================================================
        // GET DROPDOWNS — for modal selects (JSON)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetDropdowns()
        {
            var operators = await _context.Operators
                .OrderBy(o => o.BusinessName)
                .Select(o => new
                {
                    id = o.Id.ToString(),
                    businessName = o.BusinessName ?? o.Name ?? ""
                })
                .ToListAsync();

            // Driver uses RefId as the stored DriverId (same as OutsideGuide using Rfid)
            var drivers = await _context.Drivers
                .OrderBy(d => d.LName)
                .Select(d => new
                {
                    refId = d.RefId,
                    fullName = (d.LName.ToUpper() + ", " + d.FName.ToUpper()
                               + (d.MName != null && d.MName.Trim() != ""
                                   ? " " + d.MName.ToUpper()
                                   : "")).Trim()
                })
                .ToListAsync();

            return Json(new { operators, drivers });
        }
    }
}