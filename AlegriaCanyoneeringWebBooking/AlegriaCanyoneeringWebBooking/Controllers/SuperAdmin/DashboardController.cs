using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin,Operator")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Fetch all guests and their ArrivalDate stored as Unix timestamps
                var guests = await _context.Guests
                    .Where(g => !string.IsNullOrEmpty(g.ArrivalDate)) // Ensure ArrivalDate is not null or empty
                    .Select(g => new
                    {
                        g.ArrivalDate // We'll pass this directly to the view
                    })
                    .ToListAsync();

                // Get stats for today, this month, and previous month
                var guestsToday = guests.Count(g => ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Date == DateTime.Today);
                var guestsThisMonth = guests.Count(g => ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Month == DateTime.Now.Month && ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Year == DateTime.Now.Year);
                var guestsPrevMonth = guests.Count(g => ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Month == DateTime.Now.AddMonths(-1).Month && ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Year == DateTime.Now.AddMonths(-1).Year);

                // Fetch monthly guest counts for the current year
                var monthlyGuestCounts = new List<int>();
                var monthLabels = new List<string>();
                for (int i = 1; i <= 12; i++)
                {
                    monthLabels.Add(DateTimeFormatInfo.CurrentInfo.GetAbbreviatedMonthName(i));
                    monthlyGuestCounts.Add(guests.Count(g => ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Month == i && ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Year == DateTime.Now.Year));
                }

                // Fetch yearly guest counts for the last 5 years
                var yearlyGuestCounts = new List<int>();
                var yearLabels = new List<string>();
                for (int i = 0; i < 5; i++)
                {
                    var year = DateTime.Now.AddYears(-i).Year;
                    yearLabels.Add(year.ToString());
                    yearlyGuestCounts.Add(guests.Count(g => ConvertUnixToDateTime(long.Parse(g.ArrivalDate)).Year == year));
                }

                // Pass all data to the view
                ViewBag.GuestsToday = guestsToday;
                ViewBag.GuestsThisMonth = guestsThisMonth;
                ViewBag.GuestsPrevMonth = guestsPrevMonth;
                ViewBag.MonthLabels = monthLabels;
                ViewBag.MonthlyGuestCounts = monthlyGuestCounts;
                ViewBag.YearLabels = yearLabels;
                ViewBag.YearlyGuestCounts = yearlyGuestCounts;
                ViewBag.GuestDates = guests.Select(g => g.ArrivalDate).ToList(); // Pass Unix timestamps to the view

                return View();
            }
            catch (Exception ex)
            {
                // Handle error and provide feedback
                Console.WriteLine($"Error in DashboardController: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading dashboard data. Please try again later.";
                return View();
            }
        }

        // Helper method to convert Unix timestamp to DateTime (if needed in other places)
        private DateTime ConvertUnixToDateTime(long unixTimestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
        }
    }
}
