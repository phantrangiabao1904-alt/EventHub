using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHub.Models
{
    public class ServiceOption
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(500)] public string Description { get; set; }
        [Range(typeof(decimal), "0", "1000000000")] public decimal UnitPrice { get; set; }
        public bool IsActive { get; set; }
    }
    public class BookingServiceItem
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int ServiceOptionId { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public virtual Booking Booking { get; set; }
        public virtual ServiceOption ServiceOption { get; set; }
    }
    public class VenueReview
    {
        public int Id { get; set; }
        [Index("IX_Review_Booking", IsUnique = true)] public int BookingId { get; set; }
        [Range(1, 5)] public int Rating { get; set; }
        [StringLength(1000)] public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual Booking Booking { get; set; }
    }
    public enum PaymentEntryType { Receipt = 1, Refund = 2 }
    public class PaymentEntry
    {
        public int Id { get; set; }
        [Index("IX_Payment_Booking"), Index("IX_Payment_Reference", 1, IsUnique = true)] public int BookingId { get; set; }
        [Index("IX_Payment_Reference", 2, IsUnique = true)] public PaymentEntryType Type { get; set; }
        public decimal Amount { get; set; }
        [Index("IX_Payment_Request", IsUnique = true)] public Guid RequestId { get; set; }
        [Required, StringLength(100), Index("IX_Payment_Reference", 3, IsUnique = true)] public string Reference { get; set; }
        [StringLength(500)] public string Notes { get; set; }
        [Required, StringLength(100)] public string RecordedBy { get; set; }
        public DateTime RecordedAt { get; set; }
        public virtual Booking Booking { get; set; }
    }
}
