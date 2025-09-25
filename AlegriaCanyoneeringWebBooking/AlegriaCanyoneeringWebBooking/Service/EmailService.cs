using MailKit.Net.Smtp;
using MimeKit;
using System.Threading.Tasks;

public class EmailService
{
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Alegria Canyoneering Web Booking", "dariuscumpio@gmail.com")); // Sender email
        message.To.Add(new MailboxAddress("", toEmail)); // Recipient email
        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        using (var client = new SmtpClient())
        {
            await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync("dariuscumpio@gmail.com", "xlbi sbxh tlwb fapj");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
