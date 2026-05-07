using AlegriaCanyoneeringWebBooking.WebUI.ViewModel;
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


            // Store formatted dates for the View
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

        private static readonly List<string> AttendanceAreas = new()
        {
            "Wonder Falls",
            "Kanlaob",
            "Kawasan Exit"
        };

        // =========================================================
        // DRIVER ATTENDANCE REPORT
        // GET /Reports/DriverAttendance?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=
        // ✅ Joins DriverAttendance.DriverId → Driver.RefId → Driver name
        // ✅ Joins Driver.DriverId → BatchAssignment → Guest.Area (Route)
        // ✅ Time extracted from unix timestamp
        // ✅ Date column in driver_attendance = Unix timestamp string
        // ✅ area filter: Wonder Falls | Kanlaob | Kawasan Exit
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DriverAttendance(string? dateFrom, string? dateTo, string? area)
        {
            dateFrom ??= DateTime.Today.ToString("yyyy-MM-dd");
            dateTo ??= DateTime.Today.ToString("yyyy-MM-dd");
            area ??= "";

            DateTime fromDt = DateTime.Parse(dateFrom).Date;
            DateTime toDt = DateTime.Parse(dateTo).Date.AddDays(1);

            long fromUnix = new DateTimeOffset(fromDt, TimeSpan.Zero).ToUnixTimeSeconds();
            long toUnix = new DateTimeOffset(toDt, TimeSpan.Zero).ToUnixTimeSeconds();

            string fromStr = fromUnix.ToString();
            string toStr = toUnix.ToString();

            // ✅ Load attendance in range
            var attendance = await _context.DriverAttendances
                .Where(a => string.Compare(a.Date, fromStr) >= 0
                         && string.Compare(a.Date, toStr) < 0)
                .ToListAsync();

            // ✅ Load all drivers: RefId string → (DriverId int, fullName)
            var drivers = await _context.Drivers
                .Select(d => new
                {
                    d.DriverId,
                    d.RefId,
                    fullName = ((d.FName ?? "") + " " + (d.MName ?? "") + " " + (d.LName ?? "")).Trim()
                })
                .ToListAsync();

            var driverByRefId = drivers.ToDictionary(d => d.RefId, d => d);

            // ✅ Load BatchAssignments → map DriverId(int) → list of BatchCodes
            var batchAssignments = await _context.BatchAssignments
                .Where(b => b.DriverId != null)
                .Select(b => new { b.DriverId, b.BatchCode })
                .ToListAsync();

            var batchesByDriverId = batchAssignments
                .Where(b => b.DriverId.HasValue)
                .GroupBy(b => b.DriverId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.BatchCode).Distinct().ToList());

            // ✅ Load Guests: Batch → Area (Route)
            var guests = await _context.Guests
                .Where(g => g.Batch != null && g.Area != null)
                .Select(g => new { g.Batch, g.Area })
                .ToListAsync();

            var areaByBatch = guests
                .GroupBy(g => g.Batch!)
                .ToDictionary(g => g.Key, g => g.First().Area ?? "");

            // ✅ Build report rows
            var report = attendance
                .OrderBy(a => a.Date)
                .Select(a =>
                {
                    string displayDate = "";
                    string displayTime = "";

                    if (long.TryParse(a.Date, out long unixTs))
                    {
                        var dto = DateTimeOffset.FromUnixTimeSeconds(unixTs).ToLocalTime();
                        displayDate = dto.ToString("MMMM d, yyyy");
                        displayTime = dto.ToString("hh:mm tt");
                    }

                    // Resolve driver name
                    string driverName = a.DriverId;
                    int? driverIntId = null;
                    if (driverByRefId.ContainsKey(a.DriverId))
                    {
                        var d = driverByRefId[a.DriverId];
                        driverName = d.fullName;
                        driverIntId = d.DriverId;
                    }

                    // Resolve Route via BatchAssignment → Guest.Area
                    string route = "";
                    if (driverIntId.HasValue && batchesByDriverId.ContainsKey(driverIntId.Value))
                    {
                        foreach (var batch in batchesByDriverId[driverIntId.Value])
                        {
                            if (batch != null && areaByBatch.ContainsKey(batch))
                            {
                                route = areaByBatch[batch];
                                break;
                            }
                        }
                    }

                    return new DriverAttendanceReportViewModel
                    {
                        DriverName = driverName,
                        RefId = a.DriverId,
                        Date = displayDate,
                        Time = displayTime,
                        Passenger = a.Passenger,
                        Route = route
                    };
                })
                .ToList();

            // ✅ Filter by area if selected
            if (!string.IsNullOrEmpty(area))
            {
                report = report
                    .Where(r => r.Route.Equals(area, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SelectedArea = area;
            ViewBag.Areas = AttendanceAreas;
            ViewBag.TotalPassenger = report.Sum(r => r.Passenger);
            ViewBag.TotalRecords = report.Count;

            return View(report);
        }


        // =========================================================
        // GUIDE ATTENDANCE REPORT
        // GET /Reports/GuideAttendance?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=
        // ✅ Joins TourGuideAttendance.TGId → Guide.Rfid → Guide name
        // ✅ Date column in tourguide_attendance = Unix timestamp string
        // ✅ Guest count from TourGuideDtr.NoOfGuest (Date = yyyyMMdd long)
        // ✅ Route via BatchAssignment → Guest.Area (TGId → Guide.Rfid → batch)
        // ✅ area filter: Wonder Falls | Kanlaob | Kawasan Exit
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GuideAttendance(string? dateFrom, string? dateTo, string? area)
        {
            dateFrom ??= DateTime.Today.ToString("yyyy-MM-dd");
            dateTo ??= DateTime.Today.ToString("yyyy-MM-dd");
            area ??= "";

            DateTime fromDt = DateTime.Parse(dateFrom).Date;
            DateTime toDt = DateTime.Parse(dateTo).Date.AddDays(1);

            long fromUnix = new DateTimeOffset(fromDt, TimeSpan.Zero).ToUnixTimeSeconds();
            long toUnix = new DateTimeOffset(toDt, TimeSpan.Zero).ToUnixTimeSeconds();

            string fromStr = fromUnix.ToString();
            string toStr = toUnix.ToString();

            // ✅ Load attendance in range (Date = unix string)
            var attendance = await _context.TourGuideAttendances
                .Where(a => string.Compare(a.Date, fromStr) >= 0
                         && string.Compare(a.Date, toStr) < 0)
                .ToListAsync();

            // ✅ Build yyyyMMdd range for TourGuideDtr lookup
            long dtrFrom = long.Parse(fromDt.ToString("yyyyMMdd"));
            long dtrTo = long.Parse(toDt.AddDays(-1).ToString("yyyyMMdd"));

            // ✅ Guest count from TourGuideDtr (Date = yyyyMMdd long)
            var guideDtrs = await _context.TourGuideDtrs
                .Where(d => d.Date >= dtrFrom && d.Date <= dtrTo)
                .ToListAsync();

            var dtrGuestMap = guideDtrs
                .GroupBy(d => d.Rfid)
                .ToDictionary(g => g.Key, g => g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0));

            // ✅ Load all guides for name + Rfid lookup
            var guides = await _context.Guides
                .Select(g => new
                {
                    g.GuideId,
                    g.Rfid,
                    fullName = ((g.FName ?? "") + " " + (g.MName ?? "") + " " + (g.LName ?? "")).Trim()
                })
                .ToListAsync();

            var guideNameMap = guides.ToDictionary(g => g.Rfid, g => g.fullName);
            var guideIdMap = guides.ToDictionary(g => g.Rfid, g => g.GuideId);

            // ✅ Load BatchAssignments → GuideId → BatchCodes
            var batchAssignments = await _context.BatchAssignments
                .Where(b => b.GuideId != null)
                .Select(b => new { b.GuideId, b.BatchCode })
                .ToListAsync();

            var batchesByGuideId = batchAssignments
                .Where(b => b.GuideId.HasValue)
                .GroupBy(b => b.GuideId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.BatchCode).Distinct().ToList());

            // ✅ Load Guests: Batch → Area (Route)
            var guests = await _context.Guests
                .Where(g => g.Batch != null && g.Area != null)
                .Select(g => new { g.Batch, g.Area })
                .ToListAsync();

            var areaByBatch = guests
                .GroupBy(g => g.Batch!)
                .ToDictionary(g => g.Key, g => g.First().Area ?? "");

            // ✅ Build report rows
            var report = attendance
                .OrderBy(a => a.Date)
                .Select(a =>
                {
                    string displayDate = "";
                    string displayTime = "";

                    if (long.TryParse(a.Date, out long unixTs))
                    {
                        var dto = DateTimeOffset.FromUnixTimeSeconds(unixTs).ToLocalTime();
                        displayDate = dto.ToString("MMMM d, yyyy");
                        displayTime = dto.ToString("hh:mm tt");
                    }

                    // Guest count
                    int guests2 = 0;
                    if (long.TryParse(a.TGId, out long rfidLong) && dtrGuestMap.ContainsKey(rfidLong))
                        guests2 = dtrGuestMap[rfidLong];

                    // Route via Guide.GuideId → BatchAssignment → Guest.Area
                    string route = "";
                    if (guideIdMap.ContainsKey(a.TGId))
                    {
                        int guideIntId = guideIdMap[a.TGId];
                        if (batchesByGuideId.ContainsKey(guideIntId))
                        {
                            foreach (var batch in batchesByGuideId[guideIntId])
                            {
                                if (batch != null && areaByBatch.ContainsKey(batch))
                                {
                                    route = areaByBatch[batch];
                                    break;
                                }
                            }
                        }
                    }

                    return new GuideAttendanceReportViewModel
                    {
                        GuideName = guideNameMap.ContainsKey(a.TGId) ? guideNameMap[a.TGId] : a.TGId,
                        Rfid = a.TGId,
                        Date = displayDate,
                        Time = displayTime,
                        Guests = guests2,
                        Route = route
                    };
                })
                .ToList();

            // ✅ Filter by area if selected
            if (!string.IsNullOrEmpty(area))
            {
                report = report
                    .Where(r => r.Route.Equals(area, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Period display
            DateTime parsedFrom = DateTime.Parse(dateFrom);
            DateTime parsedTo = DateTime.Parse(dateTo);
            string period = parsedFrom == parsedTo
                ? parsedFrom.ToString("MMMM d, yyyy").ToUpper()
                : parsedFrom.ToString("MMM d").ToUpper() + " - " + parsedTo.ToString("MMM d, yyyy").ToUpper();

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Period = period;
            ViewBag.SelectedArea = area;
            ViewBag.Areas = GuideAreas;
            ViewBag.TotalGuests = report.Sum(r => r.Guests);
            ViewBag.TotalRecords = report.Count;

            return View(report);
        }



        // =========================================================
        // DRIVER DTR REPORT — PAYROLL FORMAT
        // GET /Reports/DriverDtr?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=Wonderfalls
        // ✅ Grouped by driver — TotalPassenger × 100 = GrossSalary = NetPay
        // ✅ DriverDtr.Rfid (int) matched to Driver.RefId (string)
        // ✅ DriverDtr.Date = unix timestamp string
        // ✅ area filter: Wonderfalls | Canlaob Exit | Kawasan Exit
        // =========================================================
        private static readonly List<string> DriverAreas = new()
        {
            "Wonder Falls",
            "Kanlaob",
            "Kawasan Exit"
        };

        [HttpGet]
        public async Task<IActionResult> DriverDtr(string? dateFrom, string? dateTo, string? area)
        {
            dateFrom ??= DateTime.Today.ToString("yyyy-MM-dd");
            dateTo ??= DateTime.Today.ToString("yyyy-MM-dd");
            area ??= "";

            DateTime fromDt = DateTime.Parse(dateFrom).Date;
            DateTime toDt = DateTime.Parse(dateTo).Date.AddDays(1);

            long fromUnix = new DateTimeOffset(fromDt, TimeSpan.Zero).ToUnixTimeSeconds();
            long toUnix = new DateTimeOffset(toDt, TimeSpan.Zero).ToUnixTimeSeconds();

            string fromStr = fromUnix.ToString();
            string toStr = toUnix.ToString();

            // ✅ Load DTR records in date range
            var dtrs = await _context.DriverDtrs
                .Where(d => string.Compare(d.Date, fromStr) >= 0
                         && string.Compare(d.Date, toStr) < 0)
                .ToListAsync();

            // ✅ Load all drivers — build Rfid(int) → name map
            var drivers = await _context.Drivers
                .Select(d => new
                {
                    d.RefId,
                    fullName = ((d.LName ?? "").ToUpper() + ", " + (d.FName ?? "").ToUpper()
                               + (!string.IsNullOrWhiteSpace(d.MName) ? " " + d.MName.ToUpper() : "")).Trim()
                })
                .ToListAsync();

            var rfidNameMap = drivers
                .Where(d => int.TryParse(d.RefId, out _))
                .ToDictionary(d => int.Parse(d.RefId), d => d.fullName);

            // ✅ Build rfid → area map via BatchAssignment → Guest.Area
            //    Used to filter DTR rows by selected area
            var driversList = await _context.Drivers
                .Select(d => new { d.DriverId, d.RefId })
                .ToListAsync();

            var driverIdByRefId = driversList
                .Where(d => int.TryParse(d.RefId, out _))
                .ToDictionary(d => int.Parse(d.RefId), d => d.DriverId);

            var batchAssignments = await _context.BatchAssignments
                .Where(b => b.DriverId != null)
                .Select(b => new { b.DriverId, b.BatchCode })
                .ToListAsync();

            var batchesByDriver = batchAssignments
                .Where(b => b.DriverId.HasValue)
                .GroupBy(b => b.DriverId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.BatchCode).ToHashSet());

            var guestAreas = await _context.Guests
                .Where(g => g.Batch != null && g.Area != null)
                .Select(g => new { g.Batch, g.Area })
                .ToListAsync();

            var areaByBatch = guestAreas
                .GroupBy(g => g.Batch!)
                .ToDictionary(g => g.Key, g => g.First().Area ?? "");

            // Build rfid(int) → area string
            var rfidAreaMap = new Dictionary<int, string>();
            foreach (var rfidKv in driverIdByRefId)
            {
                int rfid = rfidKv.Key;
                int driverId = rfidKv.Value;
                if (!batchesByDriver.ContainsKey(driverId)) continue;
                foreach (var batch in batchesByDriver[driverId])
                {
                    if (batch != null && areaByBatch.ContainsKey(batch))
                    {
                        rfidAreaMap[rfid] = areaByBatch[batch];
                        break;
                    }
                }
            }

            // ✅ Filter DTR rows by area if selected
            var filteredDtrs = string.IsNullOrEmpty(area)
                ? dtrs
                : dtrs.Where(d =>
                    rfidAreaMap.ContainsKey(d.Rfid) &&
                    rfidAreaMap[d.Rfid].Equals(area, StringComparison.OrdinalIgnoreCase)
                  ).ToList();

            const int RatePerDay = 100;

            // ✅ Group by Rfid → TripCount + TotalPassenger + compute salary
            var report = filteredDtrs
                .GroupBy(d => d.Rfid)
                .Select(g =>
                {
                    int totalPax = g.Sum(x => int.TryParse(x.Passenger, out int p) ? p : 0);
                    decimal gross = totalPax * RatePerDay;
                    return new DriverDtrReportViewModel
                    {
                        RefId = g.Key.ToString(),
                        DriverName = rfidNameMap.ContainsKey(g.Key) ? rfidNameMap[g.Key] : $"Rfid: {g.Key}",
                        Address = "Alegria, Cebu",
                        Designation = "Driver",
                        TripCount = g.Count(),
                        TotalPassenger = totalPax,
                        RatePerDay = RatePerDay,
                        GrossSalary = gross,
                        NetPay = gross,        // Net = Gross (no deductions)
                        PlaceOfIssue = "Alegria, Cebu"
                    };
                })
                .OrderBy(r => r.DriverName)
                .ToList();

            // ✅ Period display
            DateTime parsedFrom = DateTime.Parse(dateFrom);
            DateTime parsedTo = DateTime.Parse(dateTo);
            string period = parsedFrom.ToString("MMMM dd").ToUpper() + " - " + parsedTo.ToString("dd, yyyy").ToUpper();
            if (parsedFrom.Month != parsedTo.Month || parsedFrom.Year != parsedTo.Year)
                period = parsedFrom.ToString("MMMM dd").ToUpper() + " - " + parsedTo.ToString("MMMM dd, yyyy").ToUpper();

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Period = period;
            ViewBag.SelectedArea = area;
            ViewBag.Areas = DriverAreas;
            ViewBag.TotalDrivers = report.Count;
            ViewBag.TotalTrips = report.Sum(r => r.TripCount);
            ViewBag.TotalPassenger = report.Sum(r => r.TotalPassenger);
            ViewBag.TotalGross = report.Sum(r => r.GrossSalary);
            ViewBag.TotalNetPay = report.Sum(r => r.NetPay);

            return View(report);
        }


        // =========================================================
        // GUIDE DTR REPORT — PAYROLL FORMAT
        // GET /Reports/GuideDtr?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=
        // ✅ Grouped by guide — TripCount (jumps) × rate = GrossSalary = NetPay
        // ✅ TourGuideDtr.Rfid (long) matched to Guide.Rfid (string)
        // ✅ TourGuideDtr.Date = yyyyMMdd as long (NOT unix)
        // ✅ Rates: Wonder Falls=500 | Kawasan Exit=600 | Kanlaob=0
        // ✅ area filter: Wonder Falls | Kanlaob | Kawasan Exit
        // =========================================================
        private static readonly List<string> GuideAreas = new()
        {
            "Wonder Falls",
            "Kanlaob",
            "Kawasan Exit"
        };

        private static int GetGuideRate(string area) => area switch
        {
            "Wonder Falls" => 500,
            "Kawasan Exit" => 600,
            "Kanlaob" => 500,
            _ => 0
        };

        [HttpGet]
        public async Task<IActionResult> GuideDtr(string? dateFrom, string? dateTo, string? area)
        {
            dateFrom ??= DateTime.Today.ToString("yyyy-MM-dd");
            dateTo ??= DateTime.Today.ToString("yyyy-MM-dd");
            area ??= "";

            // ✅ Convert yyyy-MM-dd → yyyyMMdd long for TourGuideDtr.Date
            DateTime.TryParse(dateFrom, out var fromDt);
            DateTime.TryParse(dateTo, out var toDt);
            long dtrFrom = long.Parse(fromDt.ToString("yyyyMMdd"));
            long dtrTo = long.Parse(toDt.ToString("yyyyMMdd"));

            // ✅ Load DTR records in date range (inclusive)
            var dtrs = await _context.TourGuideDtrs
                .Where(d => d.Date >= dtrFrom && d.Date <= dtrTo)
                .ToListAsync();

            // ✅ Load all guides — build Rfid(long) → (GuideId, name)
            var guides = await _context.Guides
                .Select(g => new
                {
                    g.GuideId,
                    g.Rfid,
                    fullName = ((g.LName ?? "").ToUpper() + ", " + (g.FName ?? "").ToUpper()
                               + (!string.IsNullOrWhiteSpace(g.MName) ? " " + g.MName.ToUpper() : "")).Trim()
                })
                .ToListAsync();

            var rfidNameMap = guides
                .Where(g => long.TryParse(g.Rfid, out _))
                .ToDictionary(g => long.Parse(g.Rfid), g => g.fullName);

            var rfidGuideIdMap = guides
                .Where(g => long.TryParse(g.Rfid, out _))
                .ToDictionary(g => long.Parse(g.Rfid), g => g.GuideId);

            // ✅ Load BatchAssignments → GuideId → BatchCodes
            var batchAssignments = await _context.BatchAssignments
                .Where(b => b.GuideId != null)
                .Select(b => new { b.GuideId, b.BatchCode })
                .ToListAsync();

            var batchesByGuideId = batchAssignments
                .Where(b => b.GuideId.HasValue)
                .GroupBy(b => b.GuideId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.BatchCode).Distinct().ToList());

            // ✅ Load Guests: Batch → Area
            var guests = await _context.Guests
                .Where(g => g.Batch != null && g.Area != null)
                .Select(g => new { g.Batch, g.Area })
                .ToListAsync();

            var areaByBatch = guests
                .GroupBy(g => g.Batch!)
                .ToDictionary(g => g.Key, g => g.First().Area ?? "");

            // Build rfid(long) → area
            var rfidAreaMap = new Dictionary<long, string>();
            foreach (var kv in rfidGuideIdMap)
            {
                long rfid = kv.Key;
                int guideId = kv.Value;
                if (!batchesByGuideId.ContainsKey(guideId)) continue;
                foreach (var batch in batchesByGuideId[guideId])
                {
                    if (batch != null && areaByBatch.ContainsKey(batch))
                    {
                        rfidAreaMap[rfid] = areaByBatch[batch];
                        break;
                    }
                }
            }

            // ✅ Group by Rfid → TripCount + TotalGuests + area + salary
            var report = dtrs
                .GroupBy(d => d.Rfid)
                .Select(g =>
                {
                    string guideArea = rfidAreaMap.ContainsKey(g.Key) ? rfidAreaMap[g.Key] : "";
                    int rate = GetGuideRate(guideArea);
                    int jumps = g.Count();                   // Number of jumps = trips
                    int totalPax = g.Sum(x => int.TryParse(x.NoOfGuest, out int p) ? p : 0);
                    decimal gross = totalPax * rate;             // Gross = guests × rate (matches Driver DTR logic)

                    return new GuideDtrReportViewModel
                    {
                        Rfid = g.Key.ToString(),
                        GuideName = rfidNameMap.ContainsKey(g.Key) ? rfidNameMap[g.Key] : $"Rfid: {g.Key}",
                        Address = "Alegria, Cebu",
                        Designation = "Guide",
                        TripCount = totalPax,              // Show guest count (like Driver DTR shows passengers)
                        TotalGuests = totalPax,
                        RatePerJump = rate,
                        GrossSalary = gross,
                        NetPay = gross,
                        PlaceOfIssue = "Alegria, Cebu",
                        Area = guideArea
                    };
                })
                .ToList();

            // ✅ Filter by area if selected
            if (!string.IsNullOrEmpty(area))
                report = report
                    .Where(r => r.Area.Equals(area, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            report = report.OrderBy(r => r.GuideName).ToList();

            // Period display
            string period = fromDt.ToString("MMMM dd").ToUpper() + " - " + toDt.ToString("dd, yyyy").ToUpper();
            if (fromDt.Month != toDt.Month || fromDt.Year != toDt.Year)
                period = fromDt.ToString("MMMM dd").ToUpper() + " - " + toDt.ToString("MMMM dd, yyyy").ToUpper();

            // Rate label for header
            string rateLabel = string.IsNullOrEmpty(area)
                ? "Varies by Area"
                : $"₱{GetGuideRate(area):N0} / Jump";

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Period = period;
            ViewBag.SelectedArea = area;
            ViewBag.Areas = GuideAreas;
            ViewBag.RateLabel = rateLabel;
            ViewBag.TotalGuides = report.Count;
            ViewBag.TotalTrips = report.Sum(r => r.TripCount);
            ViewBag.TotalGuests = report.Sum(r => r.TotalGuests);
            ViewBag.TotalGross = report.Sum(r => r.GrossSalary);
            ViewBag.TotalNetPay = report.Sum(r => r.NetPay);

            return View(report);
        }

        private static readonly List<string> GuestAreas = new()
        {
            "Wonder Falls",
            "Kanlaob",
            "Kawasan Exit"
        };

        // =========================================================
        // GUEST VISITOR REPORT
        // GET /Reports/Guest?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=
        // ✅ Filters by Guest.Area when area is selected
        // ✅ Groups by day (≤1 month) or by month (>1 month)
        // =========================================================
        public IActionResult Area(DateTime? dateFrom = null, DateTime? dateTo = null, string? area = null)
        {
            var fromDate = dateFrom ?? DateTime.Today;
            var toDate = dateTo ?? DateTime.Today;
            area ??= "";

            ViewBag.DateFrom = fromDate.ToString("yyyy-MM-dd");
            ViewBag.DateTo = toDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedArea = area;
            ViewBag.Areas = GuestAreas;

            // Calculate difference
            int totalMonths = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month + 1;
            bool groupByMonth = totalMonths > 1;
            ViewBag.GroupByMonth = groupByMonth;

            // Load and filter guests by date range
            var allGuests = _context.Guests
                .Include(g => g.NationalityEntity)
                .AsEnumerable()
                .Where(g =>
                {
                    if (DateTime.TryParse(g.Date, out var guestDate))
                        return guestDate.Date >= fromDate.Date && guestDate.Date <= toDate.Date;
                    return false;
                });

            // ✅ Filter by area if selected
            if (!string.IsNullOrEmpty(area))
            {
                allGuests = allGuests.Where(g =>
                    !string.IsNullOrEmpty(g.Area) &&
                    g.Area.Equals(area, StringComparison.OrdinalIgnoreCase));
            }

            var guests = allGuests.ToList();

            List<TourismReportViewModel> report;

            if (groupByMonth)
            {
                // Group by MONTH
                report = guests
                    .GroupBy(g => new { gYear = DateTime.Parse(g.Date).Year, gMonth = DateTime.Parse(g.Date).Month })
                    .OrderBy(g => g.Key.gYear).ThenBy(g => g.Key.gMonth)
                    .Select(g =>
                    {
                        var firstDay = new DateTime(g.Key.gYear, g.Key.gMonth, 1);
                        var lastDay = new DateTime(g.Key.gYear, g.Key.gMonth,
                                            DateTime.DaysInMonth(g.Key.gYear, g.Key.gMonth));

                        return new TourismReportViewModel
                        {
                            Date = firstDay,
                            Label = $"{firstDay:MMMM d} – {lastDay:MMMM d, yyyy}",
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
                        Label = g.Key.ToString("MMMM d, yyyy (ddd)"),
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
        
        
        // =========================================================
        // OUTSIDE GUIDE DTR REPORT — uses tourguide_priority
        // GET /Reports/OutsideGuideDtr?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&area=
        // ✅ TourGuidePriority.GuideIdPrior (int) = RFID → OutsideGuide.Rfid (string)
        // ✅ TourGuidePriority.Date = Unix timestamp (long stored as string)
        // ✅ TourGuidePriority.NoOfGuest (int)
        // ✅ Area from Guest.Area via Guest.Batch → BatchAssignments
        // =========================================================

        private static readonly List<string> OutsideGuideAreas = new()
        {
            "Wonder Falls",
            "Kanlaob",
            "Kawasan Exit"
        };

        private static int GetOutsideGuideRate(string area) => area switch
        {
            "Wonder Falls" => 500,
            "Kawasan Exit" => 600,
            "Kanlaob" => 500,
            _ => 0
        };

        // Unix timestamp helpers
        private static long ToUnixDay(DateTime dt) =>
            new DateTimeOffset(dt.Date, TimeSpan.Zero).ToUnixTimeSeconds();

        private static long ToUnixDayEnd(DateTime dt) =>
            new DateTimeOffset(dt.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero).ToUnixTimeSeconds();

        [HttpGet]
        public async Task<IActionResult> OutsideGuideDtr(
         string? dateFrom, string? dateTo, string? area)
        {
            dateFrom ??= DateTime.Today.ToString("yyyy-MM-dd");
            dateTo ??= DateTime.Today.ToString("yyyy-MM-dd");
            area ??= "";

            DateTime.TryParse(dateFrom, out var fromDt);
            DateTime.TryParse(dateTo, out var toDt);

            long unixFrom = ToUnixDay(fromDt);
            long unixTo = ToUnixDayEnd(toDt);

            System.Diagnostics.Debug.WriteLine("============================================");
            System.Diagnostics.Debug.WriteLine($"[OutsideGuideDtr] Date Range : {dateFrom} → {dateTo}");
            System.Diagnostics.Debug.WriteLine($"[OutsideGuideDtr] Unix Range : {unixFrom} → {unixTo}");
            System.Diagnostics.Debug.WriteLine("============================================");

            // ── 1. Load & filter TourGuidePriorities ──────────────────────────
            var allPriorities = await _context.TourGuidePriorities.ToListAsync();
            System.Diagnostics.Debug.WriteLine($"[1] Total TourGuidePriorities in DB: {allPriorities.Count}");

            var priorities = allPriorities
                .Where(p => p.Date != null
                         && long.TryParse(p.Date, out long ts)
                         && ts >= unixFrom
                         && ts <= unixTo)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[1] Priorities in date range: {priorities.Count}");
            foreach (var p in priorities.Take(5))
                System.Diagnostics.Debug.WriteLine($"      GuideIdPrior={p.GuideIdPrior}, Date={p.Date}, Guests={p.NoOfGuest}");

            // ── 2. Load outside guides keyed by RFID (int) ────────────────────
            var outsideGuides = await _context.OutsideGuides
                .Select(g => new
                {
                    g.OutsideGuideId,
                    g.Rfid,
                    g.Nickname,
                    g.Address,
                    FullName = ((g.LName ?? "").ToUpper() + ", "
                               + (g.FName ?? "").ToUpper()
                               + (!string.IsNullOrWhiteSpace(g.MName)
                                   ? " " + g.MName!.ToUpper() : "")).Trim()
                })
                .ToListAsync();

            var rfidGuideMap = outsideGuides
                .Where(g => int.TryParse(g.Rfid, out _))
                .ToDictionary(g => int.Parse(g.Rfid), g => g);

            System.Diagnostics.Debug.WriteLine($"[2] OutsideGuides with numeric RFID: {rfidGuideMap.Count}");
            foreach (var kv in rfidGuideMap.Take(5))
                System.Diagnostics.Debug.WriteLine($"      RFID={kv.Key} → {kv.Value.FullName}");

            // ── 3. Build day-bucket → Area map from Guest.ArrivalDate ─────────
            //
            //  ROOT CAUSE OF MISSING AREA:
            //  The old code used BatchAssignments.GuideId which stores LOCAL guide IDs,
            //  NOT OutsideGuideId — so TryGetValue always failed and Area stayed "".
            //
            //  FIX: TourGuidePriority.Date and Guest.ArrivalDate are both unix
            //  timestamps. We normalise both to the same UTC start-of-day bucket
            //  (floor to 86400 seconds) then join to get Guest.Area for that day.

            var guests = await _context.Guests
                .Where(g => g.ArrivalDate != null && g.Area != null && g.Area != "")
                .Select(g => new { g.ArrivalDate, g.Area })
                .ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[3] Guests with ArrivalDate+Area: {guests.Count}");

            // Helper: floor unix timestamp string to UTC start-of-day
            static long DayBucket(string? unixStr) =>
                long.TryParse(unixStr, out long ts) ? (ts / 86400L) * 86400L : -1L;

            // day-bucket → most common area on that day
            var areaByDayBucket = guests
                .GroupBy(g => DayBucket(g.ArrivalDate))
                .Where(grp => grp.Key >= 0)
                .ToDictionary(
                    grp => grp.Key,
                    grp => grp
                        .GroupBy(x => x.Area!)
                        .OrderByDescending(x => x.Count())
                        .First().Key
                );

            System.Diagnostics.Debug.WriteLine($"[3] Day buckets with area: {areaByDayBucket.Count}");
            foreach (var kv in areaByDayBucket.Take(5))
                System.Diagnostics.Debug.WriteLine(
                    $"      Bucket={kv.Key} ({DateTimeOffset.FromUnixTimeSeconds(kv.Key):yyyy-MM-dd}) → Area={kv.Value}");

            // Diagnostics: show what bucket each priority row resolves to
            System.Diagnostics.Debug.WriteLine("[3] Priority → bucket → area:");
            foreach (var p in priorities.Take(5))
            {
                var bucket = DayBucket(p.Date);
                var found = areaByDayBucket.TryGetValue(bucket, out var a);
                System.Diagnostics.Debug.WriteLine(
                    $"      PriorityDate={p.Date} bucket={bucket} → {(found ? a : "NOT FOUND — no Guest on this day with Area set")}");
            }

            // ── 4. Build report grouped by RFID ───────────────────────────────
            var validRfids = new HashSet<int>(rfidGuideMap.Keys);

            var report = priorities
                .Where(p => validRfids.Contains(p.GuideIdPrior))
                .GroupBy(p => p.GuideIdPrior)
                .Select(g =>
                {
                    var info = rfidGuideMap.TryGetValue(g.Key, out var gi) ? gi : null;

                    // Resolve area per row, pick most frequent across date range
                    var resolvedAreas = g
                        .Select(p =>
                        {
                            var bucket = DayBucket(p.Date);
                            return areaByDayBucket.TryGetValue(bucket, out var a) ? a : "";
                        })
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToList();

                    string guideArea = resolvedAreas
                        .GroupBy(a => a)
                        .OrderByDescending(x => x.Count())
                        .Select(x => x.Key)
                        .FirstOrDefault() ?? "";

                    System.Diagnostics.Debug.WriteLine(
                        $"[4] RFID={g.Key} Name={info?.FullName} → Area='{guideArea}' (resolved {resolvedAreas.Count}/{g.Count()} rows)");

                    int rate = GetOutsideGuideRate(guideArea);
                    int totalPax = g.Sum(p => p.NoOfGuest);
                    decimal gross = totalPax * rate;

                    return new OutsideGuideDtrReportViewModel
                    {
                        Rfid = g.Key.ToString(),
                        GuideName = info?.FullName ?? $"RFID: {g.Key}",
                        Nickname = info?.Nickname,
                        Address = info?.Address ?? "Alegria, Cebu",
                        Designation = "Outside Guide",
                        TripCount = totalPax,
                        TotalGuests = totalPax,
                        RatePerJump = rate,
                        GrossSalary = gross,
                        NetPay = gross,
                        Area = guideArea,
                        PlaceOfIssue = "Alegria, Cebu"
                    };
                })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[4] Final report rows (before area filter): {report.Count}");
            foreach (var r in report)
                System.Diagnostics.Debug.WriteLine(
                    $"      {r.GuideName} | Area={r.Area} | Rate={r.RatePerJump} | Gross={r.GrossSalary} | Net={r.NetPay}");

            // ── 5. Optional area filter ────────────────────────────────────────
            if (!string.IsNullOrEmpty(area))
                report = report
                    .Where(r => r.Area.Equals(area, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            report = report.OrderBy(r => r.GuideName).ToList();

            // ── 6. Period label + ViewBag ──────────────────────────────────────
            string period = fromDt.Month == toDt.Month && fromDt.Year == toDt.Year
                ? fromDt.ToString("MMMM dd").ToUpper() + " - " + toDt.ToString("dd, yyyy").ToUpper()
                : fromDt.ToString("MMMM dd").ToUpper() + " - " + toDt.ToString("MMMM dd, yyyy").ToUpper();

            string rateLabel = string.IsNullOrEmpty(area)
                ? "Varies by Area"
                : $"₱{GetOutsideGuideRate(area):N0} / Jump";

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Period = period;
            ViewBag.SelectedArea = area;
            ViewBag.Areas = OutsideGuideAreas;
            ViewBag.RateLabel = rateLabel;
            ViewBag.TotalGuides = report.Count;
            ViewBag.TotalTrips = report.Sum(r => r.TripCount);
            ViewBag.TotalGuests = report.Sum(r => r.TotalGuests);
            ViewBag.TotalGross = report.Sum(r => r.GrossSalary);
            ViewBag.TotalNetPay = report.Sum(r => r.NetPay);

            return View(report);
        }
    }
}