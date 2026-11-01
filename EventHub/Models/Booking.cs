using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHub.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Index("IX_Booking_Venue_Date", 1)]
        public int VenueId { get; set; }

        [Column(TypeName = "date")]
        [Index("IX_Booking_Venue_Date", 2)]
        public DateTime EventDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public int GuestCount { get; set; }

         
        public decimal TotalPrice { get; set; }
        public decimal? VenueRentalPrice { get; set; }
        public decimal? RequiredDeposit { get; set; }
        public decimal? DepositPercent { get; set; }
        [StringLength(500)] public string DepositQrImageUrl { get; set; }
        [StringLength(100)] public string DepositBankName { get; set; }
        [StringLength(50)] public string DepositAccountNumber { get; set; }
        [StringLength(100)] public string DepositAccountHolder { get; set; }
        [StringLength(1000)] public string DepositInstructions { get; set; }
        public DateTime? DepositRequestedAt { get; set; }

        public BookingStatus Status { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; }

         
        public DateTime? ReminderSentAt { get; set; }

        public virtual User User { get; set; }
        public virtual Venue Venue { get; set; }

        public Booking()
        {
            Status = BookingStatus.Pending;
            CreatedAt = DateTime.Now;
        }

        public DateTime StartsAt() { return EventDate.Date + StartTime; }
    }
}
