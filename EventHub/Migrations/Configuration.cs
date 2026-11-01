using System.Data.Entity.Migrations;
using System.Linq;
using EventHub.Data;
using EventHub.Models;
using EventHub.Security;

namespace EventHub.Migrations
{
     
    internal sealed class Configuration : DbMigrationsConfiguration<EventHubContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;
            ContextKey = "EventHub.Data.EventHubContext";
        }

        protected override void Seed(EventHubContext context)
        {
            AddUser(context, "Quản trị viên", "admin@eventhub.com", "Admin@123", Role.Admin);
            AddUser(context, "Điều phối viên", "coordinator@eventhub.com", "Coord@123", Role.Coordinator);
            AddUser(context, "Khách hàng mẫu", "client@eventhub.com", "Client@123", Role.Client);
            context.SaveChanges();

            if (!context.ServiceOptions.Any())
            {
                context.ServiceOptions.Add(new ServiceOption { Name = "Trang trí cơ bản", Description = "Gói trang trí sự kiện cơ bản", UnitPrice = 2000000m, IsActive = true });
                context.ServiceOptions.Add(new ServiceOption { Name = "Âm thanh và micro", Description = "Một bộ âm thanh cho sự kiện", UnitPrice = 1500000m, IsActive = true });
                context.ServiceOptions.Add(new ServiceOption { Name = "Suất ăn", Description = "Giá cho một khách", UnitPrice = 250000m, IsActive = true });
                context.SaveChanges();
            }

             
        }

        private static void AddUser(EventHubContext context, string name, string email, string password, Role role)
        {
            if (context.Users.Any(u => u.Email == email)) return;
            context.Users.Add(new User { FullName = name, Email = email, PasswordHash = PasswordHasher.Hash(password), Role = role });
        }
    }
}
