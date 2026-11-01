using System;
using System.Data;
using System.Linq;
using EventHub.Data;
using EventHub.Models;
using EventHub.ViewModels;

namespace EventHub.Services
{
    public class BookingExtrasService
    {
        private readonly EventHubContext db;
        public BookingExtrasService(EventHubContext context) { db = context; }

        public string ConfirmDeposit(int actorId, int bookingId, Guid requestId, out bool newlyConfirmed)
        { return ConfirmPayment(actorId, bookingId, requestId, "deposit", out newlyConfirmed); }

        public string ConfirmBalance(int actorId, int bookingId, Guid requestId)
        { bool ignored; return ConfirmPayment(actorId, bookingId, requestId, "balance", out ignored); }

        public string ConfirmRefund(int actorId, int bookingId, Guid requestId)
        { bool ignored; return ConfirmPayment(actorId, bookingId, requestId, "refund", out ignored); }

        private string ConfirmPayment(int actorId, int bookingId, Guid requestId, string operation, out bool newlyConfirmed)
        {
            newlyConfirmed = false;
            if (requestId == Guid.Empty) return "Phiên xác nhận không hợp lệ. Vui lòng tải lại trang.";
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var actor = db.Users.Find(actorId);
                if (actor == null || (actor.Role != Role.Admin && actor.Role != Role.Coordinator)) return "Không có quyền xác nhận thanh toán.";
                var booking = db.Bookings.Find(bookingId);
                if (booking == null) return "Không tìm thấy đơn.";
                db.Entry(booking).Reload();
                var prefix = operation == "deposit" ? "COC-" : operation == "balance" ? "THU-" : "HOAN-";
                var existing = db.PaymentEntries.SingleOrDefault(p => p.RequestId == requestId);
                if (existing != null) return existing.BookingId == bookingId && existing.Reference.StartsWith(prefix) ? null : "Yêu cầu xác nhận đã được sử dụng.";
                var paid = db.PaymentEntries.Where(p => p.BookingId == bookingId)
                    .Select(p => (decimal?)(p.Type == PaymentEntryType.Receipt ? p.Amount : -p.Amount)).Sum() ?? 0;
                decimal amount;
                if (operation == "deposit")
                {
                    if (booking.Status == BookingStatus.Confirmed && booking.DepositRequestedAt.HasValue && booking.RequiredDeposit.HasValue && paid >= booking.RequiredDeposit.Value) return null;
                    if (booking.Status != BookingStatus.AwaitingDeposit || !booking.DepositRequestedAt.HasValue || !booking.RequiredDeposit.HasValue)
                        return "Hãy duyệt yêu cầu và gửi QR trước khi xác nhận nhận cọc.";
                    if (booking.StartsAt() <= DateTime.Now) return "Sự kiện đã bắt đầu, không thể xác nhận cọc muộn.";
                    amount = Math.Max(0, booking.RequiredDeposit.Value - paid);
                    if (amount > booking.TotalPrice - paid) return "Số tiền cọc không hợp lệ.";
                    booking.Status = BookingStatus.Confirmed;
                    db.BookingStatusChanges.Add(new BookingStatusChange { BookingId = bookingId, FromStatus = BookingStatus.AwaitingDeposit,
                        ToStatus = BookingStatus.Confirmed, ChangedAt = DateTime.Now, ActorName = actor.FullName });
                }
                else if (operation == "balance")
                {
                    if (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.Completed) return "Đơn chưa đặt thành công hoặc đã hủy.";
                    amount = Math.Max(0, booking.TotalPrice - paid);
                }
                else
                {
                    if (booking.Status != BookingStatus.Cancelled) return "Hãy hủy đơn trước khi xác nhận hoàn tiền.";
                    amount = paid;
                }
                if (amount > 0)
                    db.PaymentEntries.Add(new PaymentEntry { BookingId = bookingId,
                        Type = operation == "refund" ? PaymentEntryType.Refund : PaymentEntryType.Receipt, Amount = amount,
                        RequestId = requestId, Reference = prefix + bookingId + "-" + requestId.ToString("N"),
                        RecordedBy = actor.FullName, RecordedAt = DateTime.Now,
                        Notes = operation == "deposit" ? "Nhân viên xác nhận đã nhận đủ cọc." : operation == "balance" ? "Nhân viên xác nhận đã nhận đủ tiền còn lại." : "Nhân viên xác nhận đã hoàn tiền." });
                db.SaveChanges(); tx.Commit();
                newlyConfirmed = operation == "deposit";
                return null;
            }
        }

        public string Review(int userId, ReviewViewModel vm)
        {
            if (vm == null || vm.Rating < 1 || vm.Rating > 5 || (vm.Comment ?? "").Length > 1000)
                return "Đánh giá phải từ 1 đến 5 sao, nhận xét tối đa 1000 ký tự.";
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var booking = db.Bookings.SingleOrDefault(b => b.Id == vm.BookingId && b.UserId == userId);
                if (booking == null) return "Không tìm thấy đơn của bạn.";
                if (booking.Status != BookingStatus.Completed) return "Chỉ được đánh giá sau khi đơn đã hoàn thành.";
                if (db.VenueReviews.Any(r => r.BookingId == booking.Id)) return "Bạn đã đánh giá đơn này.";
                db.VenueReviews.Add(new VenueReview { BookingId = booking.Id, Rating = vm.Rating,
                    Comment = (vm.Comment ?? "").Trim(), CreatedAt = DateTime.Now });
                db.SaveChanges();
                tx.Commit();
                return null;
            }
        }

        public string RecordPayment(int actorId, RecordPaymentViewModel vm)
        {
            bool confirmed;
            return RecordPayment(actorId, vm, out confirmed);
        }

        public string RecordPayment(int actorId, RecordPaymentViewModel vm, out bool newlyConfirmed)
        {
            newlyConfirmed = false;
            if (vm == null || vm.RequestId == Guid.Empty || vm.Amount <= 0 || vm.Amount != decimal.Truncate(vm.Amount)
                || !Enum.IsDefined(typeof(PaymentEntryType), vm.Type) || string.IsNullOrWhiteSpace(vm.Reference)
                || vm.Reference.Length > 100 || (vm.Notes ?? "").Length > 500)
                return "Thông tin thu/hoàn tiền không hợp lệ. Nhập số tiền nguyên VND và mã tham chiếu.";
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var actor = db.Users.Find(actorId);
                if (actor == null || (actor.Role != Role.Admin && actor.Role != Role.Coordinator)) return "Không có quyền ghi nhận thanh toán.";
                var booking = db.Bookings.Find(vm.BookingId);
                if (booking == null) return "Không tìm thấy đơn đặt chỗ.";
                db.Entry(booking).Reload();
                var existing = db.PaymentEntries.SingleOrDefault(p => p.RequestId == vm.RequestId);
                if (existing != null)
                    return existing.BookingId == vm.BookingId && existing.Type == vm.Type && existing.Amount == vm.Amount
                        && existing.Reference == vm.Reference.Trim() ? null : "Phiếu đã được xử lý với thông tin khác. Hãy tải lại trang.";
                var reference = vm.Reference.Trim();
                if (db.PaymentEntries.Any(p => p.BookingId == vm.BookingId && p.Type == vm.Type && p.Reference == reference))
                    return "Mã giao dịch/chứng từ đã được ghi nhận cho đơn này.";
                var paid = db.PaymentEntries.Where(p => p.BookingId == booking.Id)
                    .Select(p => (decimal?)(p.Type == PaymentEntryType.Receipt ? p.Amount : -p.Amount)).Sum() ?? 0;
                if (vm.Type == PaymentEntryType.Receipt && booking.Status == BookingStatus.Cancelled) return "Không thể thu tiền cho đơn đã hủy.";
                if (vm.Type == PaymentEntryType.Receipt && booking.Status == BookingStatus.Pending) return "Hãy duyệt yêu cầu và gửi QR cọc trước khi ghi nhận tiền.";
                if (vm.Type == PaymentEntryType.Receipt && booking.Status == BookingStatus.AwaitingDeposit && booking.StartsAt() <= DateTime.Now)
                    return "Sự kiện đã bắt đầu. Vui lòng xử lý lại đơn thay vì xác nhận cọc muộn.";
                if (vm.Type == PaymentEntryType.Receipt && vm.Amount > booking.TotalPrice - paid) return "Số tiền thu vượt quá số tiền còn phải thu.";
                if (vm.Type == PaymentEntryType.Refund && vm.Amount > paid) return "Số tiền hoàn vượt quá số tiền đã thu còn lại.";
                if (vm.Type == PaymentEntryType.Refund && booking.Status == BookingStatus.Confirmed && paid - vm.Amount < (booking.RequiredDeposit ?? 0))
                    return "Khoản hoàn làm thiếu tiền cọc. Hãy hủy đơn trước khi hoàn tiền cọc.";
                db.PaymentEntries.Add(new PaymentEntry { BookingId = booking.Id, Type = vm.Type, Amount = vm.Amount,
                    Reference = vm.Reference.Trim(), Notes = (vm.Notes ?? "").Trim(), RequestId = vm.RequestId,
                    RecordedBy = actor.FullName, RecordedAt = DateTime.Now });
                var confirm = vm.Type == PaymentEntryType.Receipt && booking.Status == BookingStatus.AwaitingDeposit
                    && booking.DepositRequestedAt.HasValue && booking.RequiredDeposit.HasValue && paid + vm.Amount >= booking.RequiredDeposit.Value;
                if (confirm)
                {
                    booking.Status = BookingStatus.Confirmed;
                    db.BookingStatusChanges.Add(new BookingStatusChange { BookingId = booking.Id, FromStatus = BookingStatus.AwaitingDeposit,
                        ToStatus = BookingStatus.Confirmed, ChangedAt = DateTime.Now, ActorName = actor.FullName });
                }
                db.SaveChanges();
                tx.Commit();
                newlyConfirmed = confirm;
                return null;
            }
        }
    }
}
