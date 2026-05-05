using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.WebUI.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("OutsideGuides")]
[Authorize(Roles = "Super Admin")]
public class OutsideGuidesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public OutsideGuidesController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
    }

    // =========================================================
    // INDEX
    // =========================================================
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => View();

    // =========================================================
    // OPERATOR FILTER DROPDOWN — uses operator_list
    // =========================================================
    [HttpGet("GetOperatorsForFilter")]
    public async Task<IActionResult> GetOperatorsForFilter()
    {
        var operators = await _context.OperatorLists
            .OrderBy(o => o.BusinessName)
            .Select(o => new
            {
                id = o.OperatorId,
                displayName = string.IsNullOrWhiteSpace(o.BusinessName) ? o.OwnerName : o.BusinessName
            })
            .ToListAsync();

        return Json(operators);
    }

    // =========================================================
    // DATATABLE
    // =========================================================
    [HttpPost("GetOutsideGuidesData")]
    public async Task<IActionResult> GetOutsideGuidesData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault();
            var operatorFilter = Request.Form["operatorFilter"].FirstOrDefault();

            // ✅ Include OperatorList (operator_list table)
            var query = _context.OutsideGuides
                .Include(g => g.OperatorList)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(operatorFilter) &&
                int.TryParse(operatorFilter, out int filterOpId))
            {
                query = query.Where(g => g.OperatorId == filterOpId);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(g =>
                    (g.FName != null && g.FName.Contains(searchValue)) ||
                    (g.LName != null && g.LName.Contains(searchValue)) ||
                    (g.Rfid != null && g.Rfid.Contains(searchValue)) ||
                    (g.Nickname != null && g.Nickname.Contains(searchValue)) ||
                    (g.OperatorList != null &&
                        (g.OperatorList.BusinessName.Contains(searchValue) ||
                         g.OperatorList.OwnerName.Contains(searchValue))));
            }

            var recordsTotal = await query.CountAsync();

            var rawData = await query
                .OrderBy(g => g.OperatorId)
                .ThenBy(g => g.TPosition)
                .Skip(start)
                .Take(length)
                .Select(g => new
                {
                    outsideGuideId = g.OutsideGuideId,
                    fName = g.FName ?? "",
                    mName = g.MName ?? "",
                    lName = g.LName ?? "",
                    rfid = g.Rfid ?? "",
                    nickname = g.Nickname ?? "----",
                    cNumber = g.CNumber ?? "----",
                    image = g.Image ?? "",
                    operatorId = g.OperatorId,
                    // ✅ Read from OperatorList
                    operatorName = g.OperatorList != null
                        ? (!string.IsNullOrWhiteSpace(g.OperatorList.BusinessName)
                            ? g.OperatorList.BusinessName
                            : g.OperatorList.OwnerName ?? "—")
                        : "—"
                })
                .ToListAsync();

            var data = rawData.Select(g => new
            {
                g.outsideGuideId,
                fullName = $"{g.fName} {g.mName} {g.lName}".Replace("  ", " ").Trim(),
                g.rfid,
                g.nickname,
                g.cNumber,
                g.operatorName,
                g.operatorId,
                image = NormalizeImagePath(g.image)
            }).ToList();

            return Json(new { draw, recordsFiltered = recordsTotal, recordsTotal, data });
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
    // CREATE GET
    // =========================================================
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var allRfids = await _context.OutsideGuides.Select(g => g.Rfid).ToListAsync();
        int nextRfid = allRfids
            .Where(r => !string.IsNullOrEmpty(r) && int.TryParse(r, out _))
            .Select(r => int.Parse(r))
            .Where(r => r >= 200000)
            .DefaultIfEmpty(199999)
            .Max() + 1;

        int nextTPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        ViewData["Action"] = "Create";

        // ✅ Dropdown from operator_list
        ViewData["Operators"] = await _context.OperatorLists
            .OrderBy(o => o.BusinessName)
            .Select(o => new
            {
                Id = o.OperatorId,
                DisplayName = string.IsNullOrWhiteSpace(o.BusinessName) ? o.OwnerName : o.BusinessName
            })
            .ToListAsync();

        return PartialView("_OutsideGuideForm", new OutsideGuide
        {
            Rfid = nextRfid.ToString(),
            TPosition = nextTPosition
        });
    }

    // =========================================================
    // CREATE POST
    // =========================================================
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OutsideGuide model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
        ModelState.Remove("Nickname");
        ModelState.Remove("OperatorList");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed: " + string.Join(", ", errors) });
        }

        try
        {
            if (await _context.OutsideGuides.AnyAsync(g => g.Rfid == model.Rfid))
            {
                var max = (await _context.OutsideGuides.Select(g => g.Rfid).ToListAsync())
                    .Where(r => int.TryParse(r, out _))
                    .Select(r => int.Parse(r))
                    .DefaultIfEmpty(199999)
                    .Max();
                model.Rfid = (max + 1).ToString();
            }

            model.TPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            while (await _context.OutsideGuides.AnyAsync(g => g.TPosition == model.TPosition))
                model.TPosition++;

            model.MName = string.IsNullOrWhiteSpace(model.MName) ? "" : model.MName;
            model.Nickname = string.IsNullOrWhiteSpace(model.Nickname) ? "" : model.Nickname;
            model.Image = await SavePhotoToWwwRoot(PhotoFile, "outsideguides");

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Outside guide created successfully." });
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
        try
        {
            var guide = await _context.OutsideGuides.FindAsync(id);
            if (guide == null) return NotFound();

            ViewData["Action"] = "Edit";

            // ✅ Dropdown from operator_list
            ViewData["Operators"] = await _context.OperatorLists
                .OrderBy(o => o.BusinessName)
                .Select(o => new
                {
                    Id = o.OperatorId,
                    DisplayName = string.IsNullOrWhiteSpace(o.BusinessName) ? o.OwnerName : o.BusinessName
                })
                .ToListAsync();

            return PartialView("_OutsideGuideForm", guide);
        }
        catch (Exception ex)
        {
            return Content($"Error: {ex.Message}");
        }
    }

    // =========================================================
    // EDIT POST
    // =========================================================
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(OutsideGuide model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
        ModelState.Remove("Nickname");
        ModelState.Remove("OperatorList");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed: " + string.Join(", ", errors) });
        }

        try
        {
            model.MName = string.IsNullOrWhiteSpace(model.MName) ? "" : model.MName;
            model.Nickname = string.IsNullOrWhiteSpace(model.Nickname) ? "" : model.Nickname;

            var newPath = await SavePhotoToWwwRoot(PhotoFile, "outsideguides");
            if (newPath != null) model.Image = newPath;

            _context.Update(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Outside guide updated successfully." });
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
            var guide = await _context.OutsideGuides.FindAsync(id);
            if (guide == null)
                return Json(new { success = false, message = "Outside guide not found." });

            _context.OutsideGuides.Remove(guide);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Outside guide deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // ARRANGE GET
    // =========================================================
    [HttpGet("Arrange")]
    public async Task<IActionResult> Arrange()
    {
        var guides = await _context.OutsideGuides
            .OrderBy(g => g.TPosition)
            .Select(g => new OutsideGuideQueueItem
            {
                OutsideGuideId = g.OutsideGuideId,
                Rfid = g.Rfid,
                FullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim(),
                Image = g.Image,
                TPosition = g.TPosition
            })
            .ToListAsync();

        guides.ForEach(g => g.Image = NormalizeImagePath(g.Image));
        return View(guides);
    }

    // =========================================================
    // SAVE QUEUE ORDER
    // =========================================================
    [HttpPost("Arrange")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueueOrder([FromBody] List<int> guideIds)
    {
        try
        {
            if (guideIds == null || !guideIds.Any())
                return Json(new { success = false, message = "No guide IDs provided." });

            var guides = await _context.OutsideGuides
                .Where(g => guideIds.Contains(g.OutsideGuideId))
                .ToListAsync();

            int baseTs = guides.Min(g => g.TPosition);

            for (int i = 0; i < guideIds.Count; i++)
            {
                var guide = guides.FirstOrDefault(g => g.OutsideGuideId == guideIds[i]);
                if (guide != null)
                    guide.TPosition = baseTs + (i * 10);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Queue order saved for {guideIds.Count} guide(s)." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // AUTO QUEUE
    // =========================================================
    [HttpPost("AutoQueue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoQueue()
    {
        try
        {
            const int UnixThreshold = 1_000_000_000;

            var allGuides = await _context.OutsideGuides
                .OrderBy(g => g.TPosition)
                .ToListAsync();

            int lastTs = allGuides
                .Where(g => g.TPosition >= UnixThreshold)
                .Select(g => g.TPosition)
                .DefaultIfEmpty((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10)
                .Max();

            int fixedCount = 0;
            foreach (var guide in allGuides)
            {
                if (guide.TPosition < UnixThreshold)
                {
                    lastTs++;
                    while (allGuides.Any(g => g.OutsideGuideId != guide.OutsideGuideId && g.TPosition == lastTs))
                        lastTs++;
                    guide.TPosition = lastTs;
                    fixedCount++;
                }
            }

            if (fixedCount == 0)
                return Json(new { success = true, message = "All guides already have a valid queue timestamp.", count = 0 });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"{fixedCount} guide(s) assigned a queue timestamp.", count = fixedCount });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private static string? NormalizeImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.Contains(":\\") ||
            (path.Contains(":/") && !path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ||
            path.StartsWith("\\\\"))
            return null;
        return path;
    }

    private async Task<string?> SavePhotoToWwwRoot(IFormFile? photo, string subfolder)
    {
        if (photo == null || photo.Length == 0) return null;
        try
        {
            var wwwroot = _hostEnvironment.WebRootPath;
            if (string.IsNullOrEmpty(wwwroot))
            {
                wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "WebUI", "wwwroot");
                if (!Directory.Exists(wwwroot))
                    wwwroot = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
            }

            var folder = Path.Combine(wwwroot, "uploads", subfolder);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await photo.CopyToAsync(stream);

            return $"/uploads/{subfolder}/{fileName}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving photo: {ex.Message}", ex);
        }
    }
}