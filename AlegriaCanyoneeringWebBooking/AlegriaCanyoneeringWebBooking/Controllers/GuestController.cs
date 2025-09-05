using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using System;
using System.Linq;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class GuestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GuestController> _logger;

        public GuestController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<GuestController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;

            // Test connection
            if (!_context.Database.CanConnect())
            {
                throw new Exception("Cannot connect to database. Please check your connection string.");
            }
        }
       // GET: Anticipate(Add + Reserved Guests in one page)
        public async Task<IActionResult> Anticipate()
        {
            // Load operators
            var operators = await _context.Operators
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToListAsync();

            if (!operators.Any())
            {
                operators = new List<SelectListItem>
        {
            new SelectListItem { Text = "No operators available", Value = "" }
        };
            }

            // Load nationalities for dropdown
            var nationalities = await _context.Nationalities // Assuming you have a Nationalities DbSet
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(), // or whatever your primary key is
                    Text = n.NatName
                })
                .ToListAsync();

            if (!nationalities.Any())
            {
                nationalities = new List<SelectListItem>
        {
            new SelectListItem { Text = "No nationalities available", Value = "" }
        };
            }

            ViewBag.OperatorList = operators;
            ViewBag.NationalityList = nationalities;

            // Load Guests with Operator and Nationality
            var reservedGuests = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)  // Include nationality data
                .Where(g => g.BookingStatus == "anticipated" || g.BookingStatus == "reserved")
                .ToListAsync();

            var model = new GuestListViewModel
            {
                NewGuest = new Guest(),
                ReservedGuests = reservedGuests ?? new List<Guest>()
            };

            return View(model);
        }


        // POST: Anticipate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anticipate(GuestListViewModel model)
        {
            if (ModelState.IsValid)
            {
                var guest = model.NewGuest;
                guest.BookingStatus = "anticipated";

                // Assign Batch if it's not set
                if (string.IsNullOrEmpty(guest.Batch))
                {
                    guest.Batch = DateTime.Now.ToString("yyyyMMddHHmmss");
                }

                _context.Add(guest);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Anticipate));
            }

            // If validation fails, repopulate dropdowns
            await PopulateDropdowns();

            // Reload reserved guests
            model.ReservedGuests = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .ToListAsync();

            return View(model);
        }

        // Helper method to populate dropdowns
        private async Task PopulateDropdowns()
        {
            // Populate operators
            var operators = await _context.Operators
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToListAsync();

            if (!operators.Any())
            {
                operators = new List<SelectListItem>
        {
            new SelectListItem { Text = "No operators available", Value = "" }
        };
            }

            // Populate nationalities
            var nationalities = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.NatName
                })
                .ToListAsync();

            if (!nationalities.Any())
            {
                nationalities = new List<SelectListItem>
        {
            new SelectListItem { Text = "No nationalities available", Value = "" }
        };
            }

            ViewBag.OperatorList = operators;
            ViewBag.NationalityList = nationalities;
        }



        // GET: Guest/Reserve
        public async Task<IActionResult> Reserve()
        {
            var anticipatedGuests = await _context.Guests
        .Include(g => g.Operator)
        .Where(g => g.BookingStatus == "anticipated")
        .ToListAsync();
            return View(anticipatedGuests);
        }
        // GET: Guest/ReserveDetails/5
        public async Task<IActionResult> ReserveDetails(int id)
        {
            // Eagerly load the 'Operator' data along with the 'Guest' data
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality) // Eagerly load the related 'Operator' entity
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound(); // If the guest is not found, return a 404 page
            }

            // Pass the guest model (which includes the 'Operator') to the view
            return View(guest);
        }

        // GET: Guest/EditReserve/5
        public async Task<IActionResult> EditReserve(int id)
        {
            // Retrieve the guest and eagerly load the 'Operator' data
            var guest = await _context.Guests
                .Include(g => g.Operator)  // Eagerly load the related 'Operator' entity
                .FirstOrDefaultAsync(g => g.Id == id);  // Fetch the guest by its ID

            if (guest == null)
            {
                return NotFound();  // If the guest is not found, return a 404 page
            }

            // Retrieve operators from the database for the dropdown list
            var operators = await _context.Operators
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName // Or use any other property you want to display
                })
                .ToListAsync();

            // If the list is null or empty, create an empty list to avoid errors
            if (operators == null || !operators.Any()) // Ensure `System.Linq` is imported for `.Any()`
            {
                operators = new List<SelectListItem>
        {
            new SelectListItem { Text = "No operators available", Value = "" }
        };
            }

            // Pass the operator list to the ViewBag for use in the view
            ViewBag.OperatorList = operators;

            // Pass the guest model (which includes the 'Operator') to the view
            return View(guest);
        }


        // POST: Guest/EditReserve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReserve(int id, Guest guest)
        {
            if (id != guest.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(guest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GuestExists(guest.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Reserve));
            }
            return View(guest);
        }

        // GET: Guest/Accept
        public async Task<IActionResult> Accept()
        {
            var reservedGuests = await _context.Guests
                .Where(g => g.BookingStatus == "reserved")
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .ToListAsync();

            return View(reservedGuests);
        }

        // GET: Guest/ScanQR/5
        public async Task<IActionResult> ScanQR(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            // Generate QR code data - use proper nationality reference
            string qrData = $"GuestID:{guest.Id},Name:{guest.Fullname},Age:{guest.Age},Nationality:{guest.Nationality?.NatName ?? guest.NationalityType ?? "Unknown"},RFID:{guest.RFID},Operator:{guest.Operator?.BusinessName ?? "No Operator Available"}";

            string qrCodeImage = GenerateQRCodeBase64(qrData);

            ViewBag.QRCodeImage = qrCodeImage;
            ViewBag.QRData = qrData;
            ViewBag.Guest = guest;

            return View(guest);
        }

        // POST: Guest/ConfirmQR/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmQR(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.Nationality)
                .Include(g => g.Operator) // Include operator to check existing value
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            // Generate final QR code data
            string qrData = $"GuestID:{guest.Id},Name:{guest.Fullname},Age:{guest.Age},Nationality:{guest.Nationality?.NatName ?? guest.NationalityType ?? "Unknown"},Batch:{guest.Batch},RFID:{guest.RFID},Status:CONFIRMED";
            string qrCodeBase64 = GenerateQRCodeBase64(qrData);

            // Update guest with QR code and confirm status
            guest.QrCode = qrCodeBase64;
            guest.BookingStatus = "confirmed";

            // DON'T change OperatorId - keep the existing one or set to null if not needed
            // Remove this problematic line: guest.OperatorId = GetCurrentOperatorId();

            // If you really need to set an operator, do this instead:
            if (!guest.OperatorId.HasValue)
            {
                // Get the first available operator or a default one
                var firstOperator = await _context.Operators.FirstOrDefaultAsync();
                if (firstOperator != null)
                {
                    guest.OperatorId = firstOperator.OperatorId;
                }
                // If no operators exist, leave OperatorId as null (make sure your model allows this)
            }

            try
            {
                _context.Update(guest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Guest confirmed successfully with QR Code!";
                return RedirectToAction(nameof(Accept));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error confirming guest with ID: {GuestId}", id);
                TempData["ErrorMessage"] = "Error confirming guest. Please try again.";
                return RedirectToAction(nameof(ScanQR), new { id = id });
            }
        }

        private int GetCurrentOperatorId()
        {
            int operatorId;
            if (int.TryParse(User.Identity?.Name, out operatorId))
            {
                return operatorId;
            }
            return 0;
        }

        // POST: Guest/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            guest.BookingStatus = status;
            _context.Update(guest);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Anticipate));
        }

        private string GenerateQRCodeBase64(string data)
        {
            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q))
                using (Base64QRCode qrCode = new Base64QRCode(qrCodeData))
                {
                    string qrCodeImageAsBase64 = qrCode.GetGraphic(20, "#000000", "#FFFFFF", true);
                    return $"data:image/png;base64,{qrCodeImageAsBase64}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"QR Code generation error: {ex.Message}");
                return null;
            }
        }


        private bool GuestExists(int id)
        {
            return _context.Guests.Any(e => e.Id == id);
        }
    }
}