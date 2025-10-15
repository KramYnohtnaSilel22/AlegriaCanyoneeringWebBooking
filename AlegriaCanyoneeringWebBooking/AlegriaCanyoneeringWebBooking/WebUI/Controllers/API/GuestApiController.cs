using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using AlegriaCanyoneeringWebBooking.Models;
using AlegriaCanyoneeringWebBooking.Domain.Models;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Route("api/guestapi")]
    [ApiController]
    public class GuestApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public GuestApiController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

      
    }
}