using System.Security.Cryptography;
using System.Text;

namespace AlegriaCanyoneeringWebBooking.Helpers;

public static class PasswordHelper
{
    // ── Hash a plain-text password using SHA1 ──────────────────
    public static string HashPassword(string password)
    {
        using SHA1 sha1 = SHA1.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        byte[] hash  = sha1.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    // ── Verify plain-text against a stored SHA1 hash ───────────
    public static bool VerifyPassword(string enteredPassword, string storedHashedPassword)
    {
        if (string.IsNullOrEmpty(storedHashedPassword))
            return false;

        string sha1Hash = HashPassword(enteredPassword);
        return string.Equals(sha1Hash, storedHashedPassword, StringComparison.OrdinalIgnoreCase);
    }

    // ── Check if a value looks like a SHA1 hash (40 hex chars) ─
    public static bool IsHashed(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Length == 40 && value.All(c => Uri.IsHexDigit(c));
    }
}