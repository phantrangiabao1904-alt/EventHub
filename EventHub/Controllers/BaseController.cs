using System.Web.Mvc;
using EventHub.Data;
using EventHub.Security;

namespace EventHub.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly EventHubContext Db = new EventHubContext();

        protected int CurrentUserId
        {
            get
            {
                var p = User as AppPrincipal;
                return p == null ? 0 : p.UserId;
            }
        }

        protected void Success(string message) { TempData["Success"] = message; }
        protected void Error(string message) { TempData["Error"] = message; }
        protected void Warning(string message) { TempData["Warning"] = message; }

        protected ActionResult RedirectLocal(string returnUrl, ActionResult fallback)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return fallback;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Db.Dispose();
            base.Dispose(disposing);
        }
    }
}
