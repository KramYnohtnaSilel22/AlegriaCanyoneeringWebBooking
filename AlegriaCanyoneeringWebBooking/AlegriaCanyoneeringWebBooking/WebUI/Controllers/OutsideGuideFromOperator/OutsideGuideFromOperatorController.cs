using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Route("OutsideGuideFromOperator")]
[Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
public class OutsideGuideFromOperatorController : Controller
{
    private readonly ApplicationDbContext _context;

    public OutsideGuideFromOperatorController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private bool IsSuperAdmin => User.IsInRole("Super Admin");
    private bool IsOperator => User.IsInRole("Operator");

    private async Task<Operator?> GetCurrentOperatorAsync()
    {
        var username = User.Identity?.Name
                    ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _context.Operators.FirstOrDefaultAsync(o => o.Username == username);
    }

    // =========================================================
    // INDEX
    // =========================================================
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => View();

    // =========================================================
    // DATATABLE — server-side
    // =========================================================
    [HttpPost("GetOutsideGuideData")]
    public async Task<IActionResult> GetOutsideGuideData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.OutsideGuideFromOperators.AsQueryable();

            // ── Operators see only their own records ──────────────────────────
            if (IsOperator && !IsSuperAdmin)
            {
                var op = await GetCurrentOperatorAsync();
                if (op == null)
                    return Json(new { draw, recordsFiltered = 0, recordsTotal = 0, data = new List<object>() });

                query = query.Where(r => r.OperatorId == op.Id.ToString());
            }

            // ── recordsTotal = count BEFORE search filter ─────────────────────
            var recordsTotal = await query.CountAsync();

            // ── Apply search ──────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(r =>
                    r.OperatorName.Contains(searchValue) ||
                    r.OutsideGuideName.Contains(searchValue) ||
                    r.OutsideGuideId.Contains(searchValue));
            }

            // ── recordsFiltered = count AFTER search filter ───────────────────
            var recordsFiltered = await query.CountAsync();

            var data = await query
                .OrderBy(r => r.Id)
                .Skip(start)
                .Take(length)
                .Select(r => new
                {
                    r.Id,
                    r.OperatorId,
                    r.OperatorName,
                    guideId = r.OutsideGuideId,
                    guideName = r.OutsideGuideName
                })
                .ToListAsync();

            return Json(new { draw, recordsFiltered, recordsTotal, data });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                draw = Request.Form["draw"].FirstOrDefault(),
                recordsFiltered = 0,
                recordsTotal = 0,
                data = new List<object>(),
                error = ex.Message
            });
        }
    }

    // =========================================================
    // GET DROPDOWNS — operators + outside guides for the form
    // =========================================================
    [HttpGet("GetDropdowns")]
    public async Task<IActionResult> GetDropdowns()
    {
        try
        {
            List<object> operators;

            if (IsOperator && !IsSuperAdmin)
            {
                var op = await GetCurrentOperatorAsync();
                operators = op == null
                    ? new List<object>()
                    : new List<object>
                    {
                        new
                        {
                            id           = op.Id.ToString(),
                            businessName = op.BusinessName ?? op.Name ?? ""
                        }
                    };
            }
            else
            {
                operators = (await _context.Operators
                    .OrderBy(o => o.BusinessName ?? o.Name)
                    .Select(o => new
                    {
                        id = o.Id.ToString(),
                        businessName = o.BusinessName ?? o.Name ?? ""
                    })
                    .ToListAsync())
                    .Cast<object>()
                    .ToList();
            }

            var guides = await _context.OutsideGuides
                .OrderBy(g => g.FName)
                .Select(g => new
                {
                    rfid = g.Rfid ?? "",
                    fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? ""))
                               .Replace("  ", " ").Trim()
                })
                .ToListAsync();

            return Json(new { operators, guides });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    // =========================================================
    // CREATE GET
    // =========================================================
    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["Action"] = "Create";
        return PartialView("_OutsideGuideFromOperatorForm", new OutsideGuideFromOperator());
    }

    // =========================================================
    // CREATE POST
    // =========================================================
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OutsideGuideFromOperator model)
    {
        model.OutsideGuideId = Request.Form["guideId"].FirstOrDefault() ?? model.OutsideGuideId;
        model.OutsideGuideName = Request.Form["guideName"].FirstOrDefault() ?? model.OutsideGuideName;
        model.OperatorId = Request.Form["operatorId"].FirstOrDefault() ?? model.OperatorId;
        model.OperatorName = Request.Form["operatorName"].FirstOrDefault() ?? model.OperatorName;

        if (IsOperator && !IsSuperAdmin)
        {
            var op = await GetCurrentOperatorAsync();
            if (op == null)
                return Json(new { success = false, message = "Operator account not found." });
            model.OperatorId = op.Id.ToString();
            model.OperatorName = op.BusinessName ?? op.Name ?? "";
        }

        if (string.IsNullOrWhiteSpace(model.OperatorId) || string.IsNullOrWhiteSpace(model.OutsideGuideId))
            return Json(new { success = false, message = "Please select both an Operator and a Guide." });

        try
        {
            bool exists = await _context.OutsideGuideFromOperators.AnyAsync(r =>
                r.OperatorId == model.OperatorId &&
                r.OutsideGuideId == model.OutsideGuideId);

            if (exists)
                return Json(new { success = false, message = "This guide is already assigned to that operator." });

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Record created successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // EDIT GET
    // =========================================================
    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await _context.OutsideGuideFromOperators.FindAsync(id);
        if (record == null) return NotFound();

        if (IsOperator && !IsSuperAdmin)
        {
            var op = await GetCurrentOperatorAsync();
            if (op == null || record.OperatorId != op.Id.ToString())
                return Forbid();
        }

        ViewData["Action"] = "Edit";
        return PartialView("_OutsideGuideFromOperatorForm", record);
    }

    // =========================================================
    // EDIT POST
    // =========================================================
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OutsideGuideFromOperator model)
    {
        model.OutsideGuideId = Request.Form["guideId"].FirstOrDefault() ?? model.OutsideGuideId;
        model.OutsideGuideName = Request.Form["guideName"].FirstOrDefault() ?? model.OutsideGuideName;
        model.OperatorId = Request.Form["operatorId"].FirstOrDefault() ?? model.OperatorId;
        model.OperatorName = Request.Form["operatorName"].FirstOrDefault() ?? model.OperatorName;

        if (string.IsNullOrWhiteSpace(model.OperatorId) || string.IsNullOrWhiteSpace(model.OutsideGuideId))
            return Json(new { success = false, message = "Please select both an Operator and a Guide." });

        if (IsOperator && !IsSuperAdmin)
        {
            var op = await GetCurrentOperatorAsync();
            if (op == null)
                return Json(new { success = false, message = "Operator account not found." });

            var existing = await _context.OutsideGuideFromOperators
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (existing == null || existing.OperatorId != op.Id.ToString())
                return Json(new { success = false, message = "Access denied." });

            model.OperatorId = op.Id.ToString();
            model.OperatorName = op.BusinessName ?? op.Name ?? "";
        }

        try
        {
            bool duplicate = await _context.OutsideGuideFromOperators.AnyAsync(r =>
                r.Id != model.Id &&
                r.OperatorId == model.OperatorId &&
                r.OutsideGuideId == model.OutsideGuideId);

            if (duplicate)
                return Json(new { success = false, message = "This guide is already assigned to that operator." });

            _context.Update(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Record updated successfully." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Json(new { success = false, message = "Concurrency error. Please try again." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // DELETE
    // =========================================================
    [HttpPost("DeleteAjax")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        try
        {
            var record = await _context.OutsideGuideFromOperators.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Record not found." });

            if (IsOperator && !IsSuperAdmin)
            {
                var op = await GetCurrentOperatorAsync();
                if (op == null || record.OperatorId != op.Id.ToString())
                    return Json(new { success = false, message = "Access denied." });
            }

            _context.OutsideGuideFromOperators.Remove(record);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Record deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}