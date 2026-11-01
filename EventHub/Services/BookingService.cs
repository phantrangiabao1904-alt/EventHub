using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EventHub.Data;
using EventHub.Models;
using EventHub.ViewModels;
using EventHub.Helpers;

namespace EventHub.Services
{
    public class BookingResult
    {
        public List<KeyValuePair<string, string>> Errors = new List<KeyValuePair<string, string>>();
        public Booking Booking;

        public bool Success { get { return Errors.Count == 0; } }
        public void Add(string field, string message) { Errors.Add(new KeyValuePair<string, string>(field ?? "", message)); }
        public string FirstMessage() { return Errors.Count == 0 ? "" : Errors[0].Value; }
    }

     
    public class BookingService
    {
        private readonly EventHubContext _db;
        public BookingService(EventHubContext db) { _db = db; }

         
        public static bool CanTransition(BookingStatus from, BookingStatus to)
        {
            switch (from)
            {
                case BookingStatus.Pending: return to == BookingStatus.AwaitingDeposit || to == BookingStatus.Cancelled;
                case BookingStatus.AwaitingDeposit: return to == BookingStatus.Confirmed || to == BookingStatus.Cancelled;
                case BookingStatus.Confirmed: return to == BookingStatus.Completed || to == BookingStatus.Cancelled;
                default: return false;  
            }
        }

         
        public bool HasConflict(int venueId, DateTime date, TimeSpan start, TimeSpan end)
        {
            var d = date.Date;
            return _db.Bookings.Any(b => b.VenueId == venueId
                                         && b.EventDate == d
                                         && b.Status != BookingStatus.Cancelled
                                         && b.StartTime < end
                                         && b.EndTime > start);
        }

        public BookingResult Create(int userId, int venueId, DateTime eventDate, TimeSpan start, TimeSpan end, int guests, string notes,
            IEnumerable<ServiceSelectionViewModel> services = null)
        {
            var result = new BookingResult();
            var venue = _db.Venues.Find(venueId);
            if (venue == null) { result.Add("", "Địa điểm không tồn tại."); return result; }

            if (venue.Status != VenueStatus.Available)
                result.Add("", "Địa điểm này đang bảo trì, tạm thời không nhận đặt chỗ.");

            var now = DateTime.Now;
            var date = eventDate.Date;
            if (date < now.Date)
                result.Add("EventDate", "Ngày tổ chức không được ở trong quá khứ.");
            else if (date == now.Date && start <= now.TimeOfDay)
                result.Add("StartTime", "Giờ bắt đầu phải sau thời điểm hiện tại.");

            if (start < TimeSpan.Zero || end >= TimeSpan.FromDays(1) || end <= start)
                result.Add("EndTime", "Giờ kết thúc phải sau giờ bắt đầu và trong cùng ngày.");

            if (guests < 1)
                result.Add("GuestCount", "Số khách phải từ 1 trở lên.");
            else if (guests > venue.Capacity)
                result.Add("GuestCount", "Địa điểm này chỉ chứa tối đa " + venue.Capacity + " khách.");

            if (!result.Success) return result;
            var selected = (services ?? Enumerable.Empty<ServiceSelectionViewModel>()).Where(s => s.Selected).ToList();
            if (selected.Count > 50 || selected.Any(s => s.Quantity < 1 || s.Quantity > 10000)
                || selected.Select(s => s.ServiceOptionId).Distinct().Count() != selected.Count)
            { result.Add("", "Danh sách dịch vụ hoặc số lượng không hợp lệ."); return result; }

             
            using (var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                _db.Entry(venue).Reload();
                if (venue.Status != VenueStatus.Available)
                { result.Add("", "Địa điểm đã chuyển sang bảo trì."); return result; }
                if (guests > venue.Capacity)
                { result.Add("GuestCount", "Sức chứa địa điểm đã thay đổi. Vui lòng giảm số khách."); return result; }
                var ids = selected.Select(s => s.ServiceOptionId).ToList();
                var options = _db.ServiceOptions.Where(s => ids.Contains(s.Id) && s.IsActive).ToList();
                if (options.Count != selected.Count)
                { result.Add("", "Có dịch vụ đã ngừng cung cấp. Vui lòng chọn lại."); return result; }
                var extras = selected.Select(s => {
                    var option = options.Single(o => o.Id == s.ServiceOptionId);
                    return new BookingServiceItem { ServiceOptionId = option.Id, Name = option.Name, UnitPrice = option.UnitPrice, Quantity = s.Quantity };
                }).ToList();
                var total = venue.Price + extras.Sum(s => s.UnitPrice * s.Quantity);
                var depositPercent = new DepositService(_db).Settings().Percent;
                if (HasConflict(venue.Id, date, start, end))
                {
                    result.Add("", "Địa điểm đã có lịch đặt trùng khung giờ này. Vui lòng chọn thời gian khác.");
                    return result;
                }

                var booking = new Booking
                {
                    UserId = userId,
                    VenueId = venue.Id,
                    EventDate = date,
                    StartTime = start,
                    EndTime = end,
                    GuestCount = guests,
                    VenueRentalPrice = venue.Price,
                    TotalPrice = total,
                    RequiredDeposit = Math.Round(total * depositPercent / 100m, 0, MidpointRounding.AwayFromZero),
                    DepositPercent = depositPercent,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    Status = BookingStatus.Pending
                };
                _db.Bookings.Add(booking);
                _db.SaveChanges();
                foreach (var item in extras) { item.BookingId = booking.Id; _db.BookingServiceItems.Add(item); }
                AddHistory(booking, null, userId);
                _db.SaveChanges();
                tx.Commit();
                result.Booking = booking;
            }
            return result;
        }

         
        public BookingResult CancelByClient(int bookingId, int userId)
        {
            using (var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var result = new BookingResult();
                var b = _db.Bookings.Find(bookingId);
                if (b != null) _db.Entry(b).Reload();
                if (b == null || b.UserId != userId) { result.Add("", "Không tìm thấy đơn đặt chỗ."); return result; }
                if (!CanTransition(b.Status, BookingStatus.Cancelled)) { result.Add("", "Đơn này không thể hủy ở trạng thái hiện tại."); return result; }
                if (b.StartsAt() <= DateTime.Now) { result.Add("", "Sự kiện đã bắt đầu hoặc đã diễn ra nên không thể hủy."); return result; }

                var previous = b.Status;
                b.Status = BookingStatus.Cancelled;
                AddHistory(b, previous, userId);
                _db.SaveChanges();
                tx.Commit();
                result.Booking = b;
                return result;
            }
        }

         
        public BookingResult ChangeStatus(int bookingId, BookingStatus to, int actorId)
        {
            using (var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var result = new BookingResult();
                var actor = _db.Users.Find(actorId);
                if (actor == null || (actor.Role != Role.Admin && actor.Role != Role.Coordinator))
                { result.Add("", "Không có quyền cập nhật trạng thái."); return result; }
                var b = _db.Bookings.Find(bookingId);
                if (b != null) _db.Entry(b).Reload();
                if (b == null) { result.Add("", "Không tìm thấy đơn đặt chỗ."); return result; }

                if (to == BookingStatus.AwaitingDeposit)
                { result.Add("", "Hãy dùng chức năng Duyệt và gửi QR cọc để gửi hướng dẫn cho khách."); return result; }
                if (to == BookingStatus.Confirmed)
                {
                    var paid = _db.PaymentEntries.Where(p => p.BookingId == b.Id)
                        .Select(p => (decimal?)(p.Type == PaymentEntryType.Receipt ? p.Amount : -p.Amount)).Sum() ?? 0;
                    if (!b.DepositRequestedAt.HasValue || !b.RequiredDeposit.HasValue || paid < b.RequiredDeposit.Value)
                    { result.Add("", "Chưa nhận đủ cọc. Hãy đối chiếu giao dịch và ghi nhận tiền cọc trước."); return result; }
                }

                if (!CanTransition(b.Status, to))
                {
                    result.Add("", "Đơn #" + b.Id + " đang ở trạng thái \"" + Helpers.Vi.BookingStatusName(b.Status)
                                   + "\", không thể chuyển sang \"" + Helpers.Vi.BookingStatusName(to) + "\".");
                    return result;
            }
            if (to == BookingStatus.Confirmed && b.StartsAt() <= DateTime.Now)
            {
                result.Add("", "Không thể xác nhận đơn có thời gian bắt đầu đã qua. Hãy hủy đơn này.");
                return result;
            }
            if (to == BookingStatus.Completed && b.StartsAt() > DateTime.Now)
            {
                result.Add("", "Chỉ có thể đánh dấu hoàn thành sau khi sự kiện đã bắt đầu.");
                return result;
            }

            var previous = b.Status;
            b.Status = to;
            AddHistory(b, previous, actorId);
            _db.SaveChanges();
            tx.Commit();
            result.Booking = b;
            return result;
            }
        }

        private void AddHistory(Booking booking, BookingStatus? from, int actorId)
        {
            var actorName = _db.Users.Where(u => u.Id == actorId).Select(u => u.FullName).FirstOrDefault() ?? "Hệ thống";
            _db.BookingStatusChanges.Add(new BookingStatusChange {
                BookingId = booking.Id, FromStatus = from, ToStatus = booking.Status,
                ChangedAt = DateTime.Now, ActorName = actorName
            });
        }
    }
}
