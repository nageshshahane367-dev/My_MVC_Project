
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MyFirstMVCApp.Dto;
using MyFirstMVCApp.Models;

namespace MyFirstMVCApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthController(
            AppDbContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        // Display Login page
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        // Display Registration page
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Registration()
        {
            return View();
        }

        // Navigate from Login to Registration
        [HttpGet]
        [AllowAnonymous]
        public IActionResult LoginToRegister()
        {
            return RedirectToAction(nameof(Registration));
        }

        // Navigate from Registration to Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterToLogin()
        {
            return RedirectToAction(nameof(Login));
        }

        // Register a new user
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> RegisterUser(UserDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                ViewBag.Message = "Please enter email and password.";
                return View("Registration", dto);
            }

            var email = dto.Email.Trim();

            // Only allow these predefined roles
            var allowedRoles = new[] { "Admin", "HR", "Employee" };

            if (string.IsNullOrWhiteSpace(dto.Role) ||
                !allowedRoles.Contains(dto.Role))
            {
                ViewBag.Message = "Please select a valid role.";
                return View("Registration", dto);
            }

            // Check whether the email already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                ViewBag.Message = "Email already exists.";
                return View("Registration", dto);
            }

            var user = new User
            {
                Username = dto.Username?.Trim() ?? string.Empty,
                Email = email,
                Role = dto.Role
            };

            // Store a password hash, never the plain-text password
            user.Password = _passwordHasher.HashPassword(
                user,
                dto.Password
            );

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Registration successful. Please log in.";

            return RedirectToAction(nameof(Login));
        }

        // Login an existing user
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginUser(UserDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                ViewBag.Message = "Please enter email and password.";
                return View("Login", dto);
            }

            var email = dto.Email.Trim();

            // Find the user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            // Use a generic message to avoid revealing account existence
            if (user == null || string.IsNullOrWhiteSpace(user.Password))
            {
                ViewBag.Message = "Invalid email or password.";
                return View("Login", dto);
            }

            PasswordVerificationResult verificationResult;

            // Handle invalid or incompatible stored password formats
            try
            {
                verificationResult = _passwordHasher.VerifyHashedPassword(
                    user,
                    user.Password,
                    dto.Password
                );
            }
            catch (FormatException)
            {
                ViewBag.Message =
                    "This account's password format is invalid. " +
                    "Please reset the password or register a new test account.";

                return View("Login", dto);
            }

            // Reject incorrect passwords
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ViewBag.Message = "Invalid email or password.";
                return View("Login", dto);
            }

            // Upgrade the password hash when required
            if (verificationResult ==
                PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Password = _passwordHasher.HashPassword(
                    user,
                    dto.Password
                );

                await _context.SaveChangesAsync();
            }

            // Generate JWT token with username, email and role
            var token = GenerateJwtToken(
                user.Username,
                user.Email,
                user.Role
            );

            // Store JWT in an HTTP-only cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddHours(1),
                IsEssential = true
            };

            Response.Cookies.Append(
                "jwt_Token",
                token,
                cookieOptions
            );

            return RedirectToAction("Index", "Dashboard");
        }

        // Generate JWT token
        private string GenerateJwtToken(
            string username,
            string email,
            string role)
        {
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT key is missing from configuration."
                );

            var key = Encoding.UTF8.GetBytes(jwtKey);

            if (key.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Key must contain at least 32 bytes for HS256."
                );
            }

            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role)
                }),
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        // Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_Token", new CookieOptions
            {
                Path = "/",
                Secure = Request.IsHttps,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });

            return RedirectToAction(nameof(Login));
        }
    }
}
