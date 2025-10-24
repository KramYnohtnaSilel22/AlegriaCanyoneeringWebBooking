using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Guest(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Calculate difference
            int totalMonths = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month + 1;

            // Determine grouping
            bool groupByMonth = totalMonths > 1; // >1 month -> group by month, else group by day
            ViewBag.GroupByMonth = groupByMonth;

            // Load and filter guests
            var guests = _context.Guests
                .Include(g => g.NationalityEntity)
                .AsEnumerable()
                .Where(g =>
                {
                    if (DateTime.TryParse(g.Date, out var guestDate))
                        return guestDate.Date >= fromDate.Date && guestDate.Date <= toDate.Date;
                    return false;
                })
                .ToList();

            List<TourismReportViewModel> report;

            if (groupByMonth)
            {
                // Group by MONTH - Display as "January 1 – January 31, 2025"
                report = guests
                    .GroupBy(g => new { gYear = DateTime.Parse(g.Date).Year, gMonth = DateTime.Parse(g.Date).Month })
                    .OrderBy(g => g.Key.gYear).ThenBy(g => g.Key.gMonth)
                    .Select(g =>
                    {
                        var firstDay = new DateTime(g.Key.gYear, g.Key.gMonth, 1);
                        var lastDay = new DateTime(g.Key.gYear, g.Key.gMonth, DateTime.DaysInMonth(g.Key.gYear, g.Key.gMonth));

                        return new TourismReportViewModel
                        {
                            Date = firstDay,
                            Label = $"{firstDay:MMMM d} – {lastDay:MMMM d, yyyy}", // Format: January 1 – January 31, 2025
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                        };
                    })
                    .ToList();
            }
            else
            {
                // Group by DAY
                report = guests
                    .GroupBy(g => DateTime.Parse(g.Date).Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new TourismReportViewModel
                    {
                        Date = g.Key,
                        Label = g.Key.ToString("MMMM d, yyyy (ddd)"), // Format: October 23, 2025 (Thu)
                        ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                        ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                        OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                        OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                        ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                        ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                    })
                    .ToList();
            }

            return View(report);
        }

        public IActionResult Nationality(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            // 🗓️ Save to ViewBag for date pickers
            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // 🔹 Load all guests with nationality info
            var allGuests = _context.Guests
                .Include(g => g.NationalityEntity)
                .ToList();

            // 🔹 Filter guests by date range (include all Nationalities, including id = 1)
            var guests = allGuests
                .Where(g =>
                {
                    if (!DateTime.TryParse(g.Date, out var guestDate))
                        return false;

                    return g.NationalityEntity != null &&
                           guestDate.Date >= fromDate.Date &&
                           guestDate.Date <= toDate.Date;
                })
                .ToList();

            // 🔹 Group guests by nationality name (NatName)
            var nationalityReport = guests
                .Where(g => !string.IsNullOrWhiteSpace(g.Gender) && g.NationalityEntity != null)
                .GroupBy(g => g.NationalityEntity.NatName.Trim())
                .Select((g, index) => new
                {
                    Seq = index + 1,
                    NatName = g.Key, // NatName is correct
                    Male = g.Count(x => x.Gender.Trim().ToLower() == "male"),
                    Female = g.Count(x => x.Gender.Trim().ToLower() == "female"),
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            // 🔹 Compute totals
            ViewBag.TotalMale = nationalityReport.Sum(x => x.Male);
            ViewBag.TotalFemale = nationalityReport.Sum(x => x.Female);
            ViewBag.TotalEnding = nationalityReport.Sum(x => x.Total);

            // 🔹 Convert to ViewModel
            var viewModel = nationalityReport.Select(x => new TourismReportViewModel
            {
                Label = x.NatName,
                OtherProvinceMale = x.Male,
                OtherProvinceFemale = x.Female
            }).ToList();

            return View(viewModel);
        }





        public IActionResult Operator(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            // 🗓️ Save to ViewBag for date pickers
            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // 🔹 Load all guests with operator info
            var allGuests = _context.Guests
                .Include(g => g.Operators)
                .ToList();

            // 🔹 Filter guests by date range
            var guests = allGuests
                .Where(g =>
                {
                    if (!DateTime.TryParse(g.Date, out var guestDate))
                        return false;
                    return g.Operators != null &&
                           guestDate.Date >= fromDate.Date &&
                           guestDate.Date <= toDate.Date;
                })
                .ToList();

            // 🔹 Group guests by operator business name
            var operatorReport = guests
                .Where(g => !string.IsNullOrWhiteSpace(g.Gender) && g.Operators != null)
                .GroupBy(g => g.Operators.BusinessName.Trim())
                .Select((g, index) => new
                {
                    Seq = index + 1,
                    BusinessName = g.Key,
                    Male = g.Count(x => x.Gender.Trim().ToLower() == "male"),
                    Female = g.Count(x => x.Gender.Trim().ToLower() == "female"),
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            // 🔹 Compute totals
            ViewBag.TotalMale = operatorReport.Sum(x => x.Male);
            ViewBag.TotalFemale = operatorReport.Sum(x => x.Female);
            ViewBag.TotalEnding = operatorReport.Sum(x => x.Total);

            // 🔹 Convert to ViewModel
            var viewModel = operatorReport.Select(x => new TourismReportViewModel
            {
                Label = x.BusinessName,
                OtherProvinceMale = x.Male,
                OtherProvinceFemale = x.Female
            }).ToList();

            return View(viewModel);
        }
    }
}