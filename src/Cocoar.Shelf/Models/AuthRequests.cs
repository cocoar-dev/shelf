namespace Cocoar.Shelf.Models;

public record LoginRequest(string UserName, string Password, bool RememberMe = false);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string UserNameOrEmail);

public record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

public record MfaLoginRequest(string Code, bool RememberMe = false, bool RememberMachine = false);

public record EmailOtpLoginRequest(string Code, bool RememberMe = false);

public record MagicLinkRequest(string Email);

public record MagicLinkLoginRequest(Guid UserId, string Token, bool RememberMe = false);

public record CreateAdminRequest(string UserName, string Password, string? DisplayName = null, string? Email = null);

public record CreateUserRequest(string UserName, string? Password = null, string? DisplayName = null, string? Email = null);

public record UpdateUserRequest(string? DisplayName = null, string? Email = null);

public record SetPasswordRequest(string Password);

public record SetActiveRequest(bool IsActive);
