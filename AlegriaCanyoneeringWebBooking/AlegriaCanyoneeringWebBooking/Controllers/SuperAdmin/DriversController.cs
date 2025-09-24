using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class DriversController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public DriversController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Drivers
        public async Task<IActionResult> Index()
        {
            var drivers = await _context.Drivers.ToListAsync();
            return View(drivers);
        }

        // GET: Drivers/Details/5
        public async Task<IActionResult> Details(long id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }
            return View(driver);
        }

        // GET: Drivers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Drivers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Driver driver, IFormFile imageFile)
        {
            // remove Image validation so it won’t block
            ModelState.Remove("Image");

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string wwwRootPath = _hostEnvironment.WebRootPath;
                    string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName);
                    string extension = Path.GetExtension(imageFile.FileName);

                    fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                    string path = Path.Combine(wwwRootPath + "/uploads/", fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    driver.Image = "/uploads/" + fileName;
                }

                _context.Add(driver);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(driver);
        }



        // GET: Drivers/Edit/5
        public async Task<IActionResult> Edit(long id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }
            return View(driver);
        }

        // POST: Drivers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Driver driver, IFormFile? imageFile)
        {
            if (id != driver.DriverId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string wwwRootPath = _hostEnvironment.WebRootPath;
                        string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName);
                        string extension = Path.GetExtension(imageFile.FileName);

                        fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                        string path = Path.Combine(wwwRootPath + "/uploads/", fileName);

                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(stream);
                        }

                        driver.Image = "/uploads/" + fileName;
                    }

                    _context.Update(driver);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Drivers.Any(e => e.DriverId == driver.DriverId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(driver);
        }

        // GET: Drivers/Delete/5
        public async Task<IActionResult> Delete(long id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }
            return View(driver);
        }

        // POST: Drivers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver != null)
            {
                _context.Drivers.Remove(driver);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
