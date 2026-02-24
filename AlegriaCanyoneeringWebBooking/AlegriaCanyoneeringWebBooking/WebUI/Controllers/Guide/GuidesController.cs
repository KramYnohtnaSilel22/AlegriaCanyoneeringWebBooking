using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("Guides")]
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
    // CREATE GET — with auto Rfid and TPosition
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

        // ✅ TPosition auto-increment
        var maxTPosition = await _context.Guides.MaxAsync(t => (int?)t.TPosition) ?? 100000;
        int nextTPosition = maxTPosition + 1;

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
            // ✅ Check duplicate Rfid
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

            // ✅ Check duplicate TPosition
            bool tposExists = await _context.Guides.AnyAsync(t => t.TPosition == model.TPosition);
            if (tposExists)
            {
                var maxTPos = await _context.Guides.MaxAsync(t => (int?)t.TPosition) ?? 100000;
                model.TPosition = maxTPos + 1;
            }

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
            // else: keeps existing image from hidden input

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