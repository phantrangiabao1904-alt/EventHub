using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using EventHub.Models;
using EventHub.Security;
using EventHub.Services;
using EventHub.ViewModels;

namespace EventHub.Controllers
{
     
    [AppAuthorize(Roles = AppRoles.Client)]
    public class BookingsController : BaseController
    {
        public ActionResult Index()
        {
            var uid = CurrentUserId;
            var list = Db.Bookings.Include(b => b.Venue)
                .Where(b => b.UserId == uid)
                .OrderByDescending(b => b.CreatedAt)
                .ToList();
            return View(list);
        }

        public ActionResult Details(int id)
        {
            var uid = CurrentUserId;
            var booking = Db.Bookings.Include(b => b.Venue).Include(b => b.User)
                .FirstOrDefault(b => b.Id == id && b.UserId == uid);
            if (booking == null) return HttpNotFound();
            return View(new BookingDetailsViewModel {
                Booking = booking,
                History = Db.BookingStatusChanges.Where(h => h.BookingId == id).OrderBy(h => h.Id).ToList(),
                Services = Db.BookingServiceItems.Where(s => s.BookingId == id).ToList(),
                Payments = Db.PaymentEntries.Where(p => p.BookingId == id).OrderBy(p => p.Id).ToList(),
                Review = Db.VenueReviews.SingleOrDefault(r => r.BookingId == id)
            });
        }

        [HttpGet]
        public ActionResult Create(int venueId, System.DateTime? eventDate, System.TimeSpan? startTime, System.TimeSpan? endTime)
        {
            var venue = Db.Venues.Find(venueId);
            if (venue == null) return HttpNotFound();
            if (venue.Status != VenueStatus.Available)
            {
                Error("Địa điểm này đang bảo trì, tạm thời không nhận đặt chỗ.");
                return RedirectToAction("Details", "Venues", new { id = venueId });
            }
            var vm = new BookingCreateViewModel { VenueId = venueId };
            if (eventDate.HasValue) vm.EventDate = eventDate.Value.Date;
            if (startTime.HasValue) vm.StartTime = startTime.Value;
            if (endTime.HasValue) vm.EndTime = endTime.Value;
            var error = AvailabilityService.Validate(vm.EventDate, vm.StartTime, vm.EndTime, System.DateTime.Now);
            if (error != null) ModelState.AddModelError("", error);
            return View(Fill(vm, venue));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(BookingCreateViewModel vm)
        {
            var venue = Db.Venues.Find(vm.VenueId);
            if (venue == null) return HttpNotFound();

            if (ModelState.IsValid)
            {
                var result = new BookingService(Db).Create(CurrentUserId, vm.VenueId, vm.EventDate, vm.StartTime, vm.EndTime, vm.GuestCount, vm.Notes, vm.Services);
                if (result.Success)
                {
                    Notifier.Received(Db, result.Booking.Id);
                    Success("Đã gửi yêu cầu thuê. Vui lòng chờ nhân viên duyệt và gửi hướng dẫn cọc; đơn chưa đặt thành công.");
                    return RedirectToAction("Details", new { id = result.Booking.Id });
                }
                foreach (var err in result.Errors) ModelState.AddModelError(err.Key, err.Value);
            }
            return View(Fill(vm, venue));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var result = new BookingService(Db).CancelByClient(id, CurrentUserId);
            if (result.Success)
            {
                Notifier.Cancelled(Db, id, true);
                Success("Đã hủy đơn đặt chỗ.");
            }
            else Error(result.FirstMessage());
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Review(ReviewViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            if (!Db.Bookings.Any(b => b.Id == vm.BookingId && b.UserId == CurrentUserId)) return HttpNotFound();
            var error = ModelState.IsValid ? new BookingExtrasService(Db).Review(CurrentUserId, vm) : "Đánh giá không hợp lệ.";
            if (error == null) Success("Cảm ơn bạn đã đánh giá địa điểm!"); else Error(error);
            return RedirectToAction("Details", new { id = vm.BookingId });
        }

        private BookingCreateViewModel Fill(BookingCreateViewModel vm, Venue v)
        {
            vm.DepositPercent = new DepositService(Db).Settings().Percent;
            var submitted = vm.Services ?? new System.Collections.Generic.List<ServiceSelectionViewModel>();
            vm.Services = Db.ServiceOptions.Where(s => s.IsActive).OrderBy(s => s.Name).ToList().Select(s => {
                var choice = submitted.FirstOrDefault(x => x.ServiceOptionId == s.Id);
                return new ServiceSelectionViewModel { ServiceOptionId = s.Id, Name = s.Name, Description = s.Description,
                    UnitPrice = s.UnitPrice, Selected = choice != null && choice.Selected, Quantity = choice == null ? 1 : choice.Quantity };
            }).ToList();
             
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Services[", System.StringComparison.Ordinal)).ToList()) ModelState.Remove(key);
            vm.VenueName = v.Name;
            vm.VenueImageUrl = v.ImageUrl;
            vm.VenueCapacity = v.Capacity;
            vm.VenuePrice = v.Price;
            return vm;
        }
    }
}
