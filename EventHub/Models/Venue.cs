using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EventHub.Models
{
    public class Venue
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required, StringLength(500)]
        public string Address { get; set; }

         
        public int Capacity { get; set; }

         
        public decimal Price { get; set; }

        public VenueStatus Status { get; set; }

        [StringLength(500)]
        public string ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; }

        public Venue()
        {
            Bookings = new List<Booking>();
            CreatedAt = DateTime.Now;
            Status = VenueStatus.Available;
        }
    }
}
