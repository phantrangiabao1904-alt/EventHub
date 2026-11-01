using System.ComponentModel.DataAnnotations;

namespace EventHub.ViewModels
{
    public class DepositSettingsViewModel
    {
        [Range(typeof(decimal), "1", "100", ErrorMessage = "Tỷ lệ cọc phải từ 1% đến 100%.")]
        public decimal Percent { get; set; }
        [StringLength(500)] public string QrImageUrl { get; set; }
        [StringLength(100)] public string BankName { get; set; }
        [StringLength(50)] public string AccountNumber { get; set; }
        [StringLength(100)] public string AccountHolder { get; set; }
        [StringLength(1000)] public string Instructions { get; set; }
    }
    public class DepositRequestViewModel : DepositSettingsViewModel
    {
        public int BookingId { get; set; }
    }
}
