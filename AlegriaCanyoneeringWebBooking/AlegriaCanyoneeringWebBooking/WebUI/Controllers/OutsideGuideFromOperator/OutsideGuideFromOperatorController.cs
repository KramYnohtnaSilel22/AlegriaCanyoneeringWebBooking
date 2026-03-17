using AlegriaCanyoneeringWebBooking.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.WebUI.Controllers
{
    [Authorize(Roles = "Super Admin,Operator")]
    public class OutsideGuideFromOperatorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OutsideGuideFromOperatorController(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
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
        public async Task<IActionResult> GetOutsideGuideData()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = int.TryParse(Request.Form["start"].FirstOrDefault(), out var s) ? s : 0;
            var length = int.TryParse(Request.Form["length"].FirstOrDefault(), out var l) ? l : 10;
            var search = Request.Form["search[value]"].FirstOrDefault()?.ToLower() ?? "";

            try
            {
                // ── Filter by operator if current user is NOT Super Admin ──
                var query = _context.OutsideGuideFromOperators.AsQueryable();

                if (!User.IsInRole("Super Admin"))
                {
                    var username = User.Identity!.Name;
                    var loggedInOp = await _context.Operators
                                         .FirstOrDefaultAsync(o => o.Username == username);
                    if (loggedInOp != null)
                        query = query.Where(x => x.OperatorId == loggedInOp.Id.ToString());
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.OperatorId.ToLower().Contains(search) ||
                        x.OperatorName.ToLower().Contains(search) ||
                        x.GuideId.ToLower().Contains(search) ||
                        x.GuideName.ToLower().Contains(search));
                }

                var totalRecords = await _context.OutsideGuideFromOperators.CountAsync();
                var filteredCount = await query.CountAsync();

                var records = await query
                    .OrderBy(x => x.OperatorName)
                    .ThenBy(x => x.GuideName)
                    .Skip(start)
                    .Take(length)
                    .Select(x => new
                    {
                        id = x.Id,
                        operatorId = x.OperatorId,
                        operatorName = x.OperatorName,
                        guideId = x.GuideId,
                        guideName = x.GuideName
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
            return PartialView("_OutsideGuideForm", new OutsideGuideFromOperator());
        }

        // =========================================================
        // CREATE — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string operatorId, string operatorName,
            string guideId, string guideName)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || string.IsNullOrWhiteSpace(guideId))
                return Json(new { success = false, message = "Please select both an operator and a guide." });

            bool exists = await _context.OutsideGuideFromOperators
                .AnyAsync(x => x.OperatorId == operatorId && x.GuideId == guideId);
            if (exists)
                return Json(new { success = false, message = "This guide is already assigned to that operator." });

            _context.OutsideGuideFromOperators.Add(new OutsideGuideFromOperator
            {
                OperatorId = operatorId,
                OperatorName = operatorName,
                GuideId = guideId,
                GuideName = guideName
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
            var record = await _context.OutsideGuideFromOperators.FindAsync(id);
            if (record == null) return NotFound();
            return PartialView("_OutsideGuideForm", record);
        }

        // =========================================================
        // EDIT — POST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string operatorId, string operatorName,
            string guideId, string guideName)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || string.IsNullOrWhiteSpace(guideId))
                return Json(new { success = false, message = "Please select both an operator and a guide." });

            var existing = await _context.OutsideGuideFromOperators.FindAsync(id);
            if (existing == null)
                return Json(new { success = false, message = "Record not found." });

            bool duplicate = await _context.OutsideGuideFromOperators
                .AnyAsync(x => x.OperatorId == operatorId && x.GuideId == guideId && x.Id != id);
            if (duplicate)
                return Json(new { success = false, message = "This guide is already assigned to that operator." });

            existing.OperatorId = operatorId;
            existing.OperatorName = operatorName;
            existing.GuideId = guideId;
            existing.GuideName = guideName;

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
            var record = await _context.OutsideGuideFromOperators.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Record not found." });

            _context.OutsideGuideFromOperators.Remove(record);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Record deleted successfully." });
        }

        // =========================================================
        // GET DROPDOWNS — role-aware operator list
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetDropdowns()
        {
            List<object> operators;

            if (User.IsInRole("Super Admin"))
            {
                // Super Admin sees ALL operators
                operators = (await _context.Operators
                    .OrderBy(o => o.BusinessName)
                    .Select(o => new
                    {
                        id = o.Id.ToString(),
                        businessName = (o.BusinessName != null && o.BusinessName.Trim() != "")
                                          ? o.BusinessName
                                          : o.Name ?? ""
                    })
                    .ToListAsync())
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                // Operator role — only sees their own business name
                var username = User.Identity!.Name;
                var loggedInOp = await _context.Operators
                    .FirstOrDefaultAsync(o => o.Username == username);

                operators = loggedInOp != null
                    ? new List<object>
                    {
                        new
                        {
                            id           = loggedInOp.Id.ToString(),
                            businessName = (loggedInOp.BusinessName != null && loggedInOp.BusinessName.Trim() != "")
                                              ? loggedInOp.BusinessName
                                              : loggedInOp.Name ?? ""
                        }
                    }
                    : new List<object>();
            }

            var guides = await _context.Guides
                .OrderBy(g => g.LName)
                .Select(g => new
                {
                    rfid = g.Rfid,
                    fullName = (g.LName.ToUpper() + ", " + g.FName.ToUpper()
                               + (g.MName != null && g.MName.Trim() != ""
                                   ? " " + g.MName.ToUpper()
                                   : "")).Trim()
                })
                .ToListAsync();

            return Json(new { operators, guides });
        }
    }
}