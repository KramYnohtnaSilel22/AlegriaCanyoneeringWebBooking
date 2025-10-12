

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
            DateTime from, to;

            // Handle automatic date range depending on filter
            switch (filter.ToLower())
            {
                case "daily":
                    from = dateFrom ?? DateTime.Today;
                    to = dateTo ?? DateTime.Today;
                    break;


                case "weekly":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                    from = dateFrom ?? startOfWeek;
                    to = dateTo ?? startOfWeek.AddDays(6);
                    break;

                case "monthly":
                    from = dateFrom ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    to = dateTo ?? from.AddMonths(1).AddDays(-1);
                    break;

                case "quarterly":
                    int currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                    from = dateFrom ?? new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                    to = dateTo ?? from.AddMonths(3).AddDays(-1);
                    break;

                case "yearly":
                    from = dateFrom ?? new DateTime(DateTime.Today.Year, 1, 1);
                    to = dateTo ?? new DateTime(DateTime.Today.Year, 12, 31);
                    break;

                default:
                    from = dateFrom ?? DateTime.Today;
                    to = dateTo ?? DateTime.Today;
                    break;
            }

            ViewBag.DateFrom = from.ToString("yyyy-MM-dd");
            ViewBag.DateTo = to.ToString("yyyy-MM-dd");
            ViewBag.Filter = filter;

            // Load all guests with Nationality
            var guests = _context.Guests.Include(g => g.NationalityEntity).ToList();

            // Filter by date and exclude "Within Cebu Province" (Id = 1)
            var filtered = guests
                .Where(g => !string.IsNullOrWhiteSpace(g.Date)
                            && DateTime.TryParse(g.Date, out var parsedDate)
                            && parsedDate.Date >= from.Date
                            && parsedDate.Date <= to.Date
                            && g.NationalityEntity != null
                            && g.NationalityEntity.id != 1) // exclude Cebu
                .Select(g => new
                {
                    Nationality = g.NationalityEntity.NatName,
                    Gender = (g.Gender ?? "").Trim().ToLowerInvariant()
                })
                .ToList();

            // Group by nationality and count male/female
            var grouped = filtered
                .GroupBy(x => x.Nationality)
                .Select(g => new TourismReportViewModel
                {
                    Label = g.Key,
                    OtherProvinceMale = g.Count(x => x.Gender == "male"),
                    OtherProvinceFemale = g.Count(x => x.Gender == "female")
                })
                .OrderByDescending(x => x.OtherProvinceMale + x.OtherProvinceFemale)
                .ToList();

            // Totals
            ViewBag.TotalMale = grouped.Sum(x => x.OtherProvinceMale);
            ViewBag.TotalFemale = grouped.Sum(x => x.OtherProvinceFemale);
            ViewBag.TotalEnding = grouped.Sum(x => x.OtherProvinceMale + x.OtherProvinceFemale);

            return View(grouped);
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
