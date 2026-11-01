using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EventHub.Models;

namespace EventHub.ViewModels
{
    public class VenueDetailsViewModel
    {
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public List<PublicReviewViewModel> Reviews { get; set; } = new List<PublicReviewViewModel>();
        public System.TimeSpan? StartTime { get; set; }
        public System.TimeSpan? EndTime { get; set; }
        public bool? IsAvailable { get; set; }
        public string AvailabilityError { get; set; }
        public Venue Venue { get; set; }
        public System.DateTime Date { get; set; }
        public List<VenueTimeSlot> BookedSlots { get; set; }
        public List<VenueTimeSlot> FreeSlots { get; set; }
    }

    public class VenueTimeSlot
    {
        public System.TimeSpan Start { get; set; }
        public System.TimeSpan End { get; set; }
    }

    public class PublicReviewViewModel
    {
        public int Rating { get; set; }
        public string Comment { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
    public class VenueListViewModel
    {
        public System.DateTime? EventDate { get; set; }
        public System.TimeSpan? StartTime { get; set; }
        public System.TimeSpan? EndTime { get; set; }
        public bool AvailabilityChecked { get; set; }
        public string AvailabilityError { get; set; }
        public List<Venue> Venues { get; set; }
        public string Search { get; set; }
        public int? Capacity { get; set; }
        public string Sort { get; set; }
    }

    public class VenueFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên địa điểm.")]
        [StringLength(200, ErrorMessage = "Tên địa điểm tối đa 200 ký tự.")]
        [Display(Name = "Tên địa điểm")]
        public string Name { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
        [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [Range(1, 100000, ErrorMessage = "Sức chứa phải từ 1 đến 100.000 khách.")]
        [Display(Name = "Sức chứa (khách)")]
        public int Capacity { get; set; }

        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Giá thuê phải từ 0 đến 1.000.000.000 ₫.")]
        [Display(Name = "Giá thuê / sự kiện (₫)")]
        public decimal Price { get; set; }

        [Display(Name = "Trạng thái")]
        public VenueStatus Status { get; set; }

        [RegularExpression(@"^(https?://[^\s]+|/Uploads/Venues/[a-f0-9]{32}\.jpg|/Content/images/venues/sample-\d{2}\.jpg)$", ErrorMessage = "Nhập URL http/https hợp lệ hoặc chọn ảnh từ máy.")]
        [StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa 500 ký tự.")]
        [Display(Name = "Đường dẫn ảnh")]
        public string ImageUrl { get; set; }

        public VenueFormViewModel() { Status = VenueStatus.Available; }
    }
}
