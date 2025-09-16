using BCrypt.Net;
using Org.BouncyCastle.Crypto.Generators;

namespace AlegriaCanyoneeringWebBooking.Helpers
{
    public class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
        public static bool VerifyPassword(string enteredPassword, string storedHashedPassword)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHashedPassword);
            }
            catch (SaltParseException ex)
            {
                // Log the error message (if necessary)
                Console.WriteLine($"Error verifying password: {ex.Message}");
                return false; // Return false if there is an issue with the salt
            }
        }
    }
}
