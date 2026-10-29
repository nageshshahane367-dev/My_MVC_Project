
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstMVCApp.Dto;
using MyFirstMVCApp.Models;
using MyFirstMVCApp.ViewModels;
using System.Security.Claims;

namespace MyFirstMVCApp.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // Get the email of the currently logged-in user.
        private string? GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;
        }

        // Check whether the logged-in user can manage any employee.
        private bool CanManageEmployees()
        {
            return User.IsInRole("Admin") || User.IsInRole("HR");
        }

        // Create a notification for a specific recipient.
        private async Task CreateNotificationAsync(
            string? recipientEmail,
            string title,
            string message)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            var notification = new Notification
            {
                RecipientEmail = recipientEmail.Trim(),
                Title = title,
                Message = message,
                CreatedAtUtc = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        // Admin, HR and Employee can view the employee list.
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Employees
                .AsNoTracking()
                .Select(x => new EmployeeDto
                {
                    Id = x.Id,
                    EmployeeName = x.EmployeeName,
                    Department = x.Department,
                    Email = x.Email,
                    Designation = x.Designation
                })
                .ToListAsync();

            return View(list);
        }

        // Open the Add Employee form.
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public IActionResult AddEmployee()
        {
            return View();
        }

        // Redirect to the Add Employee form.
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public IActionResult RedirectToEmployeeForm()
        {
            return RedirectToAction(nameof(AddEmployee));
        }

        // Add an employee.
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployeeDetail(EmployeeDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Message =
                    "Please enter valid employee details.";

                return View("AddEmployee", dto);
            }

            // Employees can add only their own profile.
            if (!CanManageEmployees())
            {
                var currentEmail = GetCurrentUserEmail();

                if (string.IsNullOrWhiteSpace(currentEmail) ||
                    !string.Equals(
                        currentEmail,
                        dto.Email?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var normalizedEmail = dto.Email?.Trim();

            bool exists = await _context.Employees
                .AnyAsync(e => e.Email == normalizedEmail);

            if (exists)
            {
                ViewBag.Message =
                    "An employee with the same email already exists.";

                return View("AddEmployee", dto);
            }

            var employee = new Employee
            {
                EmployeeName = dto.EmployeeName,
                Department = dto.Department,
                Email = normalizedEmail!,
                Designation = dto.Designation,
                CreatedAt = DateTime.UtcNow
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Only Admin can delete employees.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee is null)
            {
                return NotFound();
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Open the Update Employee form.
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> UpdateEmployee(int id)
        {
            var data = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (data is null)
            {
                return NotFound();
            }

            // Employees can open only their own profile.
            if (!CanManageEmployees())
            {
                var currentEmail = GetCurrentUserEmail();

                if (string.IsNullOrWhiteSpace(currentEmail) ||
                    !string.Equals(
                        currentEmail,
                        data.Email,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var dto = new EmployeeDto
            {
                Id = data.Id,
                EmployeeName = data.EmployeeName,
                Department = data.Department,
                Email = data.Email,
                Designation = data.Designation
            };

            return View(dto);
        }

        // Display employee analytics.
        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Analytics()
        {
            var employees = _context.Employees.AsNoTracking();

            var model = new DashboardAnalyticsViewModel
            {
                TotalEmployees = await employees.CountAsync(),

                TotalDepartments = await employees
                    .Where(e => e.Department != null &&
                                e.Department != "")
                    .Select(e => e.Department)
                    .Distinct()
                    .CountAsync(),

                TotalDesignations = await employees
                    .Where(e => e.Designation != null &&
                                e.Designation != "")
                    .Select(e => e.Designation)
                    .Distinct()
                    .CountAsync(),

                DepartmentCounts = await employees
                    .GroupBy(e => e.Department)
                    .Select(g => new DepartmentEmployeeCount
                    {
                        Department = g.Key ?? "Unassigned",
                        EmployeeCount = g.Count()
                    })
                    .OrderByDescending(x => x.EmployeeCount)
                    .ToListAsync(),

                RecentEmployees = await employees
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(5)
                    .Select(e => new RecentEmployeeViewModel
                    {
                        Id = e.Id,
                        EmployeeName = e.EmployeeName,
                        Department = e.Department,
                        Designation = e.Designation
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        // Display employee profile details.
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee is null)
            {
                return NotFound();
            }

            // Admin and HR can view any employee's profile.
            if (CanManageEmployees())
            {
                return View(employee);
            }

            // Employees can view only their own profile.
            var currentEmail = GetCurrentUserEmail();

            if (string.IsNullOrWhiteSpace(currentEmail) ||
                !string.Equals(
                    currentEmail,
                    employee.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return View(employee);
        }

        // Save employee updates and create a notification.
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployeeDetail(
            EmployeeDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Message =
                    "Please fill in all the details correctly.";

                return View("UpdateEmployee", dto);
            }

            var data = await _context.Employees
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (data is null)
            {
                return NotFound();
            }

            // Employees can update only their own profile.
            if (!CanManageEmployees())
            {
                var currentEmail = GetCurrentUserEmail();

                if (string.IsNullOrWhiteSpace(currentEmail) ||
                    !string.Equals(
                        currentEmail,
                        data.Email,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                // Employees cannot change their account-linked email.
                if (!string.Equals(
                    currentEmail,
                    dto.Email?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        nameof(dto.Email),
                        "You cannot change your registered email address.");

                    ViewBag.Message =
                        "You cannot change your registered email address.";

                    return View("UpdateEmployee", dto);
                }
            }

            var normalizedEmail = dto.Email?.Trim();

            bool emailExists = await _context.Employees
                .AnyAsync(x =>
                    x.Email == normalizedEmail &&
                    x.Id != dto.Id);

            if (emailExists)
            {
                ViewBag.Message =
                    "Another employee already uses this email.";

                return View("UpdateEmployee", dto);
            }

            // Remember the original email for notification delivery.
            var originalEmail = data.Email;

            data.EmployeeName = dto.EmployeeName;
            data.Department = dto.Department;
            data.Email = normalizedEmail!;
            data.Designation = dto.Designation;

            await _context.SaveChangesAsync();

            // Notify the employee at their original registered address.
            // If Admin/HR changed the email, also notify the new address.
            await CreateNotificationAsync(
                originalEmail,
                "Employee Profile Updated",
                "Your employee profile information has been updated.");

            if (!string.Equals(
                originalEmail,
                normalizedEmail,
                StringComparison.OrdinalIgnoreCase))
            {
                await CreateNotificationAsync(
                    normalizedEmail,
                    "Employee Profile Updated",
                    "Your employee profile information has been updated.");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
