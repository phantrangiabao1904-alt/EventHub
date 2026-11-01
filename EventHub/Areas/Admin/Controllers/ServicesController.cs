using System;
using System.Linq;
using System.Web.Mvc;
using EventHub.Controllers;
using EventHub.Models;
using EventHub.Security;

namespace EventHub.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = AppRoles.Staff)]
    public class ServicesController : BaseController
    {
        public ActionResult Index() { return View(Db.ServiceOptions.OrderBy(s => s.Name).ToList()); }
        [HttpGet]
        public ActionResult Create() { return View("Form", new ServiceOption { IsActive = true }); }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(ServiceOption vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            vm.Id = 0;
            Validate(vm);
            if (!ModelState.IsValid) return View("Form", vm);
            Db.ServiceOptions.Add(vm);
            Db.SaveChanges();
            Success("Đã thêm dịch vụ.");
            return RedirectToAction("Index");
        }
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var item = Db.ServiceOptions.Find(id);
            return item == null ? (ActionResult)HttpNotFound() : View("Form", item);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, ServiceOption vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            var item = Db.ServiceOptions.Find(id);
            if (item == null) return HttpNotFound();
            vm.Id = id;
            Validate(vm);
            if (!ModelState.IsValid) return View("Form", vm);
            item.Name = vm.Name; item.Description = vm.Description; item.UnitPrice = vm.UnitPrice; item.IsActive = vm.IsActive;
            Db.SaveChanges();
            Success("Đã cập nhật dịch vụ. Đơn đã đặt vẫn giữ giá cũ.");
            return RedirectToAction("Index");
        }
        private void Validate(ServiceOption vm)
        {
            vm.Name = (vm.Name ?? "").Trim();
            if (vm.Name.Length == 0) ModelState.AddModelError("Name", "Vui lòng nhập tên dịch vụ.");
            if (vm.UnitPrice != decimal.Truncate(vm.UnitPrice)) ModelState.AddModelError("UnitPrice", "Nhập giá nguyên VND.");
            if (Db.ServiceOptions.Any(s => s.Name == vm.Name && s.Id != vm.Id)) ModelState.AddModelError("Name", "Tên dịch vụ đã tồn tại.");
        }
    }
}
