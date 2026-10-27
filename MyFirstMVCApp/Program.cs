
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using MyFirstMVCApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyFirstMVCApp.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC services
builder.Services.AddControllersWithViews();

// JWT authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is missing from configuration.");

        var key = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                    NameClaimType = ClaimTypes.Name,
                   RoleClaimType = ClaimTypes.Role,

    ClockSkew = TimeSpan.FromMinutes(1)
            };

        options.Events = new JwtBearerEvents
        {
            // Read the JWT from the browser cookie
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["jwt_Token"];

                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },

            // Redirect unauthenticated requests to the Home page
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<
    IEmployeeDescriptionService,
    OpenAiEmployeeDescriptionService>();
var app = builder.Build();

// DATABASE DIAGNOSTICS
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    var connection = db.Database.GetDbConnection();

    Console.WriteLine("\n========== DATABASE DEBUG ==========");

    Console.WriteLine($"Configured Server: {connection.DataSource}");
    Console.WriteLine($"Configured Database: {connection.Database}");

    try
    {
        await db.Database.OpenConnectionAsync();

        // Print the actual server and database
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT
                    @@SERVERNAME AS ServerName,
                    DB_NAME() AS DatabaseName;
                """;

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Console.WriteLine(
                    $"Actual Server: {reader["ServerName"]}");

                Console.WriteLine(
                    $"Actual Database: {reader["DatabaseName"]}");
            }
        }

        // Check whether the expected tables exist
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT
                    TABLE_SCHEMA,
                    TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME;
                """;

            await using var reader =
                await command.ExecuteReaderAsync();

            Console.WriteLine("\nTables found in this database:");

            bool foundTables = false;

            while (await reader.ReadAsync())
            {
                foundTables = true;

                Console.WriteLine(
                    $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
            }

            if (!foundTables)
            {
                Console.WriteLine("No visible base tables found.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"DATABASE DIAGNOSTIC ERROR: {ex.Message}");
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }

    Console.WriteLine("====================================\n");
}

Console.WriteLine("TEST: Updated Program.cs is running");

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Default route: Home page
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Print registered MVC actions
var actionProvider =
    app.Services.GetRequiredService
        <IActionDescriptorCollectionProvider>();

Console.WriteLine("\n========== MVC ACTIONS ==========");

foreach (var action in actionProvider.ActionDescriptors.Items)
{
    Console.WriteLine(action.DisplayName);
}

Console.WriteLine("=================================\n");

app.Run();
