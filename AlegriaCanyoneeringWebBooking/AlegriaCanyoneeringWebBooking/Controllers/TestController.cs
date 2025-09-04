using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Linq;

using AlegriaCanyoneeringWebBooking.Models;


namespace AlegriaCanyoneeringWebBooking.Controllers
{
    public class TestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TestController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            try
            {
                // Test database connection
                var canConnect = _context.Database.CanConnect();
                var operatorsCount = _context.Operators.Count();
                var bookingsCount = _context.Guests.Count();

                ViewBag.ConnectionStatus = canConnect ? "Connected" : "Disconnected";
                ViewBag.OperatorsCount = operatorsCount;
                ViewBag.BookingsCount = bookingsCount;
                ViewBag.DatabaseName = _context.Database.GetDbConnection().Database;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }
    }
}