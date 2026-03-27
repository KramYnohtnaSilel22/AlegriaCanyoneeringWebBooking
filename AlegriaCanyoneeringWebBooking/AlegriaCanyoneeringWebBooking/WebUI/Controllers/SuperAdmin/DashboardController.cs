using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin,Admin,Operator,Staff")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        // ── Philippine Time Zone ─────────────────────────────────────────────────────
        // Keeps counts correct regardless of where the server is hosted (UTC, etc.)
        private static readonly TimeZoneInfo PhilippineTime =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Manila");

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // ── Pull ALL guests — no status filter ───────────────────────────────
                // We only fetch the 3 date columns needed for counting.
                var rawGuests = await _context.Guests
                    .Select(g => new
                    {
                        g.ArrivalDate,  // Primary  : Unix timestamp string
                        g.DateShort,    // Fallback1: "MM/DD/YYYY"
                        g.Date          // Fallback2: free-form date string
                    })
                    .ToListAsync();

                // ── Resolve each guest to a nullable PH-local DateOnly ───────────────
                var guestDates = rawGuests
                    .Select(g => ResolveArrivalDate(g.ArrivalDate, g.DateShort, g.Date))
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();

                // ── "Today" in Philippine time ────────────────────────────────────────
                var nowPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PhilippineTime);
                var todayPh = DateOnly.FromDateTime(nowPh);
                var thisYearPh = todayPh.Year;
                var thisMonthPh = todayPh.Month;
                var prevMonthPh = todayPh.AddMonths(-1);

                // ── Stat cards ────────────────────────────────────────────────────────
                var guestsToday = guestDates.Count(d => d == todayPh);
                var guestsThisMonth = guestDates.Count(d => d.Year == thisYearPh && d.Month == thisMonthPh);
                var guestsPrevMonth = guestDates.Count(d => d.Year == prevMonthPh.Year && d.Month == prevMonthPh.Month);

                // ── Monthly breakdown (current year) ──────────────────────────────────
                var monthLabels = new List<string>();
                var monthlyGuestCounts = new List<int>();
                for (int m = 1; m <= 12; m++)
                {
                    monthLabels.Add(DateTimeFormatInfo.CurrentInfo.GetAbbreviatedMonthName(m));
                    monthlyGuestCounts.Add(guestDates.Count(d => d.Year == thisYearPh && d.Month == m));
                }

                // ── Old year breakdown (2018–2020) ────────────────────────────────────
                var oldYearLabels = new List<string> { "2018", "2019", "2020" };
                var oldYearlyGuestCounts = oldYearLabels
                    .Select(y => guestDates.Count(d => d.Year == int.Parse(y)))
                    .ToList();

                // ── Last 5 years (newest first → reversed in view) ────────────────────
                var yearLabels = new List<string>();
                var yearlyGuestCounts = new List<int>();
                for (int i = 0; i < 5; i++)
                {
                    int yr = thisYearPh - i;
                    yearLabels.Add(yr.ToString());
                    yearlyGuestCounts.Add(guestDates.Count(d => d.Year == yr));
                }

                // ── ViewBag ───────────────────────────────────────────────────────────
                ViewBag.GuestsToday = guestsToday;
                ViewBag.GuestsThisMonth = guestsThisMonth;
                ViewBag.GuestsPrevMonth = guestsPrevMonth;
                ViewBag.MonthLabels = monthLabels;
                ViewBag.MonthlyGuestCounts = monthlyGuestCounts;
                ViewBag.YearLabelsOld = oldYearLabels;
                ViewBag.YearlyGuestCountsOld = oldYearlyGuestCounts;
                ViewBag.YearLabels = yearLabels;
                ViewBag.YearlyGuestCounts = yearlyGuestCounts;
                ViewBag.GuestDates = rawGuests
                    .Where(g => !string.IsNullOrWhiteSpace(g.ArrivalDate))
                    .Select(g => g.ArrivalDate)
                    .ToList();

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DashboardController] {ex.Message}\n{ex.StackTrace}");
                ViewBag.ErrorMessage = "An error occurred while loading dashboard data. Please try again later.";
                ViewBag.GuestsToday = 0;
                ViewBag.GuestsThisMonth = 0;
                ViewBag.GuestsPrevMonth = 0;
                ViewBag.MonthLabels = new List<string>();
                ViewBag.MonthlyGuestCounts = new List<int>();
                ViewBag.YearLabelsOld = new List<string>();
                ViewBag.YearlyGuestCountsOld = new List<int>();
                ViewBag.YearLabels = new List<string>();
                ViewBag.YearlyGuestCounts = new List<int>();
                ViewBag.GuestDates = new List<string>();
                return View();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a guest's arrival date to a PH-local DateOnly.
        /// Priority: Unix ArrivalDate → DateShort → Date → null (row skipped)
        /// </summary>
        private DateOnly? ResolveArrivalDate(string? arrivalDate, string? dateShort, string? date)
        {
            // 1. Unix timestamp (ArrivalDate column)
            if (!string.IsNullOrWhiteSpace(arrivalDate) &&
                long.TryParse(arrivalDate.Trim(), out long unix))
            {
                try
                {
                    var utcDt = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                    var localDt = TimeZoneInfo.ConvertTimeFromUtc(utcDt, PhilippineTime);
                    return DateOnly.FromDateTime(localDt);
                }
                catch { /* fall through */ }
            }

            // 2. DateShort  e.g. "03/27/2026"
            if (!string.IsNullOrWhiteSpace(dateShort) &&
                DateTime.TryParse(dateShort.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime ds))
            {
                return DateOnly.FromDateTime(ds);
            }

            // 3. Generic Date field
            if (!string.IsNullOrWhiteSpace(date) &&
                DateTime.TryParse(date.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime d))
            {
                return DateOnly.FromDateTime(d);
            }

            return null;
        }
    }
}