

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AlegriaCanyoneeringWebBooking.Controllers 
{
    [Authorize(Roles = "Super Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Guest(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Load all guests first
            var allGuests = _context.Guests
                 .Include(g => g.NationalityEntity)
                 .AsEnumerable()
                 .ToList();

            // Filter by parsing the date and comparing
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

            IEnumerable<TourismReportViewModel> report = Enumerable.Empty<TourismReportViewModel>();

            switch (filter.ToLower())
            {
                case "weekly":
                    report = guests
                        .GroupBy(g => DateTime.Parse(g.Date).Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new TourismReportViewModel
                        {
                            Date = g.Key,
                            Label = g.Key.ToString("MMMM d, yyyy"),
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                        })
                        .ToList();
                    break;

                case "monthly":
                    report = guests
                        .GroupBy(g => DateTime.Parse(g.Date).Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new TourismReportViewModel
                        {
                            Date = g.Key,
                            Label = g.Key.ToString("MMMM d, yyyy"),
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                        })
                        .ToList();
                    break;

                case "quarterly":
                    // Group by year and month to show individual months
                    report = guests
                        .GroupBy(g =>
                        {
                            var d = DateTime.Parse(g.Date).Date;
                            return new { d.Year, d.Month };
                        })
                        .OrderBy(g => g.Key.Year)
                        .ThenBy(g => g.Key.Month)
                        .Select(g =>
                        {
                            var monthStart = new DateTime(g.Key.Year, g.Key.Month, 1);
                            var monthEnd = new DateTime(g.Key.Year, g.Key.Month, DateTime.DaysInMonth(g.Key.Year, g.Key.Month));
                            return new TourismReportViewModel
                            {
                                Date = monthStart,
                                Label = $"{monthStart:MMMM 1} – {monthEnd:MMMM d, yyyy}",
                                ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                                ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                                OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                                OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                                ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                                ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                            };
                        })
                        .ToList();
                    break;

                case "yearly":
                    // Show all 12 months of the year
                    var yearToDisplay = guests.Count > 0
                        ? DateTime.Parse(guests.First().Date).Year
                        : DateTime.Now.Year;

                    var allMonthsOfYear = new List<TourismReportViewModel>();
                    for (int month = 1; month <= 12; month++)
                    {
                        var monthStart = new DateTime(yearToDisplay, month, 1);
                        var monthEnd = new DateTime(yearToDisplay, month, DateTime.DaysInMonth(yearToDisplay, month));

                        var monthGuests = guests.Where(g =>
                        {
                            var gDate = DateTime.Parse(g.Date).Date;
                            return gDate.Year == yearToDisplay && gDate.Month == month;
                        }).ToList();

                        allMonthsOfYear.Add(new TourismReportViewModel
                        {
                            Date = monthStart,
                            Label = $"{monthStart:MMMM 1} – {monthEnd:MMMM d, yyyy}",
                            ThisProvinceMale = monthGuests.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = monthGuests.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = monthGuests.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = monthGuests.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = monthGuests.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                            ForeignFemale = monthGuests.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                        });
                    }

                    report = allMonthsOfYear;
                    break;

                default:
                    // daily
                    report = guests
                        .GroupBy(g => DateTime.Parse(g.Date).Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new TourismReportViewModel
                        {
                            Date = g.Key,
                            Label = g.Key.ToString("MMMM d, yyyy"),
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId > 2 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId > 2 && x.Gender == "Female")
                        })
                        .ToList();
                    break;
            }

            return View(report.OrderBy(r => r.Date).ToList());
        }


        public IActionResult Nationality(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            // 🔹 Determine date range based on filter (SAME AS OPERATOR)
            switch (filter.ToLower())
            {
                case "daily":
                    fromDate = dateFrom ?? DateTime.Today;
                    toDate = dateTo ?? DateTime.Today;
                    break;
                case "weekly":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                    fromDate = dateFrom ?? startOfWeek;
                    toDate = dateTo ?? startOfWeek.AddDays(6);
                    break;
                case "monthly":
                    fromDate = dateFrom ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    toDate = dateTo ?? fromDate.AddMonths(1).AddDays(-1);
                    break;
                case "quarterly":
                    int currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                    fromDate = dateFrom ?? new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                    toDate = dateTo ?? fromDate.AddMonths(3).AddDays(-1);
                    break;
                case "yearly":
                    fromDate = dateFrom ?? new DateTime(DateTime.Today.Year, 1, 1);
                    toDate = dateTo ?? new DateTime(DateTime.Today.Year, 12, 31);
                    break;
                default:
                    fromDate = dateFrom ?? DateTime.Today;
                    toDate = dateTo ?? DateTime.Today;
                    break;
            }

            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // 🔹 Load all guests with nationality information
            var allGuests = _context.Guests
                .Include(g => g.NationalityEntity)
                .ToList();

            // 🔹 Filter by date range and exclude Cebu (id = 1)
            var guests = allGuests
                .Where(g => g.NationalityEntity != null
                            && g.NationalityEntity.id != 1
                            && DateTime.TryParse(g.Date, out var guestDate)
                            && guestDate.Date >= fromDate.Date
                            && guestDate.Date <= toDate.Date)
                .ToList();

            // 🔹 Group by nationality
            var nationalityReport = guests
                .Where(g => !string.IsNullOrWhiteSpace(g.Gender))
                .GroupBy(g => g.NationalityEntity.NatName.Trim())
                .Select((g, index) => new
                {
                    Seq = index + 1,
                    NationalityName = g.Key,
                    Male = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "male"),
                    Female = g.Count(x => x.Gender != null && x.Gender.Trim().ToLower() == "female"),
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            // 🔹 Calculate totals
            ViewBag.TotalMale = nationalityReport.Sum(x => x.Male);
            ViewBag.TotalFemale = nationalityReport.Sum(x => x.Female);
            ViewBag.TotalEnding = nationalityReport.Sum(x => x.Total);

            // 🔹 Convert to ViewModel
            var viewModel = nationalityReport.Select(x => new TourismReportViewModel
            {
                Label = x.NationalityName,
                OtherProvinceMale = x.Male,
                OtherProvinceFemale = x.Female
            }).ToList();

            return View(viewModel);
        }


        public IActionResult Operator(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Load all guests with operator information
            var allGuests = _context.Guests
                .Include(g => g.OperatorList)
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
                .Where(g => g.OperatorId.HasValue && g.OperatorList != null)
                .GroupBy(g => new { g.OperatorId, g.OperatorList.BusinessName })
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
