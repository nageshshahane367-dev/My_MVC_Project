
using Microsoft.EntityFrameworkCore;

namespace MyFirstMVCApp.Models
{
    public class AppDbContext(
        DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        // Existing entities
        public DbSet<Employee> Employees { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Leave Management entities
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // A single balance per employee, leave type and year.
            modelBuilder.Entity<LeaveBalance>()
                .HasIndex(b => new
                {
                    b.EmployeeId,
                    b.LeaveType,
                    b.Year
                })
                .IsUnique();

            // Leave request -> Employee
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(r => r.Employee)
                .WithMany()
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Leave balance -> Employee
            modelBuilder.Entity<LeaveBalance>()
                .HasOne(b => b.Employee)
                .WithMany()
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Leave request -> Reviewer (optional)
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(r => r.ReviewedByUser)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
