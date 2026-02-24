using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("Drivers")]
public class DriversController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public DriversController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
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
    // CREATE GET — with auto RefId and DPosition
    // =========================================================
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        // ✅ Get max DriverId-based sequence (simpler and reliable)
        var maxId = await _context.Drivers.MaxAsync(d => (int?)d.DriverId) ?? 0;
        int nextSequence = maxId + 1;

        // ✅ Simple auto-increment RefId starting at 100000
        var allRefIds = await _context.Drivers.Select(d => d.RefId).ToListAsync();
        int nextRefId = 100000;
        var numericRefIds = allRefIds
            .Where(r => !string.IsNullOrEmpty(r) && int.TryParse(r, out _))
            .Select(r => int.Parse(r))
            .Where(r => r >= 100000)
            .ToList();
        if (numericRefIds.Any())
            nextRefId = numericRefIds.Max() + 1;

        // ✅ DPosition auto-increment
        var maxDPosition = await _context.Drivers.MaxAsync(d => (int?)d.DPosition) ?? 100000;
        int nextDPosition = maxDPosition + 1;

        ViewData["Action"] = "Create";
        return PartialView("_DriverForm", new Driver
        {
            RefId = nextRefId.ToString(),
            DPosition = nextDPosition
        });
    }

    // =========================================================
    // CREATE POST
    // =========================================================
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Driver model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("Guests"); // ✅ Remove navigation property from validation

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
            // ✅ Check duplicate RefId
            bool refIdExists = await _context.Drivers.AnyAsync(d => d.RefId == model.RefId);
            if (refIdExists)
            {
                // Auto-fix: generate new unique RefId
                var maxRefId = await _context.Drivers
                    .Where(d => d.RefId != null)
                    .Select(d => d.RefId)
                    .ToListAsync();
                int next = maxRefId
                    .Where(r => int.TryParse(r, out _))
                    .Select(r => int.Parse(r))
                    .DefaultIfEmpty(100000)
                    .Max() + 1;
                model.RefId = next.ToString();
            }

            // ✅ Check duplicate DPosition
            bool dposExists = await _context.Drivers.AnyAsync(d => d.DPosition == model.DPosition);
            if (dposExists)
            {
                var maxDPos = await _context.Drivers.MaxAsync(d => (int?)d.DPosition) ?? 100000;
                model.DPosition = maxDPos + 1;
            }

            // ✅ Add this before saving — prevent null DB error
            model.MName = string.IsNullOrWhiteSpace(model.MName) ? "" : model.MName;
            model.Image = await SavePhotoToWwwRoot(PhotoFile);

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Driver created successfully." });
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
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound();

            ViewData["Action"] = "Edit";
            return PartialView("_DriverForm", driver);
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
    public async Task<IActionResult> Edit(Driver model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
        ModelState.Remove("Guests"); // ✅ Remove navigation property from validation

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
            var newPath = await SavePhotoToWwwRoot(PhotoFile);
            if (newPath != null)
                model.Image = newPath;

            _context.Update(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Driver updated successfully." });
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
    // DELETE
    // =========================================================
    [HttpPost("DeleteAjax")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        try
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return Json(new { success = false, message = "Driver not found." });

            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Driver deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // DATATABLE
    // =========================================================
    [HttpPost("GetDriversData")]
    public async Task<IActionResult> GetDriversData()
    {
        try
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var query = _context.Drivers.AsQueryable();

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(d =>
                    (d.FName != null && d.FName.Contains(searchValue)) ||
                    (d.LName != null && d.LName.Contains(searchValue)) ||
                    (d.RefId != null && d.RefId.Contains(searchValue)));
            }

            var recordsTotal = await query.CountAsync();

            var data = await query
                .OrderBy(d => d.DriverId)
                .Skip(start)
                .Take(length)
                .Select(d => new
                {
                    driverId = d.DriverId,
                    fullName = (d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? ""),
                    refId = d.RefId ?? "",
                    pNumber = d.PNumber ?? "----",
                    cNumber = d.CNumber ?? "----",
                    ctcDate = d.CtcDate ?? "----",
                    image = d.Image
                })
                .ToListAsync();

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
    // HELPER — Save photo to wwwroot/uploads/drivers
    // =========================================================
    private async Task<string?> SavePhotoToWwwRoot(IFormFile? photo)
    {
        if (photo == null || photo.Length == 0) return null;

        try
        {
            // ✅ Match the path from Program.cs: WebUI/wwwroot
            string wwwrootPath = _hostEnvironment.WebRootPath;

            if (string.IsNullOrEmpty(wwwrootPath))
            {
                // Fallback: construct path to match Program.cs configuration
                // Try WebUI/wwwroot first (matches Program.cs)
                wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "WebUI", "wwwroot");

                // If WebUI/wwwroot doesn't exist, try root wwwroot
                if (!Directory.Exists(wwwrootPath))
                {
                    wwwrootPath = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
                }
            }

            var uploadsFolder = Path.Combine(wwwrootPath, "uploads", "drivers");

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            // Return web-accessible path
            return $"/uploads/drivers/{uniqueFileName}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving photo: {ex.Message}", ex);
        }
    }
}