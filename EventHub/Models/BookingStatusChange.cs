using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHub.Models
{
    public class BookingStatusChange
    {
        public int Id { get; set; }
        [Index("IX_StatusChange_Booking")]
        public int BookingId { get; set; }
        public BookingStatus? FromStatus { get; set; }
        public BookingStatus ToStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        [Required, StringLength(100)]
        public string ActorName { get; set; }
        public virtual Booking Booking { get; set; }
    }
}
