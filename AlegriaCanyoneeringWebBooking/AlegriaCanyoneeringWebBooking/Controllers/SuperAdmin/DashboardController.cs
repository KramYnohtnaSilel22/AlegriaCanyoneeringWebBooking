using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var today = DateTime.Today;

            // Guests Today
            ViewBag.GuestsToday = _context.Guests
                .AsEnumerable()
                .Count(g => !string.IsNullOrEmpty(g.ArrivalDate) &&
                            DateTime.Parse(g.ArrivalDate).Date == today);

            // Guests This Month
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            ViewBag.GuestsThisMonth = _context.Guests
                .AsEnumerable()
                .Count(g => !string.IsNullOrEmpty(g.ArrivalDate) &&
                            DateTime.Parse(g.ArrivalDate) >= firstDayOfMonth);

            // Guests Previous Month
            var prevMonth = today.AddMonths(-1);
            var firstDayPrevMonth = new DateTime(prevMonth.Year, prevMonth.Month, 1);
            var lastDayPrevMonth = firstDayOfMonth.AddDays(-1);
            ViewBag.GuestsPrevMonth = _context.Guests
                .AsEnumerable()
                .Count(g => !string.IsNullOrEmpty(g.ArrivalDate) &&
                            DateTime.Parse(g.ArrivalDate) >= firstDayPrevMonth &&
                            DateTime.Parse(g.ArrivalDate) <= lastDayPrevMonth);

            // Monthly Guests for this year
            ViewBag.MonthLabels = Enumerable.Range(1, 12)
                .Select(m => new DateTime(today.Year, m, 1).ToString("MMM"))
                .ToList();

            ViewBag.MonthlyGuestCounts = Enumerable.Range(1, 12)
                .Select(m =>
                {
                    var firstDay = new DateTime(today.Year, m, 1);
                    var lastDay = firstDay.AddMonths(1).AddDays(-1);
                    return _context.Guests
                        .AsEnumerable()
                        .Count(g => !string.IsNullOrEmpty(g.ArrivalDate) &&
                                    DateTime.Parse(g.ArrivalDate).Date >= firstDay &&
                                    DateTime.Parse(g.ArrivalDate).Date <= lastDay);
                })
                .ToList();

            // Yearly Guests (last 5 years)
            int yearsToShow = 5;
            ViewBag.YearLabels = Enumerable.Range(today.Year - yearsToShow + 1, yearsToShow)
                .Select(y => y.ToString())
                .ToList();

            ViewBag.YearlyGuestCounts = Enumerable.Range(today.Year - yearsToShow + 1, yearsToShow)
                .Select(y =>
                {
                    var firstDay = new DateTime(y, 1, 1);
                    var lastDay = new DateTime(y, 12, 31);
                    return _context.Guests
                        .AsEnumerable()
                        .Count(g => !string.IsNullOrEmpty(g.ArrivalDate) &&
                                    DateTime.Parse(g.ArrivalDate).Date >= firstDay &&
                                    DateTime.Parse(g.ArrivalDate).Date <= lastDay);
                })
                .ToList();

            return View();
        }

    }
}
