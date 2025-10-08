using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
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

        public IActionResult Tourist(string filter = "daily", DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            // Set default dates
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;

            ViewBag.Filter = filter;
            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");

            // Get guests and filter by date range
            var guests = _context.Guests
                .Include(g => g.NationalityEntity)
                .ToList()
                .Where(g => DateTime.TryParse(g.Date, out _))
                .Select(g =>
                {
                    g.Date = DateTime.Parse(g.Date).ToString("yyyy-MM-dd");
                    return g;
                })
                .Where(g =>
                {
                    var guestDate = DateTime.Parse(g.Date);
                    return guestDate >= fromDate && guestDate <= toDate;
                })
                .ToList();

            IEnumerable<TourismReportViewModel> report = Enumerable.Empty<TourismReportViewModel>();

            switch (filter.ToLower())
            {
                case "weekly":
                    report = guests
                        .GroupBy(g =>
                        {
                            var date = DateTime.Parse(g.Date);
                            int week = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);
                            return new { Year = date.Year, Week = week };
                        })
                        .Select(g => new TourismReportViewModel
                        {
                            Label = $"Week {g.Key.Week}, {g.Key.Year}",
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                        });
                    break;

                case "monthly":
                    report = guests
                        .GroupBy(g => new
                        {
                            Year = DateTime.Parse(g.Date).Year,
                            Month = DateTime.Parse(g.Date).Month
                        })
                        .Select(g => new TourismReportViewModel
                        {
                            Label = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                        });
                    break;

                case "quarterly":
                    report = guests
                        .GroupBy(g => new
                        {
                            Year = DateTime.Parse(g.Date).Year,
                            Quarter = (DateTime.Parse(g.Date).Month - 1) / 3 + 1,
                            Month = DateTime.Parse(g.Date).Month
                        })
                        .Select(g => new TourismReportViewModel
                        {
                            Label = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                        });
                    break;

                case "yearly":
                    report = guests
                        .GroupBy(g => new
                        {
                            Year = DateTime.Parse(g.Date).Year,
                            Month = DateTime.Parse(g.Date).Month
                        })
                        .Select(g => new TourismReportViewModel
                        {
                            Label = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                        });
                    break;

                default:
                    // DAILY
                    report = guests
                        .GroupBy(g => DateTime.Parse(g.Date))
                        .Select(g => new TourismReportViewModel
                        {
                            Label = g.Key.ToString("MMM dd, yyyy"),
                            ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                            ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                            OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                            OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                            ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                            ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                        });
                    break;
            }

            return View(report.OrderBy(r => r.Label).ToList());
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
    }

}
