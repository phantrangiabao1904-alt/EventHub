using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using EventHub.Models;

namespace EventHub.ViewModels
{
    public class BookingDetailsViewModel
    {
        public Booking Booking { get; set; }
        public List<BookingStatusChange> History { get; set; }
        public List<BookingServiceItem> Services { get; set; }
        public List<PaymentEntry> Payments { get; set; }
        public VenueReview Review { get; set; }
        public decimal NetPaid { get { return Payments.Sum(p => p.Type == PaymentEntryType.Receipt ? p.Amount : -p.Amount); } }
        public decimal Deposit { get { return Booking.RequiredDeposit ?? Math.Round(Booking.TotalPrice * 0.3m, 0, MidpointRounding.AwayFromZero); } }
        public decimal Balance { get { return Booking.Status == BookingStatus.Cancelled ? 0 : Math.Max(0, Booking.TotalPrice - NetPaid); } }
    }

    public class ServiceSelectionViewModel
    {
        public int ServiceOptionId { get; set; }
        public bool Selected { get; set; }
        [Range(1, 10000)] public int Quantity { get; set; } = 1;
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal UnitPrice { get; set; }
    }
    public class ReviewViewModel
    {
        public int BookingId { get; set; }
        [Range(1, 5, ErrorMessage = "Chọn số sao từ 1 đến 5.")] public int Rating { get; set; }
        [StringLength(1000)] public string Comment { get; set; }
    }
    public class RecordPaymentViewModel
    {
        public int BookingId { get; set; }
        public PaymentEntryType Type { get; set; }
        [Range(typeof(decimal), "1", "1000000000000000")] public decimal Amount { get; set; }
        [Required, StringLength(100)] public string Reference { get; set; }
        [StringLength(500)] public string Notes { get; set; }
        public Guid RequestId { get; set; }
    }
    public class BookingCreateViewModel
    {
        public decimal DepositPercent { get; set; }
        public List<ServiceSelectionViewModel> Services { get; set; } = new List<ServiceSelectionViewModel>();
        public int VenueId { get; set; }

         
        public string VenueName { get; set; }
        public string VenueImageUrl { get; set; }
        public int VenueCapacity { get; set; }
        public decimal VenuePrice { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày tổ chức.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày tổ chức")]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
        [Display(Name = "Giờ bắt đầu")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
        [Display(Name = "Giờ kết thúc")]
        public TimeSpan EndTime { get; set; }

        [Range(1, 100000, ErrorMessage = "Số khách phải từ 1 trở lên.")]
        [Display(Name = "Số khách")]
        public int GuestCount { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1000 ký tự.")]
        [Display(Name = "Ghi chú / yêu cầu đặc biệt")]
        public string Notes { get; set; }

        public BookingCreateViewModel()
        {
            EventDate = DateTime.Today.AddDays(1);
            StartTime = new TimeSpan(9, 0, 0);
            EndTime = new TimeSpan(17, 0, 0);
            GuestCount = 10;
        }
    }

    public class BookingFilterViewModel
    {
        public string Search { get; set; }
        public BookingStatus? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<Booking> Items { get; set; }
    }
}
