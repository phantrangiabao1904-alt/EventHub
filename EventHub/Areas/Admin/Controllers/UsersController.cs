using System.Linq;
using System.Web.Mvc;
using EventHub.Controllers;
using EventHub.Models;
using EventHub.Security;
using EventHub.ViewModels;

namespace EventHub.Areas.Admin.Controllers
{
     
    [AppAuthorize(Roles = AppRoles.Admin)]
    public class UsersController : BaseController
    {
        public ActionResult Index(string search, Role? role)
        {
            var q = Db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(u => u.FullName.Contains(s) || u.Email.Contains(s));
            }
            if (role.HasValue)
            {
                var r = role.Value;
                q = q.Where(u => u.Role == r);
            }

            var items = q.OrderBy(u => u.Role).ThenBy(u => u.FullName)
                .Select(u => new UserRowViewModel
                {
                    Id = u.Id, FullName = u.FullName, Email = u.Email, Role = u.Role,
                    CreatedAt = u.CreatedAt, BookingCount = u.Bookings.Count()
                }).ToList();

            return View(new UserListViewModel { Search = search, Role = role, Items = items });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetRole(int id, Role role)
        {
            if (id == CurrentUserId) { Error("Bạn không thể tự đổi vai trò của chính mình."); return RedirectToAction("Index"); }

            var u = Db.Users.Find(id);
            if (u == null) return HttpNotFound();

            u.Role = role;
            Db.SaveChanges();
            Success("Đã đổi vai trò của \"" + u.FullName + "\" thành " + Helpers.Vi.RoleName(role) + ".");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (id == CurrentUserId) { Error("Bạn không thể tự xóa tài khoản của chính mình."); return RedirectToAction("Index"); }

            var u = Db.Users.Find(id);
            if (u == null) return HttpNotFound();

            if (Db.Bookings.Any(b => b.UserId == id))
            {
                Error("Không thể xóa: người dùng này đã có đơn đặt chỗ.");
                return RedirectToAction("Index");
            }
            Db.Users.Remove(u);
            Db.SaveChanges();
            Success("Đã xóa người dùng.");
            return RedirectToAction("Index");
        }
    }
}
