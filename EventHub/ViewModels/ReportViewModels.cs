using System.Collections.Generic;
using System.Linq;
using EventHub.Models;

namespace EventHub.ViewModels
{
    public class MonthlyReportViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public List<Booking> Bookings { get; set; }
        public decimal Receipts { get; set; }
        public decimal Refunds { get; set; }
        public decimal NetCollected { get { return Receipts - Refunds; } }
        public int Pending { get { return Bookings.Count(b => b.Status == BookingStatus.Pending); } }
        public int AwaitingDeposit { get { return Bookings.Count(b => b.Status == BookingStatus.AwaitingDeposit); } }
        public int Confirmed { get { return Bookings.Count(b => b.Status == BookingStatus.Confirmed); } }
        public int Completed { get { return Bookings.Count(b => b.Status == BookingStatus.Completed); } }
        public int Cancelled { get { return Bookings.Count(b => b.Status == BookingStatus.Cancelled); } }
        public decimal CancellationRate { get { return Bookings.Count == 0 ? 0 : 100m * Cancelled / Bookings.Count; } }
        public decimal ContractValue { get { return Bookings.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed).Sum(b => b.TotalPrice); } }
        public List<VenueReportRow> TopVenues { get; set; }
    }

    public class VenueReportRow
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public decimal Value { get; set; }
    }
}
