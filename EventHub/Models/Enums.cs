namespace EventHub.Models
{
    public enum Role { Admin = 1, Coordinator = 2, Client = 3 }

    public enum VenueStatus { Available = 1, Maintenance = 2 }

    public enum BookingStatus { Pending = 1, Confirmed = 2, Completed = 3, Cancelled = 4, AwaitingDeposit = 5 }

     
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Coordinator = "Coordinator";
        public const string Client = "Client";
        public const string Staff = "Admin,Coordinator";
    }
}
