#if DEBUG
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.Domain.Models;
namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Route("api/guestapi")]  // This will make sure the base URL is 'api/guestapi'
    [ApiController]
    public class GuestApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public GuestApiController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult TestApi()
        {
            return Ok(new { message = "API is working!" });
        }

        // POST: api/v1/guests/bookings
        [HttpPost("bookings")]
        public async Task<IActionResult> CreateBooking([FromBody] object request)
        {
            try
            {
                // Parse the request manually without DTOs
                var requestJson = JsonSerializer.Serialize(request);
                var jsonDocument = JsonDocument.Parse(requestJson);

                if (!jsonDocument.RootElement.TryGetProperty("guests", out var guestsElement) ||
                    guestsElement.ValueKind != JsonValueKind.Array)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Guest data is required",
                        Data = null
                    });
                }

                var guests = new List<Guest>();
                string batchId = DateTime.Now.ToString("yyyyMMddHHmmss");

                foreach (var guestElement in guestsElement.EnumerateArray())
                {
                    // Validate required fields
                    if (!guestElement.TryGetProperty("fullname", out var fullnameElement) ||
                        string.IsNullOrWhiteSpace(fullnameElement.GetString()))
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Guest name is required for all guests",
                            Data = null
                        });
                    }

                    if (!guestElement.TryGetProperty("age", out var ageElement) ||
                        !ageElement.TryGetInt32(out int age) || age <= 0)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Valid age is required for all guests",
                            Data = null
                        });
                    }

                    if (!guestElement.TryGetProperty("operatorId", out var operatorIdElement) ||
                        !operatorIdElement.TryGetInt32(out int operatorId) || operatorId <= 0)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Operator is required for all guests",
                            Data = null
                        });
                    }

                    if (!guestElement.TryGetProperty("nationalityId", out var nationalityIdElement) ||
                        !nationalityIdElement.TryGetInt32(out int nationalityId) || nationalityId <= 0)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Nationality is required for all guests",
                            Data = null
                        });
                    }

                    // Create guest entity
                    var guest = new Guest
                    {
                        Fullname = fullnameElement.GetString(),

                        // Fix for CS0029: Cannot implicitly convert type 'int' to 'string'  
                        // Update the `Age` property assignment to convert the integer `age` to a string.  

                        Age = age.ToString(),
                
                        Gender = guestElement.TryGetProperty("gender", out var genderElement) ? genderElement.GetString() : "Male",
                        ContactNumber = guestElement.TryGetProperty("contactNumber", out var contactElement) ? contactElement.GetString() : null,
                        NationalityType = guestElement.TryGetProperty("nationalityType", out var nationalityTypeElement) ? nationalityTypeElement.GetString() : "Local",
                        NationalityId = nationalityId,
                        OperatorId = operatorId,
                        Area = guestElement.TryGetProperty("area", out var areaElement) ? areaElement.GetString() : "Wonder Falls",
                        Date = guestElement.TryGetProperty("date", out var dateElement) ? dateElement.GetString() : DateTime.Today.ToString("yyyy-MM-dd"),
                        ArrivalDate = guestElement.TryGetProperty("arrivalDate", out var arrivalDateElement) ? arrivalDateElement.GetString() : DateTime.Today.ToString("yyyy-MM-dd"),
                        BookingStatus = 0, // Active/Anticipated
                        Batch = batchId,
                        Month = DateTime.Today.ToString("yyyy-MM"),
                        DateShort = DateTime.Today.ToString("MMM dd, yyyy"),
                      
                    };

                    guests.Add(guest);
                    _context.Guests.Add(guest);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created booking batch {BatchId} with {GuestCount} guests", batchId, guests.Count);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Booking created successfully",
                    Data = new
                    {
                        batchId,
                        guestCount = guests.Count,
                        createdAt = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while creating booking",
                    Data = null
                });
            }
        }

        // GET: api/v1/guests/nationalities
        [HttpGet("nationalities")]
        public async Task<IActionResult> GetNationalities()
        {
            try
            {
                var nationalities = await _context.Nationalities
                    .Where(n => n.NatName != "Within Cebu Province" && n.NatName != "Outside Cebu Province")
                    .OrderBy(n => n.NatName)
                    .Select(n => new
                    {
                        id = n.id,
                        name = n.NatName
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Nationalities retrieved successfully",
                    Data = nationalities
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nationalities");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving nationalities",
                    Data = null
                });
            }
        }

        // GET: api/v1/guests/operators
        [HttpGet("operators")]
        public async Task<IActionResult> GetOperators()
        {
            try
            {
                var operators = await _context.Operators
                    .OrderBy(o => o.BusinessName)
                    .Select(o => new
                    {
                        id = o.Id,
                        name = o.BusinessName
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Operators retrieved successfully",
                    Data = operators
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operators");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving operators",
                    Data = null
                });
            }
        }

        [HttpGet("reserved-guests")]
        public async Task<IActionResult> GetReservedGuestsApi()
        {
            try
            {
                // ✅ TEMPORARILY REMOVE THE ERROR FOR TESTING
                // throw new Exception("Test 500 error - API is working but forcing error for testing");

                var reservedGuests = await _context.Guests
                    .Where(g => g.BookingStatus == 2 || g.BookingStatus == 0)
                    .OrderBy(g => g.Id)
                    .ToListAsync();

                if (!reservedGuests.Any())
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "No reserved guests found",
                        Data = new List<object>()
                    });
                }

                var batchLeaders = reservedGuests
                    .GroupBy(g => g.Batch)
                    .Select(batch => new
                    {
                        Id = batch.OrderBy(x => x.Id).First().Id,
                        Operator = batch.OrderBy(x => x.Id).First().OperatorList?.BusinessName ?? "No Operator",
                        TotalGuests = batch.Count(),
                        DateShort = batch.OrderBy(x => x.Id).First().DateShort,
                        BookingStatus = batch.OrderBy(x => x.Id).First().BookingStatus,
                        Batch = batch.Key,
                        Name = batch.OrderBy(x => x.Id).First().Fullname,
                        Age = batch.OrderBy(x => x.Id).First().Age,
                        ContactNumber = batch.OrderBy(x => x.Id).First().ContactNumber,
                        Nationality = batch.OrderBy(x => x.Id).First().NationalityEntity?.NatName ?? "Unknown"
                    })
                    .ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Reserved guests retrieved successfully",
                    Data = batchLeaders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reserved guests");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving reserved guests",
                    Data = null
                });
            }
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















        // GET: api/v1/guests/reserved-batches
        [HttpGet("reserved-batches")]
        public async Task<IActionResult> GetReservedBatches()
        {
            try
            {
                var reservedGuests = await _context.Guests
                    .Where(g => g.BookingStatus == 2 || g.BookingStatus == 0) // Reserved or Anticipated
                    .OrderByDescending(g => g.Id)
                    .ToListAsync();

                var batchLeaders = reservedGuests
                    .GroupBy(g => g.Batch)
                    .Select(batch => new
                    {
                        Id = batch.OrderBy(x => x.Id).First().Id,
                        Batch = batch.Key,
                        OperatorName = batch.OrderBy(x => x.Id).First().OperatorList?.BusinessName ?? "No Operator",
                        RegistrationDate = batch.OrderBy(x => x.Id).First().DateShort,
                        TotalGuests = batch.Count(),
                        BookingStatus = batch.OrderBy(x => x.Id).First().BookingStatus == 2 ? "reserved" : "anticipated",
                        QrBase64 = GenerateBatchQrCode(batch.Key) // You'll need to implement this
                    })
                    .ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Reserved batches retrieved successfully",
                    Data = batchLeaders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reserved batches");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving reserved batches",
                    Data = null
                });
            }
        }

        // POST: api/v1/guests/confirm-batch
        [HttpPost("confirm-batch")]
        public async Task<IActionResult> ConfirmBatch([FromBody] object request)
        {
            try
            {
                // Parse the request manually without DTOs
                var requestJson = JsonSerializer.Serialize(request);
                var jsonDocument = JsonDocument.Parse(requestJson);

                if (!jsonDocument.RootElement.TryGetProperty("batchCode", out var batchCodeElement) ||
                    string.IsNullOrWhiteSpace(batchCodeElement.GetString()))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Batch code is required",
                        Data = null
                    });
                }

                string batchCode = batchCodeElement.GetString();

                // Find all guests in the batch
                var batchGuests = await _context.Guests
                    .Where(g => g.Batch == batchCode)
                    .ToListAsync();

                if (!batchGuests.Any())
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Batch not found",
                        Data = null
                    });
                }

                // Update booking status for all guests in the batch
                foreach (var guest in batchGuests)
                {
                    guest.BookingStatus = 3; // Assuming 3 = Confirmed/Booked
                    guest.ArrivalDate = DateTime.UtcNow.ToString("yy/mm/dd/tt");
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Batch {BatchCode} confirmed with {GuestCount} guests", batchCode, batchGuests.Count);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Batch confirmed successfully",
                    Data = new
                    {
                        batchCode = batchCode,
                        confirmedGuests = batchGuests.Count,
                        confirmedAt = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming batch");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while confirming batch",
                    Data = null
                });
            }
        }

        // GET: api/v1/guests/batch-details/{guestId}
        [HttpGet("batch-details/{guestId}")]
        public async Task<IActionResult> GetBatchDetails(int guestId)
        {
            try
            {
                var mainGuest = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .FirstOrDefaultAsync(g => g.Id == guestId);

                if (mainGuest == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Guest not found",
                        Data = null
                    });
                }

                var guestsInBatch = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .Where(g => g.Batch == mainGuest.Batch)
                    .OrderBy(g => g.Id)
                    .Select(g => new
                    {
                        Id = g.Id,
                        Fullname = g.Fullname,
                        Age = g.Age,
                        Gender = g.Gender,
                        ContactNumber = g.ContactNumber,
                        Nationality = g.NationalityEntity != null ? g.NationalityEntity.NatName : "Unknown",
                        Operator = g.OperatorList != null ? g.OperatorList.BusinessName : "Unknown",
                        Area = g.Area,
                        Date = g.Date,
                        ArrivalDate = g.ArrivalDate,
                        BookingStatus = g.BookingStatus
                    })
                    .ToListAsync();

                var batchDetails = new
                {
                    MainGuest = new
                    {
                        Id = mainGuest.Id,
                        Fullname = mainGuest.Fullname,
                        Age = mainGuest.Age,
                        Gender = mainGuest.Gender,
                        ContactNumber = mainGuest.ContactNumber,
                        Nationality = mainGuest.NationalityEntity != null ? mainGuest.NationalityEntity.NatName : "Unknown",
                        Operator = mainGuest.OperatorList != null ? mainGuest.OperatorList.BusinessName : "Unknown",
                        Area = mainGuest.Area,
                        Date = mainGuest.Date,
                        ArrivalDate = mainGuest.ArrivalDate,
                        Batch = mainGuest.Batch
                    },
                    GuestsInBatch = guestsInBatch,
                
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Batch details retrieved successfully",
                    Data = batchDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch details for guest {GuestId}", guestId);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving batch details",
                    Data = null
                });
            }
        }

        // GET: api/v1/guests/batch-qr/{batchCode}
        [HttpGet("batch-qr/{batchCode}")]
        public async Task<IActionResult> GetBatchQrCode(string batchCode)
        {
            try
            {
                var batchExists = await _context.Guests.AnyAsync(g => g.Batch == batchCode);
                if (!batchExists)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Batch not found",
                        Data = null
                    });
                }

                // Generate QR code (you'll need to implement this)
                var qrCodeData = GenerateBatchQrCode(batchCode);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "QR code generated successfully",
                    Data = new
                    {
                        batchCode = batchCode,
                        qrBase64 = qrCodeData,
                        generatedAt = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code for batch {BatchCode}", batchCode);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while generating QR code",
                    Data = null
                });
            }
        }

        // Helper method for QR code generation (you need to implement this)
        private string GenerateBatchQrCode(string batchCode)
        {
            // TODO: Implement your QR code generation logic
            // This could use libraries like QRCoder
            // For now, return a placeholder or implement your existing QR generation
            return $"data:image/png;base64,PLACEHOLDER_FOR_QR_CODE_{batchCode}";
        }







        // GET: api/v1/guests/booked-guests
        [HttpGet("booked-guests")]
        public async Task<IActionResult> GetBookedGuests([FromQuery] string startDate, [FromQuery] string endDate)
        {
            try
            {
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Start date and end date are required",
                        Data = null
                    });
                }

                // Parse input dates
                if (!DateTime.TryParse(startDate, out DateTime start) || !DateTime.TryParse(endDate, out DateTime end))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid date format",
                        Data = null
                    });
                }

                // Adjust end date to include the entire day
                end = end.Date.AddDays(1).AddSeconds(-1);

                // Get all guests first, then filter in memory
                var allGuests = await _context.Guests
                    .Where(g => g.ArrivalDate != null)
                    .OrderByDescending(g => g.Id)
                    .ToListAsync();

                // Filter by date range in memory
                var bookedGuests = allGuests
                    .Where(g => DateTime.TryParse(g.ArrivalDate, out DateTime arrival) &&
                               arrival >= start && arrival <= end)
                    .ToList();

                var batchLeaders = bookedGuests
                    .GroupBy(g => g.Batch)
                    .Select(batch => new
                    {
                        Batch = batch.Key,
                        TotalGuests = batch.Count(),
                        OperatorName = batch.OrderBy(x => x.Id).First().OperatorList?.BusinessName ?? "No Operator",
                        ArrivalDate = batch.OrderBy(x => x.Id).First().ArrivalDate,
                        Status = GetStatusText(batch.OrderBy(x => x.Id).First().BookingStatus)
                    })
                    .ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Booked guests retrieved successfully",
                    Data = batchLeaders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booked guests");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving booked guests",
                    Data = null
                });
            }
        }


        private string GetStatusText(int bookingStatus)
        {
            return bookingStatus switch
            {
                0 => "anticipated",
                1 => "cancelled",
                2 => "confirmed",
                3 => "booked",
                _ => "unknown"
            };
        }








        // GET: api/v1/guests/guest-of-the-day
        [HttpGet("guest-of-the-day")]
        public async Task<IActionResult> GetGuestOfTheDay()
        {
            try
            {
                var today = DateTime.Today.ToString("yyyy-MM-dd");

                var guestOfTheDay = await _context.Guests
                    .Where(g => g.ArrivalDate == today)
                    .OrderByDescending(g => g.Id)
                    .FirstOrDefaultAsync();

                if (guestOfTheDay == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "No guest of the day found",
                        Data = null
                    });
                }

                var guestDetails = new
                {
                    Id = guestOfTheDay.Id,
                    Fullname = guestOfTheDay.Fullname,
                    Age = guestOfTheDay.Age,
                    Gender = guestOfTheDay.Gender,
                    ContactNumber = guestOfTheDay.ContactNumber,
                    Nationality = guestOfTheDay.NationalityEntity?.NatName ?? "Unknown",
                    Operator = guestOfTheDay.OperatorList?.BusinessName ?? "Unknown",
                    Area = guestOfTheDay.Area,
                    ArrivalDate = guestOfTheDay.ArrivalDate,
                    Batch = guestOfTheDay.Batch
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Guest of the day retrieved successfully",
                    Data = guestDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving guest of the day");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving guest of the day",
                    Data = null
                });
            }
        }

        // GET: api/v1/guests/batch/{batchCode}
        [HttpGet("batch/{batchCode}")]
        public async Task<IActionResult> GetGuestsByBatch(string batchCode)
        {
            try
            {
                var guestsInBatch = await _context.Guests
                    .Include(g => g.OperatorList)
                    .Include(g => g.NationalityEntity)
                    .Where(g => g.Batch == batchCode)
                    .OrderBy(g => g.Id)
                    .Select(g => new
                    {
                        Id = g.Id,
                        Fullname = g.Fullname,
                        Age = g.Age,
                        Gender = g.Gender,
                        ContactNumber = g.ContactNumber,
                        Nationality = g.NationalityEntity != null ? g.NationalityEntity.NatName : "Unknown",
                        Operator = g.OperatorList != null ? g.OperatorList.BusinessName : "Unknown",
                        Area = g.Area,
                        ArrivalDate = g.ArrivalDate,
                        BookingStatus = g.BookingStatus,
                        Status = "Confirmed" // Always return "Confirmed"
                    })
                    .ToListAsync();

                if (!guestsInBatch.Any())
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Batch not found",
                        Data = null
                    });
                }

                var batchDetails = new
                {
                    BatchCode = batchCode,
                    TotalGuests = guestsInBatch.Count,
                    Guests = guestsInBatch
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Batch details retrieved successfully",
                    Data = batchDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch {BatchCode}", batchCode);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while retrieving batch details",
                    Data = null
                });
            }
        }
    }
}
#endif