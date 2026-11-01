using System.ComponentModel.DataAnnotations;

namespace EventHub.Models
{
    public class DepositSettings
    {
        public int Id { get; set; }
        public decimal Percent { get; set; }
        [StringLength(500)] public string QrImageUrl { get; set; }
        [StringLength(100)] public string BankName { get; set; }
        [StringLength(50)] public string AccountNumber { get; set; }
        [StringLength(100)] public string AccountHolder { get; set; }
        [StringLength(1000)] public string Instructions { get; set; }
    }
}
