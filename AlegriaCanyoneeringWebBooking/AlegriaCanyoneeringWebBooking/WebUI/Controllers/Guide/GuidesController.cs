using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.WebUI.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("Guides")]
[Authorize(Roles = "Super Admin,Admin,Operator")]
public class GuidesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public GuidesController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        return View();
    }

    // =========================================================
    // CREATE GET — with auto Rfid, TPosition = Unix timestamp
    // =========================================================
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        // ✅ Auto-increment Rfid starting at 100000
        var allRefIds = await _context.Guides.Select(t => t.Rfid).ToListAsync();
        int nextRefId = 100000;
        var numericRefIds = allRefIds
            .Where(r => !string.IsNullOrEmpty(r) && int.TryParse(r, out _))
            .Select(r => int.Parse(r))
            .Where(r => r >= 100000)
            .ToList();
        if (numericRefIds.Any())
            nextRefId = numericRefIds.Max() + 1;

        // ✅ TPosition = current Unix timestamp (seconds since epoch)
        int nextTPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        ViewData["Action"] = "Create";
        return PartialView("_GuideForm", new Guide
        {
            Rfid = nextRefId.ToString(),
            TPosition = nextTPosition
        });
    }

    // =========================================================
    // CREATE POST
    // =========================================================
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guide model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
        ModelState.Remove("Nickname");
        ModelState.Remove("Guests");

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
            // ✅ Check duplicate Rfid — auto-fix if collision
            bool rfIdExists = await _context.Guides.AnyAsync(t => t.Rfid == model.Rfid);
            if (rfIdExists)
            {
                var maxRefId = await _context.Guides
                    .Where(t => t.Rfid != null)
                    .Select(t => t.Rfid)
                    .ToListAsync();
                int next = maxRefId
                    .Where(r => int.TryParse(r, out _))
                    .Select(r => int.Parse(r))
                    .DefaultIfEmpty(100000)
                    .Max() + 1;
                model.Rfid = next.ToString();
            }

            // ✅ TPosition = Unix timestamp — assigned fresh at save time,
            //    not trusting the form value. Shift +1 if somehow duplicate.
            model.TPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            while (await _context.Guides.AnyAsync(t => t.TPosition == model.TPosition))
                model.TPosition++;

            // ✅ Prevent null DB errors
            model.MName = string.IsNullOrWhiteSpace(model.MName) ? "" : model.MName;
            model.Nickname = string.IsNullOrWhiteSpace(model.Nickname) ? "" : model.Nickname;
            model.Image = await SavePhotoToWwwRoot(PhotoFile, "tourguides");

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tour guide created successfully." });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"Create error: {innerMsg}");
            return Json(new { success = false, message = innerMsg });
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
            var tourGuide = await _context.Guides.FindAsync(id);
            if (tourGuide == null) return NotFound();

            ViewData["Action"] = "Edit";
            return PartialView("_GuideForm", tourGuide);
        }
        catch (Exception ex)
        {
            return Content($"Error: {ex.Message} | Inner: {ex.InnerException?.Message}");
        }
    }

    // =========================================================
    // EDIT POST
    // ✅ TPosition is preserved from existing record — not changed on edit
    // =========================================================
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guide model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
        ModelState.Remove("Nickname");
        ModelState.Remove("Guests");

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

            var newPath = await SavePhotoToWwwRoot(PhotoFile, "tourguides");
            if (newPath != null)
                model.Image = newPath;

            _context.Update(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tour guide updated successfully." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Json(new { success = false, message = "Concurrency error occurred." });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            return Json(new { success = false, message = innerMsg });
        }
    }

    // =========================================================
    // DATATABLE
    // =========================================================
    [HttpPost("GetTourGuidesData")]
    public async Task<IActionResult> GetTourGuidesData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.Guides.AsQueryable();

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(t =>
                    (t.FName != null && t.FName.Contains(searchValue)) ||
                    (t.LName != null && t.LName.Contains(searchValue)) ||
                    (t.Rfid != null && t.Rfid.Contains(searchValue)) ||
                    (t.Nickname != null && t.Nickname.Contains(searchValue)));
            }

            var recordsTotal = await query.CountAsync();

            // ✅ Step 1: Fetch raw fields from DB
            var rawData = await query
                .OrderBy(t => t.GuideId)
                .Skip(start)
                .Take(length)
                .Select(t => new
                {
                    guideId = t.GuideId,
                    fName = t.FName ?? "",
                    mName = t.MName ?? "",
                    lName = t.LName ?? "",
                    rfid = t.Rfid ?? "",
                    nickname = t.Nickname ?? "----",
                    cNumber = t.CNumber ?? "----",
                    image = t.Image ?? ""
                })
                .ToListAsync();

            // ✅ Step 2: Format in memory — avoids EF Core Trim() issues
            var data = rawData.Select(t => new
            {
                guideId = t.guideId,
                fullName = $"{t.fName} {t.mName} {t.lName}".Replace("  ", " ").Trim(),
                rfid = t.rfid,
                nickname = t.nickname,
                cNumber = t.cNumber,
                image = t.image
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
    // DELETE
    // =========================================================
    [HttpPost("DeleteAjax")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        try
        {
            var tourGuide = await _context.Guides.FindAsync(id);
            if (tourGuide == null)
                return Json(new { success = false, message = "Tour guide not found." });

            _context.Guides.Remove(tourGuide);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tour guide deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // ARRANGE QUEUE GET — show guides ordered by TPosition
    // =========================================================
    [HttpGet("Arrange")]
    public async Task<IActionResult> Arrange()
    {
        var guides = await _context.Guides
            .OrderBy(g => g.TPosition)
            .Select(g => new
            {
                g.GuideId,
                g.Rfid,
                fullName = (g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? ""),
                g.Image,
                g.TPosition
            })
            .ToListAsync();

        return View(guides.Select(g => new GuideQueueItem
        {
            GuideId = g.GuideId,
            Rfid = g.Rfid,
            FullName = g.fullName.Trim(),
            Image = g.Image,
            TPosition = g.TPosition
        }).ToList());
    }

    // =========================================================
    // SAVE QUEUE ORDER — POST reordered guide IDs
    // =========================================================
    [HttpPost("Arrange")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueueOrder([FromBody] List<int> guideIds)
    {
        try
        {
            if (guideIds == null || !guideIds.Any())
                return Json(new { success = false, message = "No guide IDs provided." });

            var guides = await _context.Guides
                .Where(g => guideIds.Contains(g.GuideId))
                .ToListAsync();

            // Use smallest existing TPosition as base — preserves queue origin
            int baseTimestamp = guides.Min(g => g.TPosition);

            for (int i = 0; i < guideIds.Count; i++)
            {
                var guide = guides.FirstOrDefault(g => g.GuideId == guideIds[i]);
                if (guide != null)
                    guide.TPosition = baseTimestamp + (i * 10);
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
    // AUTO QUEUE — assign Unix timestamp to guides missing one
    // ✅ TPosition < 1_000_000_000 = old auto-increment / zero → fix
    // ✅ TPosition ≥ 1_000_000_000 = valid Unix timestamp → leave
    // =========================================================
    [HttpPost("AutoQueue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoQueue()
    {
        try
        {
            const int UnixThreshold = 1_000_000_000;

            var allGuides = await _context.Guides
                .OrderBy(g => g.TPosition)
                .ToListAsync();

            int lastTimestamp = allGuides
                .Where(g => g.TPosition >= UnixThreshold)
                .Select(g => g.TPosition)
                .DefaultIfEmpty((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10)
                .Max();

            int fixedCount = 0;

            foreach (var guide in allGuides)
            {
                if (guide.TPosition < UnixThreshold)
                {
                    lastTimestamp += 10;
                    while (allGuides.Any(g => g.GuideId != guide.GuideId && g.TPosition == lastTimestamp))
                        lastTimestamp++;
                    guide.TPosition = lastTimestamp;
                    fixedCount++;
                }
            }

            if (fixedCount == 0)
                return Json(new { success = true, message = "All guides already have a valid queue timestamp. Nothing changed.", count = 0 });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"{fixedCount} guide(s) assigned a queue timestamp.", count = fixedCount });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // HELPER — Save photo
    // =========================================================
    private async Task<string?> SavePhotoToWwwRoot(IFormFile? photo, string subfolder)
    {
        if (photo == null || photo.Length == 0) return null;

        try
        {
            string wwwrootPath = _hostEnvironment.WebRootPath;

            if (string.IsNullOrEmpty(wwwrootPath))
            {
                wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "WebUI", "wwwroot");
                if (!Directory.Exists(wwwrootPath))
                    wwwrootPath = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
            }

            var uploadsFolder = Path.Combine(wwwrootPath, "uploads", subfolder);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            return $"/uploads/{subfolder}/{uniqueFileName}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving photo: {ex.Message}", ex);
        }
    }
}