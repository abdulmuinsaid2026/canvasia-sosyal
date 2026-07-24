using System.ComponentModel.DataAnnotations;

namespace CanvasiaSocial.Web.Models;

public sealed class LoginViewModel
{
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Parola")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }
}
