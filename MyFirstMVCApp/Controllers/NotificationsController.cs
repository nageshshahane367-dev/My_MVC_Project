
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstMVCApp.Models;
using System.Security.Claims;

namespace MyFirstMVCApp.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var email = GetCurrentEmail();

            if (string.IsNullOrWhiteSpace(email))
            {
                return Forbid();
            }

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientEmail == email)
                .OrderByDescending(n => n.CreatedAtUtc)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var email = GetCurrentEmail();

            if (string.IsNullOrWhiteSpace(email))
            {
                return Forbid();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id && n.RecipientEmail == email);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var email = GetCurrentEmail();

            if (string.IsNullOrWhiteSpace(email))
            {
                return Forbid();
            }

            var unreadNotifications = await _context.Notifications
                .Where(n =>
                    n.RecipientEmail == email && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
