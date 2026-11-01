using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using EventHub.Controllers;
using EventHub.Models;
using EventHub.Security;
using EventHub.Services;
using EventHub.ViewModels;

namespace EventHub.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = AppRoles.Staff)]
    public class BookingsController : BaseController
    {
        public ActionResult Index(string search, BookingStatus? status, System.DateTime? from, System.DateTime? to)
        {
            var q = Db.Bookings.Include(b => b.Venue).Include(b => b.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                int id;
                var isId = int.TryParse(s.TrimStart('#'), out id);
                q = q.Where(b => b.Venue.Name.Contains(s) || b.User.FullName.Contains(s) || b.User.Email.Contains(s) || (isId && b.Id == id));
            }
            if (status.HasValue)
            {
                var st = status.Value;
                q = q.Where(b => b.Status == st);
            }
            if (from.HasValue)
            {
                var f = from.Value.Date;
                q = q.Where(b => b.EventDate >= f);
            }
            if (to.HasValue)
            {
                var t = to.Value.Date;
                q = q.Where(b => b.EventDate <= t);
            }

             
            var items = q.OrderBy(b => b.Status == BookingStatus.Pending ? 0 : b.Status == BookingStatus.AwaitingDeposit ? 1 : b.Status == BookingStatus.Confirmed ? 2 : 3)
                .ThenBy(b => b.EventDate).ThenBy(b => b.StartTime).ToList();

            return View(new BookingFilterViewModel { Search = search, Status = status, From = from, To = to, Items = items });
        }

        public ActionResult Details(int id)
        {
            var b = Db.Bookings.Include(x => x.Venue).Include(x => x.User).FirstOrDefault(x => x.Id == id);
            if (b == null) return HttpNotFound();
            ViewBag.History = Db.BookingStatusChanges.Where(h => h.BookingId == id).OrderBy(h => h.Id).ToList();
            var defaults = new DepositService(Db).Settings();
            ViewBag.DepositRequest = new DepositRequestViewModel { BookingId = b.Id,
                Percent = b.DepositRequestedAt.HasValue ? b.DepositPercent ?? defaults.Percent : defaults.Percent,
                QrImageUrl = b.DepositQrImageUrl ?? defaults.QrImageUrl, BankName = b.DepositBankName ?? defaults.BankName,
                AccountNumber = b.DepositAccountNumber ?? defaults.AccountNumber, AccountHolder = b.DepositAccountHolder ?? defaults.AccountHolder,
                Instructions = b.DepositInstructions ?? defaults.Instructions };
            ViewBag.Extras = new BookingDetailsViewModel { Booking = b,
                Services = Db.BookingServiceItems.Where(s => s.BookingId == id).ToList(),
                Payments = Db.PaymentEntries.Where(p => p.BookingId == id).OrderBy(p => p.Id).ToList() };
            return View(b);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SendDepositRequest(DepositRequestViewModel vm, System.Web.HttpPostedFileBase qrFile)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            var booking = Db.Bookings.Find(vm.BookingId);
            if (booking == null) return HttpNotFound();
            if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.AwaitingDeposit)
            { Error("Đơn không còn ở trạng thái có thể gửi yêu cầu cọc."); return RedirectToAction("Details", new { id = vm.BookingId }); }
            if (!ModelState.IsValid) { Error("Thông tin cọc không hợp lệ."); return RedirectToAction("Details", new { id = vm.BookingId }); }
            vm.QrImageUrl = booking.DepositQrImageUrl ?? new DepositService(Db).Settings().QrImageUrl;
            try
            {
                if (qrFile != null && qrFile.ContentLength > 0)
                    vm.QrImageUrl = VenueImageStorage.Save(qrFile, Server.MapPath("~/Uploads/PaymentQr"), "/Uploads/PaymentQr");
            }
            catch (System.Exception ex) when (ex is System.ArgumentException || ex is System.IO.IOException || ex is System.OutOfMemoryException
                || ex is System.Runtime.InteropServices.ExternalException || ex is System.UnauthorizedAccessException)
            { Error("Không lưu được QR. Chọn ảnh JPEG/PNG tối đa 3 MB."); return RedirectToAction("Details", new { id = vm.BookingId }); }
            if (!DepositService.ValidQrPath(vm.QrImageUrl) || !System.IO.File.Exists(Server.MapPath("~" + vm.QrImageUrl)))
            { Error("Hãy tải QR tại đây hoặc cấu hình QR mặc định trên Dashboard trước."); return RedirectToAction("Details", new { id = vm.BookingId }); }
            var error = new DepositService(Db).Request(CurrentUserId, vm);
            if (error != null) Error(error);
            else if (Notifier.DepositRequested(Db, vm.BookingId)) Success("Đã duyệt yêu cầu và gửi QR cọc qua email. Đơn đang chờ nhận cọc, chưa đặt thành công.");
            else Warning("Đã duyệt và lưu QR trên trang chi tiết đơn của khách, nhưng email chưa gửi được. Kiểm tra SMTP rồi bấm gửi lại QR.");
            return RedirectToAction("Details", new { id = vm.BookingId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmDeposit(int id, System.Guid requestId)
        {
            bool confirmed;
            var error = new BookingExtrasService(Db).ConfirmDeposit(CurrentUserId, id, requestId, out confirmed);
            if (error != null) Error(error);
            else
            {
                if (confirmed) { Notifier.Confirmed(Db, id); Success("Đã xác nhận nhận đủ cọc. Đơn đã đặt thành công."); }
                else Success("Yêu cầu xác nhận đã được xử lý. Không ghi nhận tiền trùng lần nữa.");
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmBalance(int id, System.Guid requestId)
        {
            var error = new BookingExtrasService(Db).ConfirmBalance(CurrentUserId, id, requestId);
            if (error != null) Error(error); else Success("Đã xác nhận nhận đủ tiền của đơn.");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmRefund(int id, System.Guid requestId)
        {
            var error = new BookingExtrasService(Db).ConfirmRefund(CurrentUserId, id, requestId);
            if (error != null) Error(error); else Success("Đã ghi nhận xác nhận hoàn tiền.");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RecordPayment(RecordPaymentViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            var confirmed = false;
            var error = ModelState.IsValid ? new BookingExtrasService(Db).RecordPayment(CurrentUserId, vm, out confirmed) : "Vui lòng nhập đủ thông tin và số tiền hợp lệ.";
            if (error == null)
            {
                if (confirmed) { Notifier.Confirmed(Db, vm.BookingId); Success("Đã xác nhận nhận đủ cọc. Đơn đã đặt thành công; email thông báo đã được đưa vào hàng gửi."); }
                else Success("Đã ghi nhận thu/hoàn tiền. Nếu chưa đủ cọc, đơn vẫn ở trạng thái chờ cọc.");
            }
            else Error(error);
            return RedirectToAction("Details", new { id = vm.BookingId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChangeStatus(int id, BookingStatus status, string returnUrl)
        {
            var result = new BookingService(Db).ChangeStatus(id, status, CurrentUserId);
            if (result.Success)
            {
                if (status == BookingStatus.Confirmed) Notifier.Confirmed(Db, id);
                else if (status == BookingStatus.Cancelled) Notifier.Cancelled(Db, id, false);
                Success("Đã cập nhật trạng thái đơn #" + id + " thành \"" + Helpers.Vi.BookingStatusName(status) + "\"."
                        + (status == BookingStatus.Confirmed || status == BookingStatus.Cancelled ? " Email thông báo đã được gửi cho khách hàng." : ""));
            }
            else Error(result.FirstMessage());

            return RedirectLocal(returnUrl, RedirectToAction("Index"));
        }
    }
}
