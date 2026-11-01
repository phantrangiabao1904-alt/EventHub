using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventHub.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, StringLength(255)]
        [Index("IX_Users_Email", IsUnique = true)]
        public string Email { get; set; }

        [Required, StringLength(255)]
        public string PasswordHash { get; set; }

        public Role Role { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; }

        public User()
        {
            Bookings = new List<Booking>();
            CreatedAt = DateTime.Now;
            Role = Role.Client;
        }
    }
}
