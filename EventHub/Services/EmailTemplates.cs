using System.Web;
using EventHub.Helpers;
using EventHub.Models;

namespace EventHub.Services
{
    public class EmailContent
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }

     
    public static class EmailTemplates
    {
        public static EmailContent PasswordReset(string fullName, string code, int minutes)
        {
            return new EmailContent {
                Subject = "EventHub: Mã khôi phục mật khẩu",
                Body = Wrap("Khôi phục mật khẩu", "<p>Xin chào " + E(fullName) + ",</p>"
                    + "<p>Mã OTP đặt lại mật khẩu của bạn: <b style=\"font-size:28px\">" + E(code) + "</b></p>"
                    + "<p>Mã có hiệu lực trong " + minutes + " phút. Không chia sẻ mã này cho bất kỳ ai.</p>"
                    + "<p>Nếu không yêu cầu khôi phục mật khẩu, bạn có thể bỏ qua email này.</p>")
            };
        }
        private static string E(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

        private static string Wrap(string title, string inner)
        {
            return "<div style=\"font-family:Segoe UI,Arial,sans-serif;background:#f4f6f8;padding:24px\">"
                 + "<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:10px;overflow:hidden;border:1px solid #e5e7eb\">"
                 + "<div style=\"background:#0f172a;color:#ffffff;padding:18px 24px;font-size:20px;font-weight:600\">EventHub</div>"
                 + "<div style=\"padding:24px;color:#111827;line-height:1.6\">"
                 + "<h2 style=\"margin:0 0 12px 0;font-size:20px\">" + E(title) + "</h2>"
                 + inner
                 + "</div>"
                 + "<div style=\"padding:14px 24px;background:#f9fafb;color:#6b7280;font-size:12px\">Đây là email tự động từ hệ thống EventHub, vui lòng không trả lời.</div>"
                 + "</div></div>";
        }

        private static string Row(string label, string value)
        {
            return "<tr><td style=\"padding:6px 12px 6px 0;color:#6b7280;white-space:nowrap\">" + E(label)
                 + "</td><td style=\"padding:6px 0;font-weight:600\">" + E(value) + "</td></tr>";
        }

        private static string Details(Booking b)
        {
            var html = "<table style=\"border-collapse:collapse;margin:12px 0\">"
                     + Row("Mã đơn", "#" + b.Id)
                     + Row("Địa điểm", b.Venue.Name)
                     + Row("Địa chỉ", b.Venue.Address)
                     + Row("Ngày", Vi.Date(b.EventDate))
                     + Row("Thời gian", Vi.TimeRange(b.StartTime, b.EndTime))
                     + Row("Số khách", b.GuestCount.ToString())
                     + Row("Tổng chi phí (gồm dịch vụ)", Vi.Money(b.TotalPrice))
                     + Row("Cọc yêu cầu", Vi.Money(b.RequiredDeposit ?? System.Math.Round(b.TotalPrice * 0.3m, 0, System.MidpointRounding.AwayFromZero)));
            if (!string.IsNullOrWhiteSpace(b.Notes)) html += Row("Ghi chú", b.Notes);
            return html + "</table>";
        }

        private static string Link()
        {
            var baseUrl = AppConfig.BaseUrl;
            if (baseUrl == "") return "";
            return "<p><a href=\"" + E(baseUrl + "/Bookings") + "\" style=\"display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px\">Xem đơn đặt chỗ của tôi</a></p>";
        }

        public static EmailContent Otp(string fullName, string code, int minutes)
        {
            var inner = "<p>Xin chào <b>" + E(fullName) + "</b>,</p>"
                      + "<p>Mã xác thực đăng ký tài khoản EventHub của bạn là:</p>"
                      + "<p style=\"font-size:34px;letter-spacing:10px;font-weight:700;background:#f3f4f6;padding:14px 0;text-align:center;border-radius:8px\">" + E(code) + "</p>"
                      + "<p>Mã có hiệu lực trong <b>" + minutes + " phút</b>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>"
                      + "<p style=\"color:#6b7280\">Nếu bạn không thực hiện đăng ký, hãy bỏ qua email này.</p>";
            return new EmailContent { Subject = "Mã xác thực EventHub: " + code, Body = Wrap("Xác thực email đăng ký", inner) };
        }

        public static EmailContent BookingReceived(Booking b)
        {
            var inner = "<p>Xin chào <b>" + E(b.User.FullName) + "</b>,</p>"
                      + "<p>Chúng tôi đã nhận được yêu cầu thuê. Nhân viên sẽ duyệt và gửi QR hướng dẫn cọc. Đơn chỉ đặt thành công sau khi nhân viên xác nhận đã nhận đủ cọc.</p>"
                      + Details(b) + Link();
            return new EmailContent { Subject = "EventHub: Đã nhận yêu cầu đặt chỗ #" + b.Id, Body = Wrap("Đã nhận yêu cầu đặt chỗ", inner) };
        }

        public static EmailContent BookingConfirmed(Booking b)
        {
            var inner = "<p>Xin chào <b>" + E(b.User.FullName) + "</b>,</p>"
                      + "<p>Nhân viên đã xác nhận nhận đủ tiền cọc. Đơn đặt chỗ của bạn đã <b style=\"color:#16a34a\">đặt thành công</b>. Chúng tôi sẽ gửi email nhắc lịch trước sự kiện.</p>"
                      + Details(b) + Link();
            return new EmailContent { Subject = "EventHub: Đơn #" + b.Id + " đã đặt thành công", Body = Wrap("Đã nhận cọc — Đặt thành công", inner) };
        }

        public static EmailContent DepositRequest(Booking b)
        {
            var body = "<p>Xin chào <b>" + E(b.User.FullName) + "</b>, yêu cầu thuê đã được duyệt và đang chờ cọc.</p>"
                + Details(b) + "<p><b>Đơn chưa đặt thành công. Sau khi chuyển khoản, vui lòng chờ nhân viên đối chiếu và xác nhận đã nhận cọc.</b></p>"
                + "<p>Quét QR bên dưới để chuyển tiền cọc. Nội dung chuyển khoản: <b>EVENTHUB " + b.Id + "</b>.</p>"
                + "<img src=\"cid:deposit-qr\" width=\"280\" alt=\"QR chuyển khoản tiền cọc\" style=\"max-width:100%;height:auto\">" + Link();
            return new EmailContent { Subject = "EventHub: QR cọc cho đơn #" + b.Id + " (chờ thanh toán)", Body = Wrap("Yêu cầu đặt cọc", body) };
        }

        public static EmailContent BookingCancelled(Booking b, bool byClient)
        {
            var intro = byClient
                ? "<p>Bạn đã <b style=\"color:#dc2626\">hủy</b> đơn đặt chỗ sau:</p>"
                : "<p>Rất tiếc, đơn đặt chỗ của bạn đã bị <b style=\"color:#dc2626\">hủy</b> bởi bộ phận điều phối. Vui lòng liên hệ với chúng tôi hoặc đặt lại thời gian khác nếu cần.</p>";
            var inner = "<p>Xin chào <b>" + E(b.User.FullName) + "</b>,</p>" + intro + Details(b);
            return new EmailContent { Subject = "EventHub: Đơn #" + b.Id + " đã bị hủy", Body = Wrap("Đơn đặt chỗ đã hủy", inner) };
        }

        public static EmailContent BookingReminder(Booking b)
        {
            var inner = "<p>Xin chào <b>" + E(b.User.FullName) + "</b>,</p>"
                      + "<p>Sự kiện của bạn <b>sắp diễn ra</b> vào <b>" + E(Vi.Date(b.EventDate)) + " lúc " + E(Vi.Time(b.StartTime)) + "</b>. Vui lòng kiểm tra lại thông tin:</p>"
                      + Details(b) + Link();
            return new EmailContent { Subject = "EventHub: Nhắc lịch sự kiện #" + b.Id + " sắp diễn ra", Body = Wrap("Sắp đến giờ sự kiện của bạn", inner) };
        }
    }
}
