using AlegriaCanyoneeringWebBooking;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.WebUI.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("Drivers")]
[Authorize(Roles = "Super Admin,Admin,Operator")]
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
    // CREATE GET — with auto RefId, DPosition = Unix timestamp
    // =========================================================
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        // ✅ Auto-increment RefId from Drivers table
        var allRefIds = await _context.Drivers.Select(d => d.RefId).ToListAsync();
        int nextRefId = 100000;
        var numericRefIds = allRefIds
            .Where(r => !string.IsNullOrEmpty(r) && int.TryParse(r, out _))
            .Select(r => int.Parse(r))
            .Where(r => r >= 100000)
            .ToList();
        if (numericRefIds.Any())
            nextRefId = numericRefIds.Max() + 1;

        // ✅ DPosition = current Unix timestamp
        int nextDPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
            // ✅ Check duplicate RefId — auto-fix if collision
            bool refIdExists = await _context.Drivers.AnyAsync(d => d.RefId == model.RefId);
            if (refIdExists)
            {
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

            // ✅ DPosition = Unix timestamp, shift +1 if duplicate
            model.DPosition = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            while (await _context.Drivers.AnyAsync(d => d.DPosition == model.DPosition))
                model.DPosition++;

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
    // ✅ DPosition preserved — not changed on edit
    // =========================================================
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Driver model, IFormFile? PhotoFile)
    {
        ModelState.Remove("Image");
        ModelState.Remove("MName");
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

            // Normalize image paths — filter out local file system paths
            var normalizedData = data.Select(d => new
            {
                d.driverId,
                d.fullName,
                d.refId,
                d.pNumber,
                d.cNumber,
                d.ctcDate,
                image = NormalizeImagePath(d.image)
            }).ToList();

            return Json(new { draw, recordsFiltered = recordsTotal, recordsTotal, data = normalizedData });
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
    // ARRANGE QUEUE GET — show drivers ordered by DPosition
    // =========================================================
    [HttpGet("Arrange")]
    public async Task<IActionResult> Arrange()
    {
        var drivers = await _context.Drivers
            .OrderBy(d => d.DPosition)
            .Select(d => new
            {
                d.DriverId,
                d.RefId,
                fullName = (d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? ""),
                d.Image,
                d.DPosition
            })
            .ToListAsync();

        return View(drivers.Select(d => new DriverQueueItem
        {
            DriverId = d.DriverId,
            RefId = d.RefId,
            FullName = d.fullName.Trim(),
            Image = NormalizeImagePath(d.Image),
            DPosition = d.DPosition
        }).ToList());
    }

    // =========================================================
    // SAVE QUEUE ORDER — POST reordered driver IDs
    // Uses the smallest existing DPosition as base so the
    // original #1 value is preserved in the queue sequence
    // =========================================================
    [HttpPost("Arrange")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQueueOrder([FromBody] List<int> driverIds)
    {
        try
        {
            if (driverIds == null || !driverIds.Any())
                return Json(new { success = false, message = "No driver IDs provided." });

            var drivers = await _context.Drivers
                .Where(d => driverIds.Contains(d.DriverId))
                .ToListAsync();

            // Use the smallest existing DPosition as base
            // so the overall queue origin stays stable
            int baseTimestamp = drivers.Min(d => d.DPosition);

            for (int i = 0; i < driverIds.Count; i++)
            {
                var driver = drivers.FirstOrDefault(d => d.DriverId == driverIds[i]);
                if (driver != null)
                    driver.DPosition = baseTimestamp + (i * 10);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Queue order saved for {driverIds.Count} driver(s)." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // AUTO QUEUE
    // ✅ DPosition < 1_000_000_000 = old auto-increment / zero → fix
    // ✅ DPosition ≥ 1_000_000_000 = valid Unix timestamp → leave
    // =========================================================
    [HttpPost("AutoQueue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoQueue()
    {
        try
        {
            const int UnixThreshold = 1_000_000_000;

            var allDrivers = await _context.Drivers
                .OrderBy(d => d.DPosition)
                .ToListAsync();

            int lastTimestamp = allDrivers
                .Where(d => d.DPosition >= UnixThreshold)
                .Select(d => d.DPosition)
                .DefaultIfEmpty((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10)
                .Max();

            int fixedCount = 0;

            foreach (var driver in allDrivers)
            {
                if (driver.DPosition < UnixThreshold)
                {
                    lastTimestamp += 10;
                    while (allDrivers.Any(d => d.DriverId != driver.DriverId && d.DPosition == lastTimestamp))
                        lastTimestamp++;
                    driver.DPosition = lastTimestamp;
                    fixedCount++;
                }
            }

            if (fixedCount == 0)
                return Json(new { success = true, message = "All drivers already have a valid queue timestamp. Nothing changed.", count = 0 });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"{fixedCount} driver(s) assigned a queue timestamp.", count = fixedCount });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // =========================================================
    // HELPER — Normalize image path (filter out local file paths)
    // =========================================================
    private static string? NormalizeImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        // Local file paths (e.g. C:\EDITED\...) are not web-accessible
        if (path.Contains(":\\") || path.Contains(":/") && !path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("\\\\"))
            return null;
        return path;
    }

    // =========================================================
    // HELPER — Save photo to wwwroot/uploads/drivers
    // =========================================================
    private async Task<string?> SavePhotoToWwwRoot(IFormFile? photo)
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

            var uploadsFolder = Path.Combine(wwwrootPath, "uploads", "drivers");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(photo.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await photo.CopyToAsync(stream);

            return $"/uploads/drivers/{uniqueFileName}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving photo: {ex.Message}", ex);
        }
    }
}