using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using EventHub.Controllers;
using EventHub.Models;
using EventHub.Security;
using EventHub.ViewModels;
using EventHub.Services;
using System.Web;

namespace EventHub.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = AppRoles.Staff)]
    public class DashboardController : BaseController
    {
        public ActionResult Index()
        {
            var today = DateTime.Today;
            var vm = new DashboardViewModel
            {
                TotalVenues = Db.Venues.Count(),
                AvailableVenues = Db.Venues.Count(v => v.Status == VenueStatus.Available),
                TotalClients = Db.Users.Count(u => u.Role == Role.Client),
                TotalBookings = Db.Bookings.Count(),
                Pending = Db.Bookings.Count(b => b.Status == BookingStatus.Pending),
                AwaitingDeposit = Db.Bookings.Count(b => b.Status == BookingStatus.AwaitingDeposit),
                DepositSettings = new DepositService(Db).Settings(),
                Confirmed = Db.Bookings.Count(b => b.Status == BookingStatus.Confirmed),
                Completed = Db.Bookings.Count(b => b.Status == BookingStatus.Completed),
                Cancelled = Db.Bookings.Count(b => b.Status == BookingStatus.Cancelled),
                 
                Revenue = Db.Bookings
                    .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                    .Select(b => (decimal?)b.TotalPrice).Sum() ?? 0m,
                UpcomingBookings = Db.Bookings.Include(b => b.Venue).Include(b => b.User)
                    .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.AwaitingDeposit || b.Status == BookingStatus.Confirmed) && b.EventDate >= today)
                    .OrderBy(b => b.EventDate).ThenBy(b => b.StartTime).Take(5).ToList(),
                RecentBookings = Db.Bookings.Include(b => b.Venue).Include(b => b.User)
                    .OrderByDescending(b => b.CreatedAt).Take(5).ToList()
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken, AppAuthorize(Roles = AppRoles.Admin)]
        public ActionResult SaveDepositSettings(DepositSettingsViewModel vm, HttpPostedFileBase qrFile)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            vm.QrImageUrl = new DepositService(Db).Settings().QrImageUrl;
            if (!ModelState.IsValid) { Error("Tỷ lệ cọc từ 1% đến 100%, thông tin chuyển khoản tối đa độ dài cho phép."); return RedirectToAction("Index"); }
            try
            {
                if (qrFile != null && qrFile.ContentLength > 0)
                    vm.QrImageUrl = VenueImageStorage.Save(qrFile, Server.MapPath("~/Uploads/PaymentQr"), "/Uploads/PaymentQr");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.IO.IOException || ex is OutOfMemoryException
                || ex is System.Runtime.InteropServices.ExternalException || ex is UnauthorizedAccessException)
            { Error("Không lưu được QR. Chọn ảnh JPEG/PNG tối đa 3 MB."); return RedirectToAction("Index"); }
            var error = new DepositService(Db).SaveSettings(CurrentUserId, vm);
            if (error != null) Error(error); else Success("Đã lưu tỷ lệ cọc và QR mặc định. Hướng dẫn đã gửi cho từng đơn được giữ nguyên.");
            return RedirectToAction("Index");
        }
    }
}
