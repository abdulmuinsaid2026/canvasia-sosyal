namespace CanvasiaSocial.Application.Authentication;

public sealed record LoginCommand(string Email, string Password, bool RememberMe);
