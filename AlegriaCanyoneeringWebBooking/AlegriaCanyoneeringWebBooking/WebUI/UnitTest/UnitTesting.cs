using Xunit;
using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Mvc;
using Mysqlx.Crud;
using AlegriaCanyoneeringWebBooking.Controllers;
namespace AlegriaCanyoneeringWebBooking.UnitTest
{
    public class UnitTesting
    {
        [Fact]
        public void Index_ReturnsViewResult()
        {


       
                // Arrange  
                var logger = new LoggerFactory().CreateLogger<HomeController>();
                var controller = new HomeController(logger);

                // Act  
                var result = controller.Index();

                Xunit.Assert.IsType<ViewResult>(result); // ✅ Explicitly call xUnit Assert
            }

        }
    }

