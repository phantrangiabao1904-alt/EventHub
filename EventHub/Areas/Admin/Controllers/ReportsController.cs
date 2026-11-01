using System;
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
    public class ReportsController : BaseController
    {
        public ActionResult Index(int? month, int? year)
        {
            var m = month ?? DateTime.Today.Month;
            var y = year ?? DateTime.Today.Year;
            if (!ModelState.IsValid || m < 1 || m > 12 || y < 2000 || y > 2100) return new HttpStatusCodeResult(400, "Invalid month or year");
            return View(Build(m, y));
        }

        public ActionResult Export(int month, int year)
        {
            if (!ModelState.IsValid || month < 1 || month > 12 || year < 2000 || year > 2100) return new HttpStatusCodeResult(400);
            return File(ExcelReportWriter.Write(Build(month, year)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "EventHub-" + year + "-" + month.ToString("D2") + ".xlsx");
        }

        private MonthlyReportViewModel Build(int month, int year)
        {
            var from = new DateTime(year, month, 1);
            var until = from.AddMonths(1);
            var bookings = Db.Bookings.AsNoTracking().Include(b => b.User).Include(b => b.Venue)
                .Where(b => b.EventDate >= from && b.EventDate < until).OrderBy(b => b.EventDate).ThenBy(b => b.StartTime).ToList();
            var payments = Db.PaymentEntries.Where(p => p.RecordedAt >= from && p.RecordedAt < until);
            return new MonthlyReportViewModel { Month = month, Year = year, Bookings = bookings,
                Receipts = payments.Where(p => p.Type == PaymentEntryType.Receipt).Select(p => (decimal?)p.Amount).Sum() ?? 0,
                Refunds = payments.Where(p => p.Type == PaymentEntryType.Refund).Select(p => (decimal?)p.Amount).Sum() ?? 0,
                TopVenues = bookings.Where(b => b.Status != BookingStatus.Cancelled).GroupBy(b => new { b.VenueId, b.Venue.Name })
                    .Select(g => new VenueReportRow { Name = g.Key.Name, Count = g.Count(),
                        Value = g.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed).Sum(b => b.TotalPrice) })
                    .OrderByDescending(v => v.Count).ThenBy(v => v.Name).Take(10).ToList() };
        }
    }
}
