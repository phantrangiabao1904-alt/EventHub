using System;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using EventHub.Helpers;
using EventHub.Models;
using EventHub.Security;
using EventHub.Services;
using EventHub.ViewModels;

namespace EventHub.Controllers
{
    public class AccountController : BaseController
    {
        private const int OtpResendSeconds = 60;    
        private const int OtpMaxAttempts = 5;       
        private static readonly object ResetGate = new object();

        [Serializable]
        private sealed class PasswordResetChallenge
        {
            public int UserId;
            public string Email;
            public string PasswordHash;
            public string OtpHash;
            public DateTime ExpiresAt;
            public int Attempts;
        }

        [HttpGet]
        public ActionResult ForgotPassword() { return View(new ForgotPasswordViewModel()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            if (!ModelState.IsValid) return View(vm);
            if (!Mailer.IsConfigured)
            {
                ModelState.AddModelError("", "Chưa cấu hình email gửi đi trong secrets.config. Vui lòng liên hệ quản trị viên.");
                return View(vm);
            }
            var email = vm.Email.Trim().ToLowerInvariant();
            lock (ResetGate)
            {
                var key = "PasswordReset:" + email;
                if (HttpRuntime.Cache[key] != null || (Session["ResetLastSent"] is DateTime
                    && (DateTime.UtcNow - (DateTime)Session["ResetLastSent"]).TotalSeconds < OtpResendSeconds))
                {
                    ModelState.AddModelError("", "Vui lòng đợi 60 giây trước khi yêu cầu mã mới.");
                    return View(vm);
                }
                HttpRuntime.Cache.Insert(key, true, null, DateTime.UtcNow.AddSeconds(OtpResendSeconds), System.Web.Caching.Cache.NoSlidingExpiration);
                Session["ResetLastSent"] = DateTime.UtcNow;
            }
            Session.Remove("PasswordReset");
            var user = Db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                var code = OtpService.Generate();
                var minutes = Math.Max(1, Math.Min(30, AppConfig.OtpExpireMinutes));
                var mail = EmailTemplates.PasswordReset(user.FullName, code, minutes);
                try
                {
                    Mailer.Send(user.Email, mail.Subject, mail.Body);
                    Session["PasswordReset"] = new PasswordResetChallenge { UserId = user.Id, Email = user.Email,
                        PasswordHash = user.PasswordHash, OtpHash = OtpService.Hash(code), ExpiresAt = DateTime.UtcNow.AddMinutes(minutes) };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Gửi mã khôi phục thất bại: " + ex.Message);
                     
                }
            }
            Success("Nếu email đã đăng ký, mã OTP sẽ được gửi tới hộp thư. Kiểm tra cả mục Spam và nhập mã trên cùng trình duyệt này.");
            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public ActionResult ResetPassword() { return View(new ResetPasswordViewModel()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            if (!ModelState.IsValid) return View(vm);
            var challenge = Session["PasswordReset"] as PasswordResetChallenge;
            if (challenge == null || challenge.ExpiresAt <= DateTime.UtcNow || challenge.Attempts >= OtpMaxAttempts)
            {
                Session.Remove("PasswordReset");
                ModelState.AddModelError("", "Mã không còn hiệu lực. Vui lòng yêu cầu mã mới.");
                return View(vm);
            }
            challenge.Attempts++;
            Session["PasswordReset"] = challenge;
            if (!OtpService.Verify(vm.Code, challenge.OtpHash))
            {
                if (challenge.Attempts >= OtpMaxAttempts) Session.Remove("PasswordReset");
                ModelState.AddModelError("", "Mã không đúng. Sau 5 lần nhập sai, bạn cần yêu cầu mã mới.");
                return View(vm);
            }
            Session.Remove("PasswordReset");
            var updated = Db.Database.ExecuteSqlCommand(
                "UPDATE [dbo].[Users] SET [PasswordHash]=@p0 WHERE [Id]=@p1 AND [PasswordHash]=@p2 AND [Email]=@p3",
                PasswordHasher.Hash(vm.NewPassword), challenge.UserId, challenge.PasswordHash, challenge.Email);
            if (updated != 1)
            {
                ModelState.AddModelError("", "Tài khoản đã thay đổi. Vui lòng yêu cầu mã mới.");
                return View(vm);
            }
            FormsAuthentication.SignOut();
            Success("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
            return RedirectToAction("Login");
        }

        [HttpGet, AppAuthorize, ActionName("Profile")]
        public ActionResult UserProfile()
        {
            var user = Db.Users.Find(CurrentUserId);
            if (user == null) return HttpNotFound();
            return View("Profile", ProfileOf(user));
        }

        [HttpPost, AppAuthorize, ValidateAntiForgeryToken, ActionName("Profile")]
        public ActionResult UpdateProfile(ProfileViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            var user = Db.Users.Find(CurrentUserId);
            if (user == null) return HttpNotFound();
            if (string.IsNullOrWhiteSpace(vm.FullName)) ModelState.AddModelError("FullName", "Vui lòng nhập họ và tên.");
            if (!ModelState.IsValid)
            {
                var display = ProfileOf(user);
                display.FullName = vm.FullName;
                return View("Profile", display);
            }
            user.FullName = vm.FullName.Trim();
            Db.SaveChanges();
            Success("Đã cập nhật hồ sơ cá nhân.");
            return RedirectToAction("Profile");
        }

        [HttpGet, AppAuthorize]
        public ActionResult ChangePassword() { return View(new ChangePasswordViewModel()); }

        [HttpPost, AppAuthorize, ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel vm)
        {
            if (vm == null) return new HttpStatusCodeResult(400);
            var user = Db.Users.Find(CurrentUserId);
            if (user == null) return HttpNotFound();
            if (ModelState.IsValid && !PasswordHasher.Verify(vm.CurrentPassword, user.PasswordHash))
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
            if (!ModelState.IsValid) return View(vm);
            user.PasswordHash = PasswordHasher.Hash(vm.NewPassword);
            Db.SaveChanges();
            Session.Remove("PasswordReset");
            FormsAuthentication.SignOut();
            Success("Đã đổi mật khẩu. Vui lòng đăng nhập lại.");
            return RedirectToAction("Login");
        }

        private static ProfileViewModel ProfileOf(User user)
        {
            return new ProfileViewModel { FullName = user.FullName, Email = user.Email,
                RoleName = Vi.RoleName(user.Role), CreatedAt = user.CreatedAt };
        }

         
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (Request.IsAuthenticated) return RedirectToHome();
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel vm, string returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(vm);

            var email = vm.Email.Trim().ToLowerInvariant();
            var user = Db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null || !PasswordHasher.Verify(vm.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
                return View(vm);
            }

            SignIn(user, vm.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToHome(user.Role);
        }

         
        [HttpGet]
        public ActionResult Register()
        {
            if (Request.IsAuthenticated) return RedirectToHome();
            return View(new RegisterViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var email = vm.Email.Trim().ToLowerInvariant();
            if (Db.Users.Any(u => u.Email == email))
            {
                ModelState.AddModelError("Email", "Email này đã được đăng ký.");
                return View(vm);
            }

            var now = DateTime.Now;
            var pending = Db.PendingRegistrations.FirstOrDefault(p => p.Email == email);
            var isNew = pending == null;
            if (isNew) pending = new PendingRegistration { Email = email };

            pending.FullName = vm.FullName.Trim();
            pending.PasswordHash = PasswordHasher.Hash(vm.Password);

             
            var recentlySent = !isNew && (now - pending.LastSentAt).TotalSeconds < OtpResendSeconds;
            if (!recentlySent)
            {
                var error = IssueOtp(pending);
                if (error != null)
                {
                    ModelState.AddModelError("", error);
                    return View(vm);
                }
            }

            if (isNew) Db.PendingRegistrations.Add(pending);
            Db.SaveChanges();

            Success(recentlySent
                ? "Mã xác thực vừa được gửi tới email của bạn. Vui lòng kiểm tra hộp thư (cả mục Spam)."
                : "Chúng tôi đã gửi mã xác thực 6 số tới " + email + ".");
            return RedirectToAction("VerifyOtp", new { email = email });
        }

        [HttpGet]
        public ActionResult VerifyOtp(string email)
        {
            if (Request.IsAuthenticated) return RedirectToHome();
            var pending = FindPending(email);
            if (pending == null)
            {
                Error("Phiên đăng ký không tồn tại hoặc đã hoàn tất. Vui lòng đăng ký lại.");
                return RedirectToAction("Register");
            }
            return View(OtpView(pending));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult VerifyOtp(VerifyOtpViewModel vm)
        {
            var pending = FindPending(vm.Email);
            if (pending == null)
            {
                Error("Phiên đăng ký không tồn tại hoặc đã hoàn tất. Vui lòng đăng ký lại.");
                return RedirectToAction("Register");
            }

            ViewData["MaskedEmail"] = Mask(pending.Email);
            ViewData["Minutes"] = AppConfig.OtpExpireMinutes;
            vm.Email = pending.Email;
            if (!ModelState.IsValid) return View(vm);

            ModelState.Remove("Code");
            var code = vm.Code;
            vm.Code = "";

            if (pending.FailedAttempts >= OtpMaxAttempts)
            {
                ModelState.AddModelError("", "Bạn đã nhập sai quá nhiều lần. Hãy bấm \"Gửi lại mã\" để nhận mã mới.");
                return View(vm);
            }
            if (pending.ExpiresAt < DateTime.Now)
            {
                ModelState.AddModelError("", "Mã xác thực đã hết hạn. Hãy bấm \"Gửi lại mã\" để nhận mã mới.");
                return View(vm);
            }
            if (!OtpService.Verify(code, pending.OtpHash))
            {
                pending.FailedAttempts++;
                Db.SaveChanges();
                var left = OtpMaxAttempts - pending.FailedAttempts;
                ModelState.AddModelError("", left > 0
                    ? "Mã xác thực không đúng. Bạn còn " + left + " lần thử."
                    : "Bạn đã nhập sai quá nhiều lần. Hãy bấm \"Gửi lại mã\" để nhận mã mới.");
                return View(vm);
            }

             
            if (!Db.Users.Any(u => u.Email == pending.Email))
            {
                Db.Users.Add(new User
                {
                    FullName = pending.FullName,
                    Email = pending.Email,
                    PasswordHash = pending.PasswordHash,
                    Role = Role.Client
                });
            }
            Db.PendingRegistrations.Remove(pending);
            Db.SaveChanges();

            Success("Xác thực email thành công. Bạn có thể đăng nhập ngay bây giờ.");
            return RedirectToAction("Login");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResendOtp(string email)
        {
            var pending = FindPending(email);
            if (pending == null)
            {
                Error("Phiên đăng ký không tồn tại hoặc đã hoàn tất. Vui lòng đăng ký lại.");
                return RedirectToAction("Register");
            }

            var wait = OtpResendSeconds - (int)(DateTime.Now - pending.LastSentAt).TotalSeconds;
            if (wait > 0)
            {
                Error("Vui lòng đợi " + wait + " giây nữa rồi gửi lại mã.");
                return RedirectToAction("VerifyOtp", new { email = pending.Email });
            }

            var error = IssueOtp(pending);
            if (error != null) { Error(error); return RedirectToAction("VerifyOtp", new { email = pending.Email }); }

            Db.SaveChanges();
            Success("Đã gửi mã xác thực mới tới " + pending.Email + ".");
            return RedirectToAction("VerifyOtp", new { email = pending.Email });
        }

         
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        public ActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }

         
         
        private string IssueOtp(PendingRegistration pending)
        {
            var code = OtpService.Generate();
            var mail = EmailTemplates.Otp(pending.FullName, code, AppConfig.OtpExpireMinutes);
            try
            {
                Mailer.Send(pending.Email, mail.Subject, mail.Body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Gửi OTP thất bại: " + ex);
                return Mailer.IsConfigured
                    ? "Không gửi được email xác thực. Vui lòng kiểm tra địa chỉ email hoặc thử lại sau."
                    : "Hệ thống chưa cấu hình gửi email (thiếu secrets.config). Vui lòng liên hệ quản trị viên.";
            }

            var now = DateTime.Now;
            pending.OtpHash = OtpService.Hash(code);
            pending.ExpiresAt = now.AddMinutes(AppConfig.OtpExpireMinutes);
            pending.FailedAttempts = 0;
            pending.LastSentAt = now;
            return null;
        }

        private PendingRegistration FindPending(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var e = email.Trim().ToLowerInvariant();
            return Db.PendingRegistrations.FirstOrDefault(p => p.Email == e);
        }

        private VerifyOtpViewModel OtpView(PendingRegistration pending)
        {
            ViewData["MaskedEmail"] = Mask(pending.Email);
            ViewData["Minutes"] = AppConfig.OtpExpireMinutes;
            return new VerifyOtpViewModel { Email = pending.Email };
        }

        private static string Mask(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email.Substring(0, 1) + new string('*', Math.Min(at - 1, 6)) + email.Substring(at);
        }

        private void SignIn(User user, bool remember)
        {
            var now = DateTime.Now;
            var expires = remember ? now.AddDays(7) : now.AddHours(8);
            var ticket = new FormsAuthenticationTicket(1, user.Id.ToString(), now, expires, remember, "");
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true
            };
            if (remember) cookie.Expires = expires;
            Response.Cookies.Add(cookie);
        }

        private ActionResult RedirectToHome(Role? role = null)
        {
            var isStaff = role.HasValue
                ? (role.Value == Role.Admin || role.Value == Role.Coordinator)
                : ((IPrincipal)User).IsStaff();
            if (isStaff) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
