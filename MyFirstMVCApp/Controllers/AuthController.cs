using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
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

        public AuthController(
            AppDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
        public IActionResult Registration()
        {
            return View();
        }

        // Navigate from Login to Registration
        public IActionResult LoginToRegister()
        {
            return RedirectToAction("Registration");
        }

        // Navigate from Registration to Login
        public IActionResult RegisterToLogin()
        {
            return RedirectToAction("Login");
        }

        // Register a new user
        [HttpPost]
        public async Task<IActionResult> RegisterUser(UserDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                ViewBag.Message =
                    "Please enter email and password.";

                return View("Registration", dto);
            }

            // Check whether email already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == dto.Email);

            if (existingUser != null)
            {
                ViewBag.Message = "Email already exists.";
                return View("Registration", dto);
            }

            // Create new user
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Password = dto.Password
            };


            _context.Users.Add(user);

            int rowsAffected = await _context.SaveChangesAsync();

            Console.WriteLine($"Users inserted: {rowsAffected}");
            Console.WriteLine($"Generated UserId: {user.UserId}");

            TempData["Message"] = "Registration successful. Please log in.";

            return RedirectToAction("Login");

        }

        // Login an existing user
        [HttpPost]
        public async Task<IActionResult> LoginUser(UserDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                ViewBag.Message =
                    "Please enter email and password.";

                return View("Login", dto);
            }

            // Find user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == dto.Email);

            // Check whether user exists
            if (user == null)
            {
                ViewBag.Message =
                    "Email does not exist. Please register first.";

                return View("Login", dto);
            }

            // Check password
            if (user.Password != dto.Password)
            {
                ViewBag.Message = "Incorrect password.";
                return View("Login", dto);
            }

            // Generate JWT token
            var token = GenerateJwtToken(user.Username, user.Email);

            // Store token in an HTTP-only cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            Response.Cookies.Append(
                "jwt_Token",
                token,
                cookieOptions);

            return RedirectToAction("Index", "Dashboard");
        }

        // Generate JWT token
        private string GenerateJwtToken(string username, string email)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key is missing.");

            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email)
        }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // Logout


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Delete the JWT cookie using the same path.
            Response.Cookies.Delete("jwt_Token", new CookieOptions
            {
                Path = "/",
                Secure = Request.IsHttps,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });

            // Redirect directly to the Login URL.
            return Redirect("/Auth/Login");
        }

    }
}
