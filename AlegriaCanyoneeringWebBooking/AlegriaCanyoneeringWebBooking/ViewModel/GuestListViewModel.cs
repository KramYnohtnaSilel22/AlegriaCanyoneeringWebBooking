using AlegriaCanyoneeringWebBooking.Models;
using System.ComponentModel.DataAnnotations;

namespace AlegriaCanyoneeringWebBooking.ViewModel
{
    public class GuestListViewModel
    {
        public Guest NewGuest { get; set; }  // required properties can stay
        public List<Guest> ReservedGuests { get; set; } = new List<Guest>();
    }
}
