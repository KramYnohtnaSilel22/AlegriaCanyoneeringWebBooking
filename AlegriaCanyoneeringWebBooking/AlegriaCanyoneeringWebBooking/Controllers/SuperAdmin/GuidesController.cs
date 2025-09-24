using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class GuidesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public GuidesController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Guides
        public async Task<IActionResult> Index()
        {
            return View(await _context.Guides.ToListAsync());
        }

        // GET: Guides/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .FirstOrDefaultAsync(m => m.GuideId == id);

            if (guide == null) return NotFound();

            return View(guide);
        }

        // GET: Guides/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Guides/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guide guide, IFormFile imageFile)
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

                    // unique filename with timestamp
                    fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                    string path = Path.Combine(wwwRootPath + "/uploads/", fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    // save relative path in DB
                    guide.Image = "/uploads/" + fileName;
                }

                _context.Add(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(guide);
        }

        // GET: Guides/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            return View(guide);
        }

        // POST: Guides/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Guide guide, IFormFile? imageFile)
        {
            if (id != guide.GuideId) return NotFound();

            // Remove Image validation because we handle it manually
            ModelState.Remove("Image");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingGuide = await _context.Guides.AsNoTracking().FirstOrDefaultAsync(g => g.GuideId == id);
                    if (existingGuide == null) return NotFound();

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

                        // delete old image if exists
                        if (!string.IsNullOrEmpty(existingGuide.Image))
                        {
                            string oldPath = Path.Combine(wwwRootPath, existingGuide.Image.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }

                        guide.Image = "/uploads/" + fileName;
                    }
                    else
                    {
                        // keep existing image
                        guide.Image = existingGuide.Image;
                    }

                    _context.Update(guide);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Guides.Any(e => e.GuideId == guide.GuideId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(guide);
        }

        // GET: Guides/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .FirstOrDefaultAsync(m => m.GuideId == id);

            if (guide == null) return NotFound();

            return View(guide);
        }

        // POST: Guides/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool GuideExists(int id)
        {
            return _context.Guides.Any(e => e.GuideId == id);
        }
    }
}
