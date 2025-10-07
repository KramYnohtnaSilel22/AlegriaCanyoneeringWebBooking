using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
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
        public IActionResult Nationality(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = dateFrom ?? DateTime.Today.AddDays(-30);
            var to = dateTo ?? DateTime.Today;
            ViewBag.DateFrom = from.ToString("yyyy-MM-dd");
            ViewBag.DateTo = to.ToString("yyyy-MM-dd");

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

            // Group by nationality and calculate male/female counts
            var grouped = filtered
                .GroupBy(x => x.Nationality)
                .Select(g => new AlegriaCanyoneeringWebBooking.ViewModel.TourismReportViewModel
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



        private bool TryParseAndCheckDate(string dateString, DateTime from, DateTime to)
        {
            var formats = new[]
            {
            "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MMM dd, yyyy", "MMMM dd, yyyy"
        };
            if (DateTime.TryParseExact(dateString, formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate.Date >= from.Date && parsedDate.Date <= to.Date;
            }
            return false;
        }
    }

}
