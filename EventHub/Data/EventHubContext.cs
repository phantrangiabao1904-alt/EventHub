using System.Data.Entity;
using EventHub.Models;

namespace EventHub.Data
{
    public class EventHubContext : DbContext
    {
        public EventHubContext() : base("name=EventHubConnection") { }
        public EventHubContext(string connectionString) : base(connectionString) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingStatusChange> BookingStatusChanges { get; set; }
        public DbSet<ServiceOption> ServiceOptions { get; set; }
        public DbSet<BookingServiceItem> BookingServiceItems { get; set; }
        public DbSet<VenueReview> VenueReviews { get; set; }
        public DbSet<PaymentEntry> PaymentEntries { get; set; }
        public DbSet<DepositSettings> DepositSettings { get; set; }
        public DbSet<PendingRegistration> PendingRegistrations { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Venue>().Property(v => v.Price).HasPrecision(18, 0);
            modelBuilder.Entity<Booking>().Property(b => b.TotalPrice).HasPrecision(18, 0);
            modelBuilder.Entity<Booking>().Property(b => b.VenueRentalPrice).HasPrecision(18, 0);
            modelBuilder.Entity<Booking>().Property(b => b.RequiredDeposit).HasPrecision(18, 0);
            modelBuilder.Entity<Booking>().Property(b => b.DepositPercent).HasPrecision(5, 2);
            modelBuilder.Entity<DepositSettings>().Property(s => s.Percent).HasPrecision(5, 2);
            modelBuilder.Entity<ServiceOption>().Property(s => s.UnitPrice).HasPrecision(18, 0);
            modelBuilder.Entity<BookingServiceItem>().Property(s => s.UnitPrice).HasPrecision(18, 0);
            modelBuilder.Entity<PaymentEntry>().Property(p => p.Amount).HasPrecision(18, 0);

             
            modelBuilder.Entity<Booking>()
                .HasRequired(b => b.User).WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId).WillCascadeOnDelete(false);
            modelBuilder.Entity<Booking>()
                .HasRequired(b => b.Venue).WithMany(v => v.Bookings)
                .HasForeignKey(b => b.VenueId).WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }
    }
}
