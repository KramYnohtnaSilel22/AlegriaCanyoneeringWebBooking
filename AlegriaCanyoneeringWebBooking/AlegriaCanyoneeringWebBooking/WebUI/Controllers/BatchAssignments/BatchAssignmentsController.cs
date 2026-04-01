using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Route("BatchAssignments")]
[Authorize(Roles = "Super Admin")]
public class BatchAssignmentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BatchAssignmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // INDEX
    // =========================================================
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => View();

    // =========================================================
    // CREATE GET — auto BatchCode
    // =========================================================
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var nextCode = await GenerateBatchCode();
        ViewData["Action"] = "Create";
        await PopulateDropdowns();

        return PartialView("_BatchAssignmentForm", new BatchAssignment
        {
            BatchCode = nextCode
        });
    }

    // =========================================================
    // CREATE POST
    // =========================================================
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BatchAssignment model)
    {
        ModelState.Remove("Operator");
        ModelState.Remove("Guide");
        ModelState.Remove("OutsideGuide");
        ModelState.Remove("Driver");

        // Guide and OutsideGuide are mutually exclusive — at least one required
        if (model.GuideId == null && model.OutsideGuideId == null)
            ModelState.AddModelError("", "Please assign either an Internal Guide or an Outside Guide.");

        if (model.GuideId != null && model.OutsideGuideId != null)
        {
            model.OutsideGuideId = null; // internal takes priority on conflict
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed.", errors });
        }

        try
        {
            // Guard against BatchCode collision (race condition)
            bool codeExists = await _context.BatchAssignments.AnyAsync(b => b.BatchCode == model.BatchCode);
            if (codeExists)
                model.BatchCode = await GenerateBatchCode();

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Batch assignment created successfully." });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return Json(new { success = false, message = msg });
        }
    }

    // =========================================================
    // EDIT GET
    // =========================================================
    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var batch = await _context.BatchAssignments.FindAsync(id);
        if (batch == null) return NotFound();

        ViewData["Action"] = "Edit";
        await PopulateDropdowns(batch);

        return PartialView("_BatchAssignmentForm", batch);
    }

    // =========================================================
    // EDIT POST
    // =========================================================
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BatchAssignment model)
    {
        ModelState.Remove("Operator");
        ModelState.Remove("Guide");
        ModelState.Remove("OutsideGuide");
        ModelState.Remove("Driver");

        if (model.GuideId == null && model.OutsideGuideId == null)
            ModelState.AddModelError("", "Please assign either an Internal Guide or an Outside Guide.");

        if (model.GuideId != null && model.OutsideGuideId != null)
            model.OutsideGuideId = null;

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed.", errors });
        }

        try
        {
            _context.Update(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Batch assignment updated successfully." });
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
    // DATATABLE — server-side
    // =========================================================
    [HttpPost("GetBatchAssignmentsData")]
    public async Task<IActionResult> GetBatchAssignmentsData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.BatchAssignments
                .AsNoTracking()
                .Include(b => b.Operator)
                .Include(b => b.Guide)
                .Include(b => b.OutsideGuide)
                .Include(b => b.Driver)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                query = query.Where(b =>
                    (b.BatchCode ?? "").Contains(searchValue) ||
                    (b.Operator != null && (b.Operator.Name ?? "").Contains(searchValue)) ||
                    (b.Guide != null &&
                        (((b.Guide.FName ?? "") + " " + (b.Guide.LName ?? "")).Contains(searchValue))) ||
                    (b.OutsideGuide != null &&
                        (
                            (((b.OutsideGuide.FName ?? "") + " " + (b.OutsideGuide.LName ?? "")).Contains(searchValue)) ||
                            ((b.OutsideGuide.Nickname ?? "").Contains(searchValue))
                        )) ||
                    (b.Driver != null &&
                        (((b.Driver.FName ?? "") + " " + (b.Driver.LName ?? "")).Contains(searchValue)))
                );
            }

            var total = await query.CountAsync();

            var raw = await query
                .OrderByDescending(b => b.Id)
                .Skip(start)
                .Take(length)
                .Select(b => new
                {
                    b.Id,
                    b.BatchCode,
                    OperatorName = b.Operator != null ? b.Operator.Name : null,

                    GuideName = b.Guide != null
                        ? (((b.Guide.FName ?? "") + " " + (b.Guide.LName ?? "")).Trim())
                        : null,

                    OutsideFName = b.OutsideGuide != null ? b.OutsideGuide.FName : null,
                    OutsideMName = b.OutsideGuide != null ? b.OutsideGuide.MName : null,
                    OutsideLName = b.OutsideGuide != null ? b.OutsideGuide.LName : null,
                    OutsideNickname = b.OutsideGuide != null ? b.OutsideGuide.Nickname : null,

                    DriverName = b.Driver != null
                        ? (((b.Driver.FName ?? "") + " " + (b.Driver.LName ?? "")).Trim())
                        : null,

                    HasGuide = b.GuideId != null,
                    HasOutside = b.OutsideGuideId != null
                })
                .ToListAsync();

            var data = raw.Select(b =>
            {
                var outsideFullName = string.IsNullOrWhiteSpace(b.OutsideMName)
                    ? $"{b.OutsideFName} {b.OutsideLName}".Trim()
                    : $"{b.OutsideFName} {b.OutsideMName} {b.OutsideLName}".Trim();

                var outsideDisplayName = !string.IsNullOrWhiteSpace(b.OutsideNickname)
                    ? $"{outsideFullName} ({b.OutsideNickname})"
                    : outsideFullName;

                return new
                {
                    id = b.Id,
                    batchCode = b.BatchCode,
                    operatorName = b.OperatorName ?? "—",
                    assignedGuide = b.HasOutside
                        ? (string.IsNullOrWhiteSpace(outsideDisplayName) ? "—" : outsideDisplayName)
                        : b.HasGuide
                            ? (b.GuideName ?? "—")
                            : "No Guide Assigned",
                    guideType = b.HasOutside ? "Outside" : b.HasGuide ? "Internal" : "—",
                    driverName = b.DriverName ?? "—"
                };
            }).ToList();

            return Json(new
            {
                draw,
                recordsFiltered = total,
                recordsTotal = total,
                data
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                draw = Request.Form["draw"].FirstOrDefault(),
                recordsFiltered = 0,
                recordsTotal = 0,
                data = new List<object>(),
                error = ex.InnerException?.Message ?? ex.Message
            });
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
            var batch = await _context.BatchAssignments.FindAsync(id);
            if (batch == null)
                return Json(new { success = false, message = "Batch assignment not found." });

            _context.BatchAssignments.Remove(batch);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Batch assignment deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private async Task<string> GenerateBatchCode()
    {
        var existing = await _context.BatchAssignments
            .Select(b => b.BatchCode)
            .ToListAsync();

        int next = existing
            .Where(c => c != null && c.StartsWith("BA-") && int.TryParse(c[3..], out _))
            .Select(c => int.Parse(c[3..]))
            .DefaultIfEmpty(999)
            .Max() + 1;

        return $"BA-{next:D4}";
    }

    private async Task PopulateDropdowns(BatchAssignment? current = null)
    {
        ViewBag.Operators = new SelectList(
            await _context.Operators
                .OrderBy(o => o.Name)
                .ToListAsync(),
            "Id",
            "Name",
            current?.OperatorId);

        ViewBag.Guides = new SelectList(
            await _context.Guides
                .OrderBy(g => g.FName)
                .Select(g => new
                {
                    g.GuideId,
                    DisplayName = string.IsNullOrWhiteSpace(g.MName)
                        ? (g.FName + " " + g.LName)
                        : (g.FName + " " + g.MName + " " + g.LName)
                })
                .ToListAsync(),
            "GuideId",
            "DisplayName",
            current?.GuideId);

        ViewBag.OutsideGuides = new SelectList(
            await _context.OutsideGuides
                .OrderBy(g => g.FName)
                .Select(g => new
                {
                    g.OutsideGuideId,
                    DisplayName = string.IsNullOrWhiteSpace(g.Nickname)
                        ? (string.IsNullOrWhiteSpace(g.MName)
                            ? (g.FName + " " + g.LName)
                            : (g.FName + " " + g.MName + " " + g.LName))
                        : (string.IsNullOrWhiteSpace(g.MName)
                            ? (g.FName + " " + g.LName + " (" + g.Nickname + ")")
                            : (g.FName + " " + g.MName + " " + g.LName + " (" + g.Nickname + ")"))
                })
                .ToListAsync(),
            "OutsideGuideId",
            "DisplayName",
            current?.OutsideGuideId);

        ViewBag.Drivers = new SelectList(
            await _context.Drivers
                .OrderBy(d => d.FName)
                .Select(d => new
                {
                    d.DriverId,
                    DisplayName = string.IsNullOrWhiteSpace(d.MName)
                        ? (d.FName + " " + d.LName)
                        : (d.FName + " " + d.MName + " " + d.LName)
                })
                .ToListAsync(),
            "DriverId",
            "DisplayName",
            current?.DriverId);
    }
}