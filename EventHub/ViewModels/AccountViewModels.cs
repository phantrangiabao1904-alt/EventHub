using System.ComponentModel.DataAnnotations;

namespace EventHub.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress, StringLength(255), Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class NewPasswordViewModel
    {
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }
        [Required, Compare("NewPassword", ErrorMessage = "Mật khẩu nhập lại không khớp."), DataType(DataType.Password), Display(Name = "Nhập lại mật khẩu mới")]
        public string ConfirmPassword { get; set; }
    }

    public class ResetPasswordViewModel : NewPasswordViewModel
    {
        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm đúng 6 chữ số."), Display(Name = "Mã OTP")]
        public string Code { get; set; }
    }

    public class ChangePasswordViewModel : NewPasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; }
    }

    public class ProfileViewModel
    {
        [Required, StringLength(100), Display(Name = "Họ và tên")]
        public string FullName { get; set; }
        public string Email { get; set; }
        public string RoleName { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(255, ErrorMessage = "Email tối đa 255 ký tự.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        [Display(Name = "Nhập lại mật khẩu")]
        public string ConfirmPassword { get; set; }
    }

    public class VerifyOtpViewModel
    {
        [Required(ErrorMessage = "Thiếu địa chỉ email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã xác thực.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực gồm đúng 6 chữ số.")]
        [Display(Name = "Mã xác thực")]
        public string Code { get; set; }
    }
}
