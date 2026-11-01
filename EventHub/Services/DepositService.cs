using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using EventHub.Data;
using EventHub.Helpers;
using EventHub.Models;
using EventHub.ViewModels;

namespace EventHub.Services
{
    public class DepositService
    {
        private readonly EventHubContext db;
        public DepositService(EventHubContext context) { db = context; }
        public static bool ValidQrPath(string path)
        {
            return path != null && Regex.IsMatch(path, @"^/Uploads/PaymentQr/[a-f0-9]{32}\.jpg$");
        }
        public DepositSettingsViewModel Settings()
        {
            var item = db.DepositSettings.AsNoTracking().SingleOrDefault(s => s.Id == 1);
            return item == null ? new DepositSettingsViewModel { Percent = Math.Max(1, Math.Min(100, AppConfig.GetInt("Booking.DepositPercent", 30))) }
                : new DepositSettingsViewModel { Percent = item.Percent, QrImageUrl = item.QrImageUrl, BankName = item.BankName,
                    AccountNumber = item.AccountNumber, AccountHolder = item.AccountHolder, Instructions = item.Instructions };
        }
        public string SaveSettings(int actorId, DepositSettingsViewModel vm)
        {
            var error = Validate(vm, false);
            if (error != null) return error;
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!db.Users.Any(u => u.Id == actorId && u.Role == Role.Admin)) return "Chỉ quản trị viên được đổi cấu hình cọc.";
                var item = db.DepositSettings.SingleOrDefault(s => s.Id == 1);
                if (item == null) { item = new DepositSettings(); db.DepositSettings.Add(item); }
                item.Percent = vm.Percent; item.QrImageUrl = vm.QrImageUrl; item.BankName = (vm.BankName ?? "").Trim();
                item.AccountNumber = (vm.AccountNumber ?? "").Trim(); item.AccountHolder = (vm.AccountHolder ?? "").Trim();
                item.Instructions = (vm.Instructions ?? "").Trim();
                db.SaveChanges(); tx.Commit();
                return null;
            }
        }
        public string Request(int actorId, DepositRequestViewModel vm)
        {
            var error = Validate(vm, true);
            if (error != null) return error;
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!db.Users.Any(u => u.Id == actorId && (u.Role == Role.Admin || u.Role == Role.Coordinator))) return "Không có quyền duyệt yêu cầu.";
                var booking = db.Bookings.Find(vm.BookingId);
                if (booking == null) return "Không tìm thấy đơn.";
                db.Entry(booking).Reload();
                if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.AwaitingDeposit) return "Chỉ gửi yêu cầu cọc cho đơn đang chờ duyệt hoặc chờ cọc.";
                if (booking.StartsAt() <= DateTime.Now) return "Sự kiện đã bắt đầu; không thể gửi yêu cầu cọc.";
                var paid = db.PaymentEntries.Where(p => p.BookingId == booking.Id)
                    .Select(p => (decimal?)(p.Type == PaymentEntryType.Receipt ? p.Amount : -p.Amount)).Sum() ?? 0;
                if (paid > 0 && booking.DepositPercent != vm.Percent) return "Đơn đã nhận một phần tiền, không được đổi tỷ lệ cọc. Bạn vẫn có thể thay QR.";
                var old = booking.Status;
                booking.DepositPercent = vm.Percent;
                booking.RequiredDeposit = Math.Round(booking.TotalPrice * vm.Percent / 100m, 0, MidpointRounding.AwayFromZero);
                booking.DepositQrImageUrl = vm.QrImageUrl;
                booking.DepositBankName = (vm.BankName ?? "").Trim(); booking.DepositAccountNumber = (vm.AccountNumber ?? "").Trim();
                booking.DepositAccountHolder = (vm.AccountHolder ?? "").Trim(); booking.DepositInstructions = (vm.Instructions ?? "").Trim();
                booking.DepositRequestedAt = DateTime.Now;
                booking.Status = BookingStatus.AwaitingDeposit;
                if (old != booking.Status)
                    db.BookingStatusChanges.Add(new BookingStatusChange { BookingId = booking.Id, FromStatus = old, ToStatus = booking.Status,
                        ChangedAt = DateTime.Now, ActorName = db.Users.Where(u => u.Id == actorId).Select(u => u.FullName).Single() });
                db.SaveChanges(); tx.Commit();
                return null;
            }
        }
        private static string Validate(DepositSettingsViewModel vm, bool requireQr)
        {
            if (vm == null || vm.Percent < 1 || vm.Percent > 100 || decimal.Round(vm.Percent, 2) != vm.Percent)
                return "Tỷ lệ cọc từ 1% đến 100%, tối đa 2 chữ số thập phân.";
            if ((requireQr || !string.IsNullOrEmpty(vm.QrImageUrl)) && !ValidQrPath(vm.QrImageUrl)) return "Vui lòng tải ảnh QR cọc hợp lệ.";
            if ((vm.BankName ?? "").Length > 100 || (vm.AccountNumber ?? "").Length > 50 || (vm.AccountHolder ?? "").Length > 100 || (vm.Instructions ?? "").Length > 1000)
                return "Thông tin chuyển khoản quá dài.";
            return null;
        }
    }
}
