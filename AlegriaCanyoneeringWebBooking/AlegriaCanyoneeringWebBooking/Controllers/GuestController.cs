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
               .Include(g => g.Nationality)
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
                var now = DateTime.Now;
                guest.Month = now.ToString("yyyy-MM");
                guest.DateShort = now.ToString("MMM dd, yyyy");
                // Assign Batch if it's not set
                if (string.IsNullOrEmpty(guest.Batch))
                {
                    guest.Batch = DateTime.Now.ToString("yyyyMMddHHmmss");
                }

                // Auto-generate RFID if not already set
                if (string.IsNullOrEmpty(guest.RFID))
                {
                    guest.RFID = GenerateRFID();
                }


                _context.Add(guest);
                await _context.SaveChangesAsync();
                // TempData for Toast
                TempData["ToastMessage"] = "Guest added successfully!";
                TempData["ToastType"] = "success"; // Can also be: info, warning, danger

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

        // Helper method to auto-generate a unique RFID
        private string GenerateRFID()
        {
            // Generates a 12-character unique alphanumeric RFID prefixed with "RFID"
            return "RFID" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
        }


        //// POST: Anticipate
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Anticipate(GuestListViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var guest = model.NewGuest;
        //        guest.BookingStatus = "anticipated";

        //        // Assign Batch if it's not set
        //        if (string.IsNullOrEmpty(guest.Batch))
        //        {
        //            guest.Batch = DateTime.Now.ToString("yyyyMMddHHmmss");
        //        }

        //        _context.Add(guest);
        //        await _context.SaveChangesAsync();

        //        return RedirectToAction(nameof(Anticipate));
        //    }

        //    // If validation fails, repopulate dropdowns
        //    await PopulateDropdowns();

        //    // Reload reserved guests
        //    model.ReservedGuests = await _context.Guests
        //        .Include(g => g.Operator)
        //        .Include(g => g.Nationality)
        //        .ToListAsync();

        //    return View(model);
        //}

        //// Helper method to populate dropdowns
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



        // GET: Guest/ReserveDetails/5
        public async Task<IActionResult> ReserveDetails(int id)
        {
            // Eagerly load the related entities
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .Include(g => g.Driver) // ✅ Include Driver
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }
            // ✅ Load drivers for dropdown
            ViewBag.DriverList = await _context.Drivers
                .Select(d => new SelectListItem
                {
                    Value = d.DriverId.ToString(),
                    Text = d.FName
                })
                .ToListAsync();


            return View(guest);
        }


        // GET: Guest/EditReserve/5
        public async Task<IActionResult> EditReserve(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            // Operators dropdown
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

            // Nationalities dropdown
            var nationalities = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),     // ✅ correct property
                    Text = n.NatName             // ✅ correct property
                })
                .ToListAsync();

            if (!nationalities.Any())
            {
                nationalities = new List<SelectListItem>
            {
                new SelectListItem { Text = "No countries available", Value = "" }
            };
            }
            ViewBag.NationalityList = nationalities;

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
                    // ✅ Auto-generate RFID if missing
                    if (string.IsNullOrEmpty(guest.RFID))
                    {
                        guest.RFID = GenerateRFID();
                    }

                    _context.Update(guest);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Reserve));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Guests.Any(e => e.Id == guest.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // repopulate dropdowns
            ViewBag.OperatorList = await _context.Operators
                .Select(o => new SelectListItem
                {
                    Value = o.OperatorId.ToString(),
                    Text = o.BusinessName
                })
                .ToListAsync();

            ViewBag.NationalityList = await _context.Nationalities
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.NatName
                })
                .ToListAsync();

            return View(guest);
        }
        // GET: Guest/Accept
        public async Task<IActionResult> Accept()
        {
            var confirmedGuests = await _context.Guests
                .Include(g => g.Nationality)
                .Include(g => g.Operator)
                .Include(g => g.Driver)
                .Include(g => g.Guide)
                .Where(g => g.BookingStatus == "reserved")
                .ToListAsync();

            return View(confirmedGuests);
        }

        // GET: Guest/ScanQR/5
        public async Task<IActionResult> ScanQR(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .Include(g => g.Driver) // ✅ Include Driver
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            // ✅ Load list of available drivers for dropdown in View
            ViewBag.DriverList = await _context.Drivers
                .Select(d => new SelectListItem
                {
                    Value = d.DriverId.ToString(),
                    Text = d.FName
                })
                .ToListAsync();

            // ✅ QR Code data with driver info
            string qrData =
                $"Guest Details\n" +
                $"-----------------------------------\n" +
                $"ID             : {guest.Id}\n" +
                $"Full Name      : {guest.Fullname}\n" +
                $"Age            : {guest.Age}\n" +
                $"Gender         : {guest.Gender}\n" +
                $"Nationality    : {guest.NationalityType}\n" +
                $"No. of Guests  : {guest.NumberOfGuests}\n" +
                $"Nat. Status    : {guest.NationalityId}\n" +
                $"Operator       : {guest.Operator?.BusinessName ?? "N/A"}\n" +
               $"Driver         : {(guest.Driver != null ? guest.Driver.FName : "None")}\n" +
                $"Booking Date   : {guest.Date:yyyy-MM-dd}\n" +
                $"Arrival Date   : {guest.ArrivalDate:yyyy-MM-dd}\n" +
                $"Month          : {guest.Month}\n" +
                $"Batch          : {guest.Batch}\n" +
                $"RFID           : {guest.RFID}\n" +
                $"Status         : {guest.BookingStatus?.ToUpper() ?? "N/A"}\n";

            ViewBag.QRCodeImage = GenerateQRCodeBase64(qrData);
            ViewBag.QRData = qrData;
            ViewBag.Guest = guest;

            return View(guest);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmQR(int id, int? driverId)
        {
            var guest = await _context.Guests
                .Include(g => g.Nationality)
                .Include(g => g.Operator)
                .Include(g => g.Driver)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            // Assign Driver if provided
            if (driverId.HasValue)
            {
                var driver = await _context.Drivers.FindAsync(driverId.Value);
                if (driver != null)
                {
                    guest.DriverId = driverId;
                }
            }

            // ✅ FIXED: Load driver after potential assignment
            if (guest.DriverId.HasValue)
            {
                guest.Driver = await _context.Drivers.FindAsync(guest.DriverId.Value);
            }

            // ✅ FIXED: Generate QR Data with proper driver display
            string qrData =
                $"Guest Details\n" +
                $"-----------------------------------\n" +
                $"ID             : {guest.Id}\n" +
                $"Full Name      : {guest.Fullname}\n" +
                $"Age            : {guest.Age}\n" +
                $"Gender         : {guest.Gender}\n" +
                $"Nationality    : {guest.NationalityType}\n" +
                $"No. of Guests  : {guest.NumberOfGuests}\n" +
                $"Nat. Status    : {guest.NationalityId}\n" +
                $"Operator       : {guest.Operator?.BusinessName ?? "N/A"}\n" +
                $"Driver         : {(guest.Driver != null ? $"{guest.Driver.RefId} - {guest.Driver.FName} {guest.Driver.LName}" : "None")}\n" +
                $"Booking Date   : {guest.Date}\n" +
                $"Arrival Date   : {guest.ArrivalDate}\n" +
                $"Month          : {guest.Month}\n" +
                $"Batch          : {guest.Batch}\n" +
                $"RFID           : {guest.RFID}\n" +
                $"Status         : confirm\n";

            guest.QrCode = GenerateQRCodeBase64(qrData);
            guest.BookingStatus = "confirm";

            try
            {
                _context.Update(guest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Guest confirmed with Driver!";
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

            // ✅ Redirect to Accept if status is confirmed
            if (status.ToLower() == "reserved")
            {
                return RedirectToAction(nameof(Accept));
            }

            // Otherwise, back to Anticipate list
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBooking(int id, List<int> driverIds, List<int> guideIds)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound();
            }

           // ✅ Store selected driver and guide IDs(you might need to modify your Guest model)
           // For now, we'll store the first selected IDs for backward compatibility
           if (driverIds != null && driverIds.Count > 0)
            {
                guest.DriverId = driverIds.First();
                // If you want to store all selected drivers, you'll need a separate relationship table
            }

            if (guideIds != null && guideIds.Count > 0)
            {
                guest.GuideId = guideIds.First();
                // If you want to store all selected guides, you'll need a separate relationship table
            }

            // ✅ Set booking status to confirmed
            guest.BookingStatus = "reserved";

            _context.Update(guest);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Accept)); // Go back to Final Bookings
        }



        public async Task<IActionResult> Book(int id)
        {
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .Include(g => g.Driver)
                .Include(g => g.Guide)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
            {
                return NotFound();
            }

            ViewBag.DriverList = await _context.Drivers
                .Select(d => new SelectListItem
                {
                    Value = d.DriverId.ToString(),
                    Text = $"{d.RefId} - {d.FName} {d.LName}"
                })
                .ToListAsync();

            ViewBag.GuideList = await _context.Guides
             .Select(g => new SelectListItem
             {
                 Value = g.GuideId.ToString(),
                 Text = $"{g.FName} {g.LName}"
             })
             .ToListAsync();

            return View(guest); // Returns Book.cshtml
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int id, List<int> driverIds, List<int> guideIds)
        {
            var guest = await _context.Guests
                .Include(g => g.Operator)
                .Include(g => g.Nationality)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guest == null)
                return NotFound();

            // For backward compatibility, assign the first selected driver and guide
            guest.DriverId = driverIds?.FirstOrDefault();
            guest.GuideId = guideIds?.FirstOrDefault();
            guest.BookingStatus = "reserved";

            _context.Update(guest);
            await _context.SaveChangesAsync();

            // Count Local vs Foreign
            int localCount = 0, foreignCount = 0;
            if (guest.NationalityType?.ToLower() == "local")
                localCount = guest.NumberOfGuests;
            else
                foreignCount = guest.NumberOfGuests;

            // Create Batch with multiple drivers/guides count
            var batch = new Batch
            {
                OperatorId = guest.OperatorId ?? 0,
                NoOfLocalGuest = localCount,
                NoOfForeignGuest = foreignCount,
                NoOfTGuide = guideIds?.Count ?? 0,
                NoOfMDriver = driverIds?.Count ?? 0,
                TotalNoOfGuest = guest.NumberOfGuests,
                ArrivalDate = DateTime.Parse(guest.ArrivalDate)
            };

            _context.Batches.Add(batch);
            await _context.SaveChangesAsync();

            return RedirectToAction("Accept"); // back to list
        }


        private bool GuestExists(int id)
        {
            return _context.Guests.Any(e => e.Id == id);
        }
    }
}