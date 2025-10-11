
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace AlegriaCanyoneeringWebBooking
{
    [Route("api/guestapi")]  // This will make sure the base URL is 'api/guestapi'
    [ApiController]
    public class GuestApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GuestApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("test")]
        public IActionResult TestApi()
        {
            return Ok(new { message = "API is working!" });
        }

        [HttpPost("new-booking")]
        public async Task<IActionResult> NewBookingApi([FromBody] GuestListViewModel model)
        {
            // Check if BatchGuestsJson is provided
            if (string.IsNullOrWhiteSpace(model.BatchGuestsJson))
            {
                return BadRequest(new { message = "Batch guest data cannot be empty", status = "error" });
            }

            List<Guest> batchGuests;
            try
            {
                // Log the received JSON to see what was sent
                Console.WriteLine("Received BatchGuestsJson: " + model.BatchGuestsJson);

                // Deserialize JSON into a list of guests
                batchGuests = JsonSerializer.Deserialize<List<Guest>>(model.BatchGuestsJson);

                // If no valid guest data is found, return bad request
                if (batchGuests == null || batchGuests.Count == 0)
                {
                    return BadRequest(new { message = "No valid guest data found", status = "error" });
                }
            }
            catch (JsonException ex)
            {
                // Log any JSON errors
                Console.WriteLine("JSON Error: " + ex.Message);
                return BadRequest(new { message = "Invalid JSON format for guest data", status = "error" });
            }

            string batchId = DateTime.Now.ToString("yyyyMMddHHmmss");

            // Add each guest to the database
            foreach (var guest in batchGuests)
            {
                guest.BookingStatus = 0;
                guest.Batch = batchId;
                //guest.RFID = RfdId;
                guest.Month = DateTime.Today.ToString("yyyy-MM");
                guest.DateShort = DateTime.Today.ToString("MMM dd, yyyy");

                _context.Guests.Add(guest);
            }

            try
            {
                // Save new guests to the database
                await _context.SaveChangesAsync();

                // Fetch updated list of anticipated guests
                var reservedGuests = await _context.Guests
                    .Where(g => g.BookingStatus == 0)
                    .OrderBy(g => g.Id)
                    .ToListAsync();

                var batchLeaders = reservedGuests
                    .GroupBy(g => g.Batch)
                    .Select(batch => batch.OrderBy(x => x.Id).First())
                    .ToList();




                // Return successful response with details
                return Ok(new
                {
                    message = "Guests added successfully!",
                    status = "success",
                    batchId,
                    guestCount = batchGuests.Count,
                    anticipatedGuests = batchLeaders // Return the updated list of anticipated guests
                });
            }
            catch (Exception ex)
            {
                // Log any errors and return a server error response
                return StatusCode(500, new { message = $"An error occurred while saving guests: {ex.Message}", status = "error" });
            }
        }


        [HttpGet("reserved-guests")]
        public async Task<IActionResult> GetReservedGuestsApi()
        {
            var reservedGuests = await _context.Guests
                .Where(g => g.BookingStatus == 2 || g.BookingStatus == 0)
                .OrderBy(g => g.Id)
                .ToListAsync();

            var batchLeaders = reservedGuests
                .GroupBy(g => g.Batch)
                .Select(batch => batch.OrderBy(x => x.Id).First())
                .ToList();

            return Ok(batchLeaders);
        }

        // POST: api/guest/update-status/{id}
        [HttpPost("update-status/{id}")]
        public async Task<IActionResult> UpdateStatusApi(int id, [FromBody] string status)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound(new { message = "Guest not found", status = "error" });
            }

            guest.BookingStatus = 2;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Status updated successfully", status = "success" });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Concurrency conflict: guest may have been modified by another process.", status = "error" });
            }
        }

        // POST: api/guest/delete/{id}
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> DeleteApi(int id)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                return NotFound(new { message = "Guest not found", status = "error" });
            }

            _context.Guests.Remove(guest);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Guest deleted successfully!", status = "success" });
        }

        // GET: api/guest/get-nationalities
        [HttpGet("get-nationalities")]
        public async Task<IActionResult> GetNationalitiesApi()
        {
            try
            {
                var nationalities = await _context.Nationalities
                    .Where(n => n.NatName != "Within Cebu Province" && n.NatName != "Outside Cebu Province")
                    .OrderBy(n => n.NatName)
                    .Select(n => new
                    {
                        n.id,
                        n.NatName
                    })
                    .ToListAsync();

                return Ok(nationalities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/guest/reserve-details/{id}
        [HttpGet("reserve-details/{id}")]
        public async Task<IActionResult> ReserveDetailsApi(int id)
        {
            var mainGuest = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (mainGuest == null)
            {
                return NotFound(new { message = "Guest not found", status = "error" });
            }

            var guestsInBatch = await _context.Guests
                .Include(g => g.OperatorList)
                .Include(g => g.NationalityEntity)
                .Where(g => g.Batch == mainGuest.Batch && g.Id != mainGuest.Id)
                .OrderBy(g => g.Id)
                .Take(4)
                .ToListAsync();

            var model = new GuestDetailsViewModel
            {
                Guest = mainGuest,
                GuestsInBatch = guestsInBatch
            };

            return Ok(model);
        }



        // Helper Methods
        private string GenerateRFID() => "RFID" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
    }
}
