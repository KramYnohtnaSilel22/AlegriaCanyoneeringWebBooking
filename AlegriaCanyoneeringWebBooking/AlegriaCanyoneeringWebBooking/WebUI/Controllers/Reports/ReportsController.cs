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

        public IActionResult Nationality(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Calculate difference in months
            int monthsDifference = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month;
            bool groupByMonth = monthsDifference >= 2; // 2 or more months -> group by month

            // Load all guests with nationality info
            var allGuests = _context.Guests
                .Include(g => g.NationalityEntity)
                .ToList();

            // Filter by date range and exclude Cebu (id = 1)
            var guests = allGuests
                .Where(g => g.NationalityEntity != null
                            && g.NationalityEntity.id != 1
                            && DateTime.TryParse(g.Date, out var guestDate)
                            && guestDate.Date >= fromDate.Date
                            && guestDate.Date <= toDate.Date)
                .ToList();

            List<TourismReportViewModel> report;

            if (groupByMonth)
            {
                // Group by MONTH but still display day/month/year for clarity
                report = guests
                    .GroupBy(g =>
                    {
                        var dt = DateTime.Parse(g.Date);
                        return new { dt.Year, dt.Month };
                    })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month)
                    .Select(g =>
                    {
                        // Find earliest day in the month to show in Label
                        var earliestDate = g.Min(x => DateTime.Parse(x.Date));
                        return new TourismReportViewModel
                        {
                            Label = earliestDate.ToString("MMMM d, yyyy"), // Always month-day-year
                            ThisProvinceMale = 0, // No Cebu data
                            ThisProvinceFemale = 0,
                            OtherProvinceMale = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "male"),
                            OtherProvinceFemale = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "female"),
                            ForeignMale = 0,
                            ForeignFemale = 0
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
                        Label = g.Key.ToString("MMMM d, yyyy"),
                        ThisProvinceMale = 0,
                        ThisProvinceFemale = 0,
                        OtherProvinceMale = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "male"),
                        OtherProvinceFemale = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "female"),
                        ForeignMale = 0,
                        ForeignFemale = 0
                    })
                    .ToList();
            }

            // Totals
            ViewBag.TotalMale = report.Sum(x => x.TotalMale);
            ViewBag.TotalFemale = report.Sum(x => x.TotalFemale);
            ViewBag.TotalEnding = report.Sum(x => x.GrandTotal);
            ViewBag.GroupByMonth = groupByMonth;

            return View(report);
        }


        public IActionResult Operator(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Load all guests with operator information
            var allGuests = _context.Guests
                .Include(g => g.Operators)
                .AsEnumerable()
                .ToList();

            // Filter by date range
            var guests = allGuests
                .Where(g =>
                {
                    try
                    {
                        var guestDate = DateTime.Parse(g.Date);
                        return guestDate.Date >= fromDate.Date && guestDate.Date <= toDate.Date;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            // Group by operator
            var operatorReport = guests
                .Where(g => g.OperatorId.HasValue && g.Operators != null)
                .GroupBy(g => new { g.OperatorId, g.Operators.BusinessName })
                .Select((g, index) => new
                {
                    Seq = index + 1,
                    OperatorName = g.Key.BusinessName,
                    Male = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "male"),
                    Female = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "female"),
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            // Calculate totals
            ViewBag.TotalMale = operatorReport.Sum(x => x.Male);
            ViewBag.TotalFemale = operatorReport.Sum(x => x.Female);
            ViewBag.GrandTotal = operatorReport.Sum(x => x.Total);

            // Convert to ViewModel for consistency
            var viewModel = operatorReport.Select(x => new TourismReportViewModel
            {
                Label = x.OperatorName,
                ThisProvinceMale = x.Male,
                ThisProvinceFemale = x.Female,
                OtherProvinceMale = x.Total
            }).ToList();

            return View(viewModel);
        }
    }
}