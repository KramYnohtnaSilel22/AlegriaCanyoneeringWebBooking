using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace AlegriaCanyoneeringWebBooking.Service
{
    public class GuestService : IGuestService
    {
        private readonly ApplicationDbContext _context;

        public GuestService(ApplicationDbContext context)
        {
            _context = context;
        }
        public Guest GetGuestOfTheDay()
        {
            var today = DateTime.Today;

            return _context.Guests
                .Include(g => g.OperatorList)         // 👈 include Operator
                .Include(g => g.NationalityEntity)    // 👈 include Nationality
                .AsEnumerable()
                .FirstOrDefault(g =>
                    DateTime.TryParse(g.ArrivalDate, out var arrivalDate) &&
                    arrivalDate.Date == today
                );

        }


    }
}
