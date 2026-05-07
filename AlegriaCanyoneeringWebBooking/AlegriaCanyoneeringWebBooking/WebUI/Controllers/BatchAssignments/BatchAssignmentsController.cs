using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[Route("BatchAssignments")]
[Authorize(Roles = "Super Admin")]
public class BatchAssignmentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BatchAssignmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => View();

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

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BatchAssignment model)
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
            bool codeExists = await _context.BatchAssignments.AnyAsync(b => b.BatchCode == model.BatchCode);
            if (codeExists)
                model.BatchCode = await GenerateBatchCode();

            model.OperatorId = await ResolveOperatorIdFromBatch(model.BatchCode);

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Batch assignment created successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var batch = await _context.BatchAssignments.FindAsync(id);
        if (batch == null) return NotFound();

        ViewData["Action"] = "Edit";
        await PopulateDropdowns(batch);

        return PartialView("_BatchAssignmentForm", batch);
    }

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
            model.OperatorId = await ResolveOperatorIdFromBatch(model.BatchCode);

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

    [HttpPost("GetBatchAssignmentsData")]
    public async Task<IActionResult> GetBatchAssignmentsData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim();

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
                    (b.Operator != null && (b.Operator.BusinessName ?? "").Contains(searchValue)) ||
                    (b.Guide != null && (((b.Guide.FName ?? "") + " " + (b.Guide.LName ?? "")).Contains(searchValue))) ||
                    (b.OutsideGuide != null &&
                        ((((b.OutsideGuide.FName ?? "") + " " + (b.OutsideGuide.LName ?? "")).Contains(searchValue)) ||
                         ((b.OutsideGuide.Nickname ?? "").Contains(searchValue)))) ||
                    (b.Driver != null && (((b.Driver.FName ?? "") + " " + (b.Driver.LName ?? "")).Contains(searchValue)))
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
                    StoredOperatorName = b.Operator != null ? b.Operator.BusinessName : null,

                    GuideName = b.Guide != null
                        ? (((b.Guide.FName ?? "") + " " + (b.Guide.LName ?? "")).Trim())
                        : null,

                    OutsideGuideId = b.OutsideGuideId,
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

            var normalizedBatchCodes = raw
                .Select(x => NormalizeBatchCode(x.BatchCode))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var guestRows = await (
                from g in _context.Guests.AsNoTracking()
                join o in _context.Operators.AsNoTracking()
                    on g.OperatorId equals o.Id into og
                from o in og.DefaultIfEmpty()
                where g.Batch != null
                select new
                {
                    BatchCode = g.Batch,
                    GuestId = g.Id,
                    GuestDate = g.Date,
                    OperatorName = o != null ? o.BusinessName : null
                })
                .ToListAsync();

            var guestOperatorMapToday = guestRows
                .Select(x => new
                {
                    BatchCode = NormalizeBatchCode(x.BatchCode),
                    x.GuestId,
                    ParsedDate = ParseGuestDate(x.GuestDate),
                    x.OperatorName
                })
                .Where(x =>
                    normalizedBatchCodes.Contains(x.BatchCode, StringComparer.OrdinalIgnoreCase) &&
                    x.ParsedDate.HasValue &&
                    x.ParsedDate.Value.Date == DateTime.Today &&
                    !string.IsNullOrWhiteSpace(x.OperatorName))
                .GroupBy(x => x.BatchCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.GuestId).Select(x => x.OperatorName!).First(),
                    StringComparer.OrdinalIgnoreCase);

            var guestOperatorMapAny = guestRows
                .Select(x => new
                {
                    BatchCode = NormalizeBatchCode(x.BatchCode),
                    x.GuestId,
                    x.OperatorName
                })
                .Where(x =>
                    normalizedBatchCodes.Contains(x.BatchCode, StringComparer.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(x.OperatorName))
                .GroupBy(x => x.BatchCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.GuestId).Select(x => x.OperatorName!).First(),
                    StringComparer.OrdinalIgnoreCase);

            var data = raw.Select(b =>
            {
                var batchCode = NormalizeBatchCode(b.BatchCode);

                var outsideFullName = string.IsNullOrWhiteSpace(b.OutsideMName)
                    ? $"{b.OutsideFName} {b.OutsideLName}".Trim()
                    : $"{b.OutsideFName} {b.OutsideMName} {b.OutsideLName}".Trim();

                var outsideDisplayName = !string.IsNullOrWhiteSpace(b.OutsideNickname)
                    ? $"{outsideFullName} ({b.OutsideNickname})"
                    : outsideFullName;

                string operatorName;
                if (guestOperatorMapToday.TryGetValue(batchCode, out var todayOperator) &&
                    !string.IsNullOrWhiteSpace(todayOperator))
                {
                    operatorName = todayOperator;
                }
                else if (guestOperatorMapAny.TryGetValue(batchCode, out var anyOperator) &&
                         !string.IsNullOrWhiteSpace(anyOperator))
                {
                    operatorName = anyOperator;
                }
                else
                {
                    operatorName = b.StoredOperatorName ?? "—";
                }

                return new
                {
                    id = b.Id,
                    batchCode,
                    operatorName,
                    assignedGuide = b.HasOutside
                        ? (string.IsNullOrWhiteSpace(outsideDisplayName) ? "—" : outsideDisplayName)
                        : b.HasGuide
                            ? (b.GuideName ?? "—")
                            : "No Guide Assigned",
                    guideType = b.HasOutside ? "Outside" : b.HasGuide ? "Internal" : "—",
                    driverName = b.DriverName ?? "—",
                    outsideGuideId = b.OutsideGuideId
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

    private async Task<int?> ResolveOperatorIdFromBatch(string? batchCode)
    {
        var normalizedBatchCode = NormalizeBatchCode(batchCode);

        var guests = await _context.Guests
            .AsNoTracking()
            .Where(g => g.Batch != null)
            .Select(g => new
            {
                g.Batch,
                g.OperatorId,
                g.Id,
                g.Date
            })
            .ToListAsync();

        var todayMatch = guests
            .Select(g => new
            {
                BatchCode = NormalizeBatchCode(g.Batch),
                g.OperatorId,
                g.Id,
                ParsedDate = ParseGuestDate(g.Date)
            })
            .Where(g =>
                g.BatchCode == normalizedBatchCode &&
                g.ParsedDate.HasValue &&
                g.ParsedDate.Value.Date == DateTime.Today)
            .OrderBy(g => g.Id)
            .Select(g => g.OperatorId)
            .FirstOrDefault();

        if (todayMatch != null)
            return todayMatch;

        return guests
            .Select(g => new
            {
                BatchCode = NormalizeBatchCode(g.Batch),
                g.OperatorId,
                g.Id
            })
            .Where(g => g.BatchCode == normalizedBatchCode)
            .OrderBy(g => g.Id)
            .Select(g => g.OperatorId)
            .FirstOrDefault();
    }

    private static string NormalizeBatchCode(string? batchCode)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return string.Empty;

        var value = batchCode.Trim();

        if (value.StartsWith("BATCH-", StringComparison.OrdinalIgnoreCase))
            value = value[6..];

        return value.Trim();
    }

    private static DateTime? ParseGuestDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (long.TryParse(trimmed, out long unix))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix)
                    .ToOffset(TimeSpan.FromHours(8))
                    .DateTime;
            }
            catch
            {
            }
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt))
            return dt;

        if (DateTime.TryParse(trimmed, out dt))
            return dt;

        return null;
    }

    private async Task PopulateDropdowns(BatchAssignment? current = null)
    {
        ViewBag.Operators = new SelectList(
            await _context.Operators
                .OrderBy(o => o.BusinessName)
                .ToListAsync(),
            "Id",
            "BusinessName",
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