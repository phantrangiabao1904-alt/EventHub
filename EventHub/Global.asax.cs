using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using EventHub.Data;
using EventHub.Migrations;
using EventHub.Security;
using EventHub.Services;

namespace EventHub
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
             
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<EventHubContext, Configuration>());
            try { using (var db = new EventHubContext()) db.Database.Initialize(false); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Khởi tạo database thất bại: " + ex); }

             
            ReminderService.Start();

             
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.Name;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            MvcHandler.DisableMvcResponseHeader = true;
        }

         



        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var formsIdentity = Context.User == null ? null : Context.User.Identity as FormsIdentity;
            if (formsIdentity == null || !formsIdentity.IsAuthenticated || formsIdentity.Ticket == null) return;

            int userId;
            if (!int.TryParse(formsIdentity.Ticket.Name, out userId)) { SetAnonymous(); return; }

            try
            {
                using (var db = new EventHubContext())
                {
                    var u = db.Users.Where(x => x.Id == userId).Select(x => new { x.Role, x.FullName }).FirstOrDefault();
                    if (u == null) { SetAnonymous(); return; }

                    var principal = new AppPrincipal(new GenericIdentity(userId.ToString(), "Forms"), u.Role.ToString(), userId, u.FullName);
                    Context.User = principal;
                    Thread.CurrentPrincipal = principal;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Không đọc được thông tin đăng nhập: " + ex.Message);
                SetAnonymous();
            }
        }

        private void SetAnonymous()
        {
            Context.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
        }
    }
}
