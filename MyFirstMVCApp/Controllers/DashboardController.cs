using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstMVCApp.Dto;
using MyFirstMVCApp.Models;

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

        public async Task<IActionResult> Index()
        {
            var list = await _context.Employees
                .Select(x => new EmployeeDto
                {
                    EmployeeName = x.EmployeeName,
                    Department = x.Department,
                    Email = x.Email,
                    Designation = x.Designation,
                    Id = x.Id
                })
                .ToListAsync();

            return View(list);
        }

        [HttpGet]
        public IActionResult AddEmployee()
        {
            return View();
        }

        public IActionResult RedirectToEmployeeForm()
        {
            return RedirectToAction(nameof(AddEmployee));
        }

        [HttpPost]
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
                    "Employee with the same email already exists.";

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

        [HttpPost]
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

        [HttpGet]
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

        [HttpPost]
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

            data.Email = dto.Email;
            data.EmployeeName = dto.EmployeeName;
            data.Designation = dto.Designation;
            data.Department = dto.Department;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}