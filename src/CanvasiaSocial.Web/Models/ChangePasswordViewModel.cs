using System.ComponentModel.DataAnnotations;

namespace CanvasiaSocial.Web.Models;

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Yeni parola en az 12 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola tekrarı zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola tekrarı")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parolalar eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
