using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using EventHub.Models;

namespace EventHub.Helpers
{
     
    public static class Vi
    {
        private static readonly CultureInfo VnCulture = new CultureInfo("vi-VN");

        public static string Money(decimal value) { return value.ToString("N0", VnCulture) + " ₫"; }
        public static string Date(DateTime d) { return d.ToString("dd/MM/yyyy"); }
        public static string Time(TimeSpan t) { return t.ToString(@"hh\:mm"); }
        public static string TimeRange(TimeSpan a, TimeSpan b) { return Time(a) + " - " + Time(b); }

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Role.Admin: return "Quản trị viên";
                case Role.Coordinator: return "Điều phối viên";
                default: return "Khách hàng";
            }
        }

        public static string VenueStatusName(VenueStatus s)
        {
            return s == VenueStatus.Available ? "Đang hoạt động" : "Đang bảo trì";
        }

        public static string VenueColor(VenueStatus s) { return s == VenueStatus.Available ? "success" : "secondary"; }

        public static string BookingStatusName(BookingStatus s)
        {
            switch (s)
            {
                case BookingStatus.Pending: return "Chờ duyệt yêu cầu";
                case BookingStatus.AwaitingDeposit: return "Đã duyệt - Chờ cọc";
                case BookingStatus.Confirmed: return "Đặt thành công";
                case BookingStatus.Completed: return "Đã hoàn thành";
                default: return "Đã hủy";
            }
        }

        public static string BookingColor(BookingStatus s)
        {
            switch (s)
            {
                case BookingStatus.Pending: return "warning";
                case BookingStatus.AwaitingDeposit: return "info";
                case BookingStatus.Confirmed: return "success";
                case BookingStatus.Completed: return "primary";
                default: return "danger";
            }
        }

        public static IEnumerable<SelectListItem> VenueStatusOptions(string selected)
        {
            return Enum.GetValues(typeof(VenueStatus)).Cast<VenueStatus>().Select(s => new SelectListItem
            {
                Value = s.ToString(), Text = VenueStatusName(s), Selected = s.ToString() == selected
            });
        }

        public static IEnumerable<SelectListItem> BookingStatusOptions(string selected)
        {
            return Enum.GetValues(typeof(BookingStatus)).Cast<BookingStatus>().Select(s => new SelectListItem
            {
                Value = s.ToString(), Text = BookingStatusName(s), Selected = s.ToString() == selected
            });
        }

        public static IEnumerable<SelectListItem> RoleOptions(string selected)
        {
            return Enum.GetValues(typeof(Role)).Cast<Role>().Select(r => new SelectListItem
            {
                Value = r.ToString(), Text = RoleName(r), Selected = r.ToString() == selected
            });
        }
    }
}
