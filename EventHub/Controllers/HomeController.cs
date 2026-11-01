using System.Linq;
using System.Web.Mvc;
using EventHub.Models;

namespace EventHub.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
             
            var venues = Db.Venues
                .Where(v => v.Status == VenueStatus.Available)
                .OrderByDescending(v => v.Bookings.Count())
                .ThenBy(v => v.Name)
                .Take(3)
                .ToList();
            return View(venues);
        }

        public ActionResult Error()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }
    }
}
