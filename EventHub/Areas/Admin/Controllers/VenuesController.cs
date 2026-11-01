using System;
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
    public class VenuesController : BaseController
    {
        public ActionResult Index(string search, VenueStatus? status)
        {
            var q = Db.Venues.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(v => v.Name.Contains(s) || v.Address.Contains(s));
            }
            if (status.HasValue)
            {
                var st = status.Value;
                q = q.Where(v => v.Status == st);
            }
            ViewData["Search"] = search;
            ViewData["Status"] = status.HasValue ? status.Value.ToString() : "";
            return View(q.OrderByDescending(v => v.Id).ToList());
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View("Form", new VenueFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(VenueFormViewModel vm, HttpPostedFileBase imageFile)
        {
            Validate(vm);
            if (!ModelState.IsValid) return View("Form", vm);
            if (!SaveImage(vm, imageFile)) return View("Form", vm);

            Db.Venues.Add(new Venue
            {
                Name = vm.Name.Trim(),
                Description = vm.Description,
                Address = vm.Address.Trim(),
                Capacity = vm.Capacity,
                Price = vm.Price,
                Status = vm.Status,
                ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim()
            });
            Db.SaveChanges();
            Success("Đã thêm địa điểm mới.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var v = Db.Venues.Find(id);
            if (v == null) return HttpNotFound();
            return View("Form", new VenueFormViewModel
            {
                Id = v.Id, Name = v.Name, Description = v.Description, Address = v.Address,
                Capacity = v.Capacity, Price = v.Price, Status = v.Status, ImageUrl = v.ImageUrl
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, VenueFormViewModel vm, HttpPostedFileBase imageFile)
        {
            var v = Db.Venues.Find(id);
            if (v == null) return HttpNotFound();

            vm.Id = id;
            Validate(vm);
            if (!ModelState.IsValid) return View("Form", vm);
            if (!SaveImage(vm, imageFile)) return View("Form", vm);

            var wasAvailable = v.Status == VenueStatus.Available;
            v.Name = vm.Name.Trim();
            v.Description = vm.Description;
            v.Address = vm.Address.Trim();
            v.Capacity = vm.Capacity;
            v.Price = vm.Price;
            v.Status = vm.Status;
            v.ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim();
            Db.SaveChanges();

            Success("Đã cập nhật địa điểm.");
            if (wasAvailable && v.Status == VenueStatus.Maintenance)
            {
                var today = DateTime.Today;
                var upcoming = Db.Bookings.Count(b => b.VenueId == id && b.EventDate >= today
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.AwaitingDeposit || b.Status == BookingStatus.Confirmed));
                if (upcoming > 0)
                    Warning("Địa điểm chuyển sang bảo trì nhưng còn " + upcoming + " đơn đặt chỗ sắp tới. Hãy xem lại và liên hệ khách hàng.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var v = Db.Venues.Find(id);
            if (v == null) return HttpNotFound();

            if (Db.Bookings.Any(b => b.VenueId == id))
            {
                Error("Không thể xóa: địa điểm này đã có đơn đặt chỗ. Hãy chuyển sang trạng thái \"Đang bảo trì\" thay vì xóa.");
                return RedirectToAction("Index");
            }
            Db.Venues.Remove(v);
            Db.SaveChanges();
            Success("Đã xóa địa điểm.");
            return RedirectToAction("Index");
        }

        private void Validate(VenueFormViewModel vm)
        {
            if (!Enum.IsDefined(typeof(VenueStatus), vm.Status))
                ModelState.AddModelError("Status", "Trạng thái không hợp lệ.");
            if (string.IsNullOrWhiteSpace(vm.Name)) return;
            var name = vm.Name.Trim();
            if (Db.Venues.Any(x => x.Name == name && x.Id != vm.Id))
                ModelState.AddModelError("Name", "Tên địa điểm này đã tồn tại.");
        }

        private bool SaveImage(VenueFormViewModel vm, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return true;
            try
            {
                vm.ImageUrl = VenueImageStorage.Save(file, Server.MapPath("~/Uploads/Venues"));
                ModelState.Remove("ImageUrl");
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.IO.IOException
                || ex is System.Runtime.InteropServices.ExternalException || ex is OutOfMemoryException
                || ex is UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceError("Lưu ảnh địa điểm thất bại: " + ex.Message);
                ModelState.AddModelError("imageFile", "Không lưu được ảnh. Chọn ảnh JPEG/PNG tối đa 3 MB, 8000 pixel mỗi chiều và 24 megapixel.");
                return false;
            }
        }
    }
}
