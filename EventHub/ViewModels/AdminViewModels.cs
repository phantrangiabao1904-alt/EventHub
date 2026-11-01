using System;
using System.Collections.Generic;
using EventHub.Models;

namespace EventHub.ViewModels
{
    public class DashboardViewModel
    {
        public DepositSettingsViewModel DepositSettings { get; set; }
        public int AwaitingDeposit { get; set; }
        public int TotalVenues { get; set; }
        public int AvailableVenues { get; set; }
        public int TotalClients { get; set; }
        public int TotalBookings { get; set; }
        public int Pending { get; set; }
        public int Confirmed { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public decimal Revenue { get; set; }
        public List<Booking> UpcomingBookings { get; set; }
        public List<Booking> RecentBookings { get; set; }
    }

    public class UserRowViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public Role Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public int BookingCount { get; set; }
    }

    public class UserListViewModel
    {
        public string Search { get; set; }
        public Role? Role { get; set; }
        public List<UserRowViewModel> Items { get; set; }
    }
}
