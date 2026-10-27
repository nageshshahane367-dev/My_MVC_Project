
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstMVCApp.Dto;
using MyFirstMVCApp.Models;
using MyFirstMVCApp.Services;

namespace MyFirstMVCApp.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmployeeDescriptionService _employeeDescriptionService;

        public DashboardController(
            AppDbContext context,
            IEmployeeDescriptionService employeeDescriptionService)
        {
            _context = context;
            _employeeDescriptionService = employeeDescriptionService;
        }

        // Admin, HR and Employee can view the employee list
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Employees
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

        // Open the Add Employee form
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public IActionResult AddEmployee()
        {
            return View();
        }

        // Redirect to the Add Employee form
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public IActionResult RedirectToEmployeeForm()
        {
            return RedirectToAction(nameof(AddEmployee));
        }

        // Add an employee
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployeeDetail(EmployeeDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Please enter valid employee details.";
                return View("AddEmployee", dto);
            }

            bool exists = await _context.Employees
                .AnyAsync(e => e.Email == dto.Email);

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
                Email = dto.Email,
                Designation = dto.Designation
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Only Admin can delete employees
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

        // Open the Update Employee form
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> UpdateEmployee(int id)
        {
            var data = await _context.Employees
                .FirstOrDefaultAsync(x => x.Id == id);

            if (data is null)
            {
                return NotFound();
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

        // Save employee updates
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployeeDetail(EmployeeDto dto)
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

            bool emailExists = await _context.Employees
                .AnyAsync(x => x.Email == dto.Email && x.Id != dto.Id);

            if (emailExists)
            {
                ViewBag.Message =
                    "Another employee already uses this email.";

                return View("UpdateEmployee", dto);
            }

            data.EmployeeName = dto.EmployeeName;
            data.Department = dto.Department;
            data.Email = dto.Email;
            data.Designation = dto.Designation;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Generate an AI employee description
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateEmployeeDescription(
            [FromBody] EmployeeDescriptionRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Department) ||
                string.IsNullOrWhiteSpace(request.Designation))
            {
                return BadRequest(new
                {
                    error = "Please enter a department and designation."
                });
            }

            if (request.Department.Length > 100 ||
                request.Designation.Length > 100)
            {
                return BadRequest(new
                {
                    error = "Department and designation must be 100 characters or fewer."
                });
            }

            try
            {
                var description =
                    await _employeeDescriptionService.GenerateDescriptionAsync(
                        request.Department.Trim(),
                        request.Designation.Trim(),
                        cancellationToken);

                return Json(new { description });
            }

            catch (HttpRequestException ex)
            {
                // Development debugging only.
                Console.WriteLine($"AI service error: {ex.Message}");

                return StatusCode(502, new
                {
                    error = ex.Message
                });
            }

            catch (InvalidOperationException)
            {
                return StatusCode(500, new
                {
                    error = "The AI service is not configured correctly or returned no description."
                });
            }
        }
    }
}
