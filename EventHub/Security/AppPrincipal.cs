using System.Security.Principal;
using System.Web.Mvc;
using System.Web.Routing;
using EventHub.Models;

namespace EventHub.Security
{
    public class AppPrincipal : GenericPrincipal
    {
        public int UserId { get; private set; }
        public string FullName { get; private set; }

        public AppPrincipal(IIdentity identity, string role, int userId, string fullName)
            : base(identity, new[] { role })
        {
            UserId = userId;
            FullName = fullName;
        }
    }

    public static class PrincipalExtensions
    {
        public static string GetFullName(this IPrincipal user)
        {
            var p = user as AppPrincipal;
            return p == null ? "" : p.FullName;
        }

        public static bool IsStaff(this IPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Coordinator);
        }
    }

     
    public class AppAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(
                    new { area = "", controller = "Account", action = "AccessDenied" }));
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}
