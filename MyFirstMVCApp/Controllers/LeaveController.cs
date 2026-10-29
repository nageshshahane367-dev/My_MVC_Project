
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstMVCApp.Dto;
using MyFirstMVCApp.Models;

namespace MyFirstMVCApp.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly AppDbContext _context;

        public LeaveController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Leave
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
                return Forbid();

            var employee = await GetEmployeeForUserAsync(currentUser);

            if (employee == null)
            {
                return NotFound(
                    "No employee profile is linked to your account. " +
                    $"Logged-in username: {currentUser.Username}. " +
                    "Please ensure an employee record exists with the same email.");
            }

            var requests = await _context.LeaveRequests
                .Where(r => r.EmployeeId == employee.Id)
                .OrderByDescending(r => r.AppliedOnUtc)
                .ToListAsync();

            int year = DateTime.UtcNow.Year;

            ViewBag.LeaveBalances = await _context.LeaveBalances
                .Where(b =>
                    b.EmployeeId == employee.Id &&
                    b.Year == year)
                .OrderBy(b => b.LeaveType)
                .ToListAsync();

            ViewBag.CurrentYear = year;

            return View(requests);
        }

        // GET: /Leave/Apply
        [HttpGet]
        public IActionResult Apply()
        {
            return View(new LeaveRequestDto());
        }

        // POST: /Leave/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(LeaveRequestDto dto)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
                return Forbid();

            var employee = await GetEmployeeForUserAsync(currentUser);

            if (employee == null)
            {
                return NotFound(
                    "No employee profile is linked to your account. " +
                    $"Logged-in username: {currentUser.Username}. " +
                    "Please ensure an employee record exists with the same email.");
            }

            // Validate requested dates.
            if (dto.StartDate == default)
            {
                ModelState.AddModelError(
                    nameof(dto.StartDate),
                    "Please select a valid start date.");
            }

            if (dto.EndDate == default)
            {
                ModelState.AddModelError(
                    nameof(dto.EndDate),
                    "Please select a valid end date.");
            }

            if (dto.StartDate != default &&
                dto.EndDate != default)
            {
                if (dto.StartDate.Date < DateTime.Today)
                {
                    ModelState.AddModelError(
                        nameof(dto.StartDate),
                        "Leave cannot start in the past.");
                }

                if (dto.EndDate.Date < dto.StartDate.Date)
                {
                    ModelState.AddModelError(
                        nameof(dto.EndDate),
                        "End date must be on or after start date.");
                }

                if (dto.StartDate.Year != dto.EndDate.Year)
                {
                    ModelState.AddModelError(
                        nameof(dto.EndDate),
                        "Please submit separate requests for different years.");
                }
            }

            if (!ModelState.IsValid)
                return View(dto);

            int totalDays =
                (dto.EndDate.Date - dto.StartDate.Date).Days + 1;

            // Prevent overlapping pending or approved leave.
            bool hasOverlap = await _context.LeaveRequests
                .AnyAsync(r =>
                    r.EmployeeId == employee.Id &&
                    (r.Status == "Pending" ||
                     r.Status == "Approved") &&
                    r.StartDate.Date <= dto.EndDate.Date &&
                    r.EndDate.Date >= dto.StartDate.Date);

            if (hasOverlap)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "You already have a pending or approved leave request for these dates.");

                return View(dto);
            }

            var request = new LeaveRequest
            {
                EmployeeId = employee.Id,
                LeaveType = dto.LeaveType,
                StartDate = dto.StartDate.Date,
                EndDate = dto.EndDate.Date,
                TotalDays = totalDays,
                Reason = dto.Reason.Trim(),
                Status = "Pending",
                AppliedOnUtc = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your leave request has been submitted successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Leave/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
                return Forbid();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.ReviewedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            bool isReviewer =
                User.IsInRole("Admin") || User.IsInRole("HR");

            bool isOwner = EmailsMatch(
                request.Employee.Email,
                currentUser.Email);

            if (!isOwner && !isReviewer)
                return Forbid();

            return View(request);
        }

        // POST: /Leave/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
                return Forbid();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            bool isOwner = EmailsMatch(
                request.Employee.Email,
                currentUser.Email);

            if (!isOwner)
                return Forbid();

            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending leave requests can be cancelled.";

                return RedirectToAction(nameof(Index));
            }

            request.Status = "Cancelled";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your leave request has been cancelled.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Leave/Manage
        // HR and Admin can review requests.
        [Authorize(Roles = "Admin,HR")]
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            var requests = await _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.ReviewedByUser)
                .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                .ThenByDescending(r => r.AppliedOnUtc)
                .ToListAsync();

            return View(requests);
        }

        // POST: /Leave/Approve/5
        [Authorize(Roles = "Admin,HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id,
            string? reviewComments)
        {
            return await ReviewRequest(
                id, "Approved", reviewComments);
        }

        // POST: /Leave/Reject/5
        [Authorize(Roles = "Admin,HR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            string? reviewComments)
        {
            if (string.IsNullOrWhiteSpace(reviewComments))
            {
                TempData["ErrorMessage"] =
                    "Please provide a reason for rejecting the request.";

                return RedirectToAction(nameof(Manage));
            }

            return await ReviewRequest(
                id, "Rejected", reviewComments);
        }

        private async Task<IActionResult> ReviewRequest(
            int id,
            string decision,
            string? reviewComments)
        {
            var reviewer = await GetCurrentUserAsync();

            if (reviewer == null)
                return Forbid();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            if (request.Status != "Pending")
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "This request has already been reviewed.";

                return RedirectToAction(nameof(Manage));
            }

            if (decision == "Approved")
            {
                var balance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(b =>
                        b.EmployeeId == request.EmployeeId &&
                        b.LeaveType == request.LeaveType &&
                        b.Year == request.StartDate.Year);

                if (balance == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        $"No {request.LeaveType} leave balance exists for " +
                        $"{request.StartDate.Year}. Configure the employee's " +
                        "leave allowance before approving this request.";

                    return RedirectToAction(nameof(Manage));
                }

                if (balance.RemainingDays < request.TotalDays)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "The employee does not have enough remaining leave days.";

                    return RedirectToAction(nameof(Manage));
                }

                balance.UsedDays += request.TotalDays;
            }

            request.Status = decision;
            request.ReviewedByUserId = reviewer.UserId;
            request.ReviewComments = reviewComments?.Trim();
            request.ReviewedOnUtc = DateTime.UtcNow;

            var notification = new Notification
            {
                RecipientEmail = request.Employee.Email,
                Title = decision == "Approved"
                    ? "Leave Request Approved"
                    : "Leave Request Rejected",

                Message = decision == "Approved"
                    ? $"Your {request.LeaveType} leave request " +
                      $"from {request.StartDate:dd MMM yyyy} to " +
                      $"{request.EndDate:dd MMM yyyy} has been approved."
                    : $"Your {request.LeaveType} leave request " +
                      $"from {request.StartDate:dd MMM yyyy} to " +
                      $"{request.EndDate:dd MMM yyyy} has been rejected. " +
                      $"Reason: {reviewComments?.Trim()}",

                CreatedAtUtc = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                $"Leave request {decision.ToLowerInvariant()} successfully.";

            return RedirectToAction(nameof(Manage));
        }

        // Resolve the logged-in user from JWT claims.
        private async Task<User?> GetCurrentUserAsync()
        {
            string? email =
                User.FindFirst(ClaimTypes.Email)?.Value ??
                User.FindFirst("email")?.Value;

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.Trim();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email != null &&
                        u.Email.Trim().ToLower() == email.ToLower());

                if (user != null)
                    return user;
            }

            // Fall back to a numeric user ID claim.
            string? userId =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value;

            if (int.TryParse(userId, out int parsedUserId))
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserId == parsedUserId);
            }

            return null;
        }

        // Find an employee whose email matches the logged-in user.
        private async Task<Employee?> GetEmployeeForUserAsync(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                return null;

            string email = user.Email.Trim().ToLower();

            return await _context.Employees
                .FirstOrDefaultAsync(e =>
                    e.Email != null &&
                    e.Email.Trim().ToLower() == email);
        }

        private static bool EmailsMatch(
            string? firstEmail,
            string? secondEmail)
        {
            return !string.IsNullOrWhiteSpace(firstEmail) &&
                   !string.IsNullOrWhiteSpace(secondEmail) &&
                   string.Equals(
                       firstEmail.Trim(),
                       secondEmail.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
