

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AlegriaCanyoneeringWebBooking
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
                    report = guests
                        .GroupBy(g =>
                        {
                            var d = DateTime.Parse(g.Date).Date;
                            int quarter = (d.Month - 1) / 3 + 1;
                            return new { d.Year, Quarter = quarter };
                        })
                        .Select(g =>
                        {
                            int startMonth = (g.Key.Quarter - 1) * 3 + 1;
                            int endMonth = startMonth + 2;
                            var qStart = new DateTime(g.Key.Year, startMonth, 1);
                            var qEnd = new DateTime(g.Key.Year, endMonth,
                                DateTime.DaysInMonth(g.Key.Year, endMonth));
                            return new TourismReportViewModel
                            {
                                Date = qStart,
                                Label = $"Q{g.Key.Quarter} ({qStart:MMMM d} – {qEnd:MMMM d, yyyy})",
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
                    report = guests
                        .GroupBy(g => DateTime.Parse(g.Date).Year)
                        .Select(g =>
                        {
                            int year = g.Key;
                            var yearStart = new DateTime(year, 1, 1);
                            return new TourismReportViewModel
                            {
                                Date = yearStart,
                                Label = $"{year} (Jan 1 – Dec 31)",
                                ThisProvinceMale = g.Count(x => x.NationalityId == 1 && x.Gender == "Male"),
                                ThisProvinceFemale = g.Count(x => x.NationalityId == 1 && x.Gender == "Female"),
                                OtherProvinceMale = g.Count(x => x.NationalityId == 2 && x.Gender == "Male"),
                                OtherProvinceFemale = g.Count(x => x.NationalityId == 2 && x.Gender == "Female"),
                                ForeignMale = g.Count(x => x.NationalityId == 3 && x.Gender == "Male"),
                                ForeignFemale = g.Count(x => x.NationalityId == 3 && x.Gender == "Female")
                            };
                        })
                        .ToList();
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
    }

}
