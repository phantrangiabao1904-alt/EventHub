using System.Linq;
using System.Web.Mvc;
using EventHub.Models;
using EventHub.ViewModels;
using EventHub.Services;
using System;

namespace EventHub.Controllers
{
     
    public class VenuesController : BaseController
    {
        public ActionResult Index(string search, int? capacity, string sort, DateTime? eventDate, TimeSpan? startTime, TimeSpan? endTime)
        {
            var error = AvailabilityService.Validate(eventDate, startTime, endTime, DateTime.Now);
            if (!ModelState.IsValid) error = "Ngày, giờ hoặc sức chứa không hợp lệ.";
            if (capacity.HasValue && capacity.Value < 1) error = "Sức chứa phải từ 1 khách trở lên.";
             
            var q = Db.Venues.Where(v => v.Status == VenueStatus.Available);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(v => v.Name.Contains(s) || v.Address.Contains(s));
            }
            if (capacity.HasValue)
            {
                var c = capacity.Value;
                q = q.Where(v => v.Capacity >= c);
            }

            var checkedTime = error == null && eventDate.HasValue;
            if (checkedTime)
            {
                var day = eventDate.Value.Date;
                var start = startTime.Value;
                var end = endTime.Value;
                q = q.Where(v => !Db.Bookings.Any(b => b.VenueId == v.Id && b.EventDate == day
                    && b.Status != BookingStatus.Cancelled && b.StartTime < end && b.EndTime > start));
            }
            if (error != null) q = q.Where(v => false);

            switch (sort)
            {
                case "price_asc": q = q.OrderBy(v => v.Price); break;
                case "price_desc": q = q.OrderByDescending(v => v.Price); break;
                case "capacity": q = q.OrderByDescending(v => v.Capacity); break;
                default: q = q.OrderBy(v => v.Name); break;
            }

            return View(new VenueListViewModel { Venues = q.ToList(), Search = search, Capacity = capacity, Sort = sort,
                EventDate = eventDate, StartTime = startTime, EndTime = endTime, AvailabilityChecked = checkedTime, AvailabilityError = error });
        }

        public ActionResult Details(int id, DateTime? date, TimeSpan? startTime, TimeSpan? endTime)
        {
            var venue = Db.Venues.Find(id);
            if (venue == null) return HttpNotFound();
            var day = (date ?? System.DateTime.Today).Date;
            var booked = Db.Bookings.Where(b => b.VenueId == id && b.EventDate == day && b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.StartTime).Select(b => new VenueTimeSlot { Start = b.StartTime, End = b.EndTime }).ToList();
            var free = new System.Collections.Generic.List<VenueTimeSlot>();
            var cursor = day == System.DateTime.Today ? TimeSpan.FromMinutes(Math.Ceiling(DateTime.Now.TimeOfDay.TotalMinutes)) : TimeSpan.Zero;
            foreach (var slot in booked)
            {
                if (slot.Start > cursor) free.Add(new VenueTimeSlot { Start = cursor, End = slot.Start });
                if (slot.End > cursor) cursor = slot.End;
            }
            if (cursor < System.TimeSpan.FromDays(1)) free.Add(new VenueTimeSlot { Start = cursor, End = System.TimeSpan.FromDays(1) });
            if (venue.Status != VenueStatus.Available || day < System.DateTime.Today) free.Clear();
            var error = !ModelState.IsValid ? "Ngày hoặc giờ không hợp lệ." :
                (startTime.HasValue || endTime.HasValue ? AvailabilityService.Validate(day, startTime, endTime, DateTime.Now) : null);
            bool? available = null;
            if (error == null && startTime.HasValue && endTime.HasValue)
                available = venue.Status == VenueStatus.Available && !new BookingService(Db).HasConflict(id, day, startTime.Value, endTime.Value);
            var reviews = Db.VenueReviews.Where(r => r.Booking.VenueId == id);
            return View(new VenueDetailsViewModel { Venue = venue, Date = day, BookedSlots = booked, FreeSlots = free,
                ReviewCount = reviews.Count(), AverageRating = reviews.Select(r => (double?)r.Rating).Average(),
                Reviews = reviews.OrderByDescending(r => r.Id).Take(20).Select(r => new PublicReviewViewModel { Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt }).ToList(),
                StartTime = startTime, EndTime = endTime, AvailabilityError = error, IsAvailable = available });
        }
    }
}
