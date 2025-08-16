using Members.Models;
using Members.Services; // Add this using statement
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Members.Controllers
{
    public class InfoController(EmailService emailService) : Controller
    {
        private readonly EmailService _emailService = emailService;

        public IActionResult Index()
        {
            string siteName = Environment.GetEnvironmentVariable("SITE_NAME_FISH_SMART") ?? "Fish-Smart";

            // Set the default view name and message
            ViewBag.Message = "Home";
            ViewData["ViewName"] = siteName;
            return View();
        }

        //public IActionResult About()
        //{
        //    ViewBag.Message = "About Us";
        //    ViewData["ViewName"] = "About Us";
        //    return View();
        //}

        [HttpPost]
        public async Task<IActionResult> SendEmail(string Name, string Email, string Subject, string Message, string Comment)
        {
            if (!string.IsNullOrEmpty(Comment))
            {
                // Likely a bot, ignore.
                return View("Index");
            }
            try
            {
                string siteEmail = Environment.GetEnvironmentVariable("SMTP_USERNAME_FISH_SMART") ?? string.Empty;
                string siteName = Environment.GetEnvironmentVariable("SITE_NAME_FISH_SMART") ?? "Fish-Smart";

                // Create properly formatted HTML email body like other system emails
                string htmlBody = "<!DOCTYPE html>" +
                                  "<html lang=\"en\">" +
                                  "<head>" +
                                  "<meta charset=\"utf-8\">" +
                                  "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                                  "<title>Contact Form Submission</title>" +
                                  "</head>" +
                                  "<body style=\"font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;\">" +
                                  $"<div style=\"background: #f8f9fa; padding: 20px; border-radius: 10px; margin-bottom: 20px;\">" +
                                  $"<h2 style=\"color: #2c5aa0; margin: 0 0 20px 0;\">📧 Contact Form Submission</h2>" +
                                  "</div>" +
                                  "<div style=\"background: white; padding: 20px; border: 1px solid #dee2e6; border-radius: 5px;\">" +
                                  $"<p><strong>From:</strong> {Name}</p>" +
                                  $"<p><strong>Email:</strong> <a href=\"mailto:{Email}\">{Email}</a></p>" +
                                  $"<p><strong>Subject:</strong> {Subject}</p>" +
                                  $"<div style=\"margin-top: 20px;\"><strong>Message:</strong></div>" +
                                  $"<div style=\"background: #f8f9fa; padding: 15px; border-radius: 5px; margin-top: 10px; white-space: pre-wrap;\">{Message}</div>" +
                                  "</div>" +
                                  $"<div style=\"text-align: center; margin-top: 20px; padding: 10px; font-size: 12px; color: #6c757d;\">" +
                                  $"<p>This message was sent via the {siteName} contact form.</p>" +
                                  $"<p>To reply, respond directly to: <a href=\"mailto:{Email}\">{Email}</a></p>" +
                                  "</div>" +
                                  "</body>" +
                                  "</html>";

                // Use EmailService to send the email with Reply-To header
                await _emailService.SendEmailAsync(
                    siteEmail, // To address
                    $"Contact Form: {Subject}", // Subject
                    htmlBody, // Properly formatted HTML body
                    Email // Reply-To address
                );

                ViewBag.Message = "Your email has been sent successfully!";
                return View("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"There was an error sending your message: {ex.Message}";
                return View("Index");
            }
        }

        public IActionResult Contact()
        {
            ViewBag.Message = "Contact Us";
            ViewData["ViewName"] = ViewBag.Message;
            return View();
        }

        public IActionResult TOS()
        {
            ViewBag.Message = "TOS";
            ViewData["ViewName"] = ViewBag.Message;
            return View();
        }

        public IActionResult Privacy()
        {
            ViewBag.Message = "Privacy";
            ViewData["ViewName"] = ViewBag.Message;
            return View();
        }

        //public IActionResult Facilities()
        //{
        //    ViewBag.Message = "Facilities";
        //    ViewData["ViewName"] = ViewBag.Message;
        //    return View();
        //}

        //public IActionResult MoreLinks()
        //{
        //    ViewBag.Message = "More Links";
        //    ViewData["ViewName"] = ViewBag.Message;
        //    return View();
        //}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}