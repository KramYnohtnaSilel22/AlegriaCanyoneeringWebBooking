using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;
using QRCoder;
using System.Linq;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using AlegriaCanyoneeringWebBooking.ViewModel;

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
        // GET: Anticipate (Add + Reserved Guests in one page)
        public async Task<IActionResult> Anticipate()
        {
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

            ViewBag.OperatorList = operators;

            // ✅ Load Guests with Operator
            var reservedGuests = await _context.Guests
                .Include(g => g.Operator)
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
                // ✅ Assign Batch if it’s not set
                if (string.IsNullOrEmpty(guest.Batch))
                {
                    guest.Batch = DateTime.Now.ToString("yyyyMMddHHmmss"); // unique batch id
                }
                _context.Add(guest);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Anticipate));
            }

            // If validation fails, repopulate operators and guests
            var operators = _context.Operators
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToList();

            if (operators == null || !operators.Any())
            {
                operators = new List<SelectListItem>
        {
            new SelectListItem { Text = "No operators available", Value = "" }
        };
            }

            ViewBag.OperatorList = operators;

            // Reload reserved guests
            model.ReservedGuests = _context.Guests.ToList();

            return View(model);
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
                .Include(g => g.Operator)  // Eagerly load the related 'Operator' entity
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
                .Include(g => g.Operator)  // Assuming `Operator` is a navigation property
                .ToListAsync();

            return View(reservedGuests);
        }


        // GET: Guest/ScanQR/5
        public async Task<IActionResult> ScanQR(int id)
        {
            // Eagerly load the 'Operator' data along with the 'Guest' data
            var guest = await _context.Guests
                .Include(g => g.Operator)  // Eagerly load the related 'Operator' entity
                .FirstOrDefaultAsync(g => g.Id == id);  // Get the specific guest by ID

            if (guest == null)
            {
                return NotFound(); // If the guest is not found, return a 404
            }

            // Generate QR code data based on guest model, including Operator's BusinessName
            string qrData = $"GuestID:{guest.Id},Name:{guest.Fullname},Age:{guest.Age},Nationality:{guest.Nationality},RFID:{guest.RFID},Operator:{guest.Operator?.BusinessName ?? "No Operator Available"}";

            // Generate QR code image as base64
            string qrCodeImage = GenerateQRCodeBase64(qrData);

            // Pass QR code data to view
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
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

            // Generate final QR code data
            string qrData = $"GuestID:{guest.Id},Name:{guest.Fullname},Age:{guest.Age},Nationality:{guest.Nationality},RFID:{guest.RFID},Status:CONFIRMED";

            string qrCodeBase64 = GenerateQRCodeBase64(qrData);

            // Update guest with QR code and confirm status
            guest.QrCode = qrCodeBase64;
            guest.BookingStatus = "confirmed";
            guest.OperatorId = GetCurrentOperatorId();

            _context.Update(guest);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Guest confirmed successfully with QR Code!";
            return RedirectToAction(nameof(Accept));
        }

        private int GetCurrentOperatorId()
        {
            int operatorId;
            if (int.TryParse(User.Identity?.Name, out operatorId))
            {
                return operatorId;
            }
            return 0; // or throw exception or handle no operator found
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

        // Method to generate QR code as Base64 string
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
