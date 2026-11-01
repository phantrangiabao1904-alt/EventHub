using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHub.Models
{
     
    public class PendingRegistration
    {
        public int Id { get; set; }

        [Required, StringLength(255)]
        [Index("IX_PendingRegistration_Email", IsUnique = true)]
        public string Email { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, StringLength(255)]
        public string PasswordHash { get; set; }

         
        [Required, StringLength(255)]
        public string OtpHash { get; set; }

        public DateTime ExpiresAt { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime LastSentAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public PendingRegistration() { CreatedAt = DateTime.Now; }
    }
}
