using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Domain.Users;

namespace ConstructionEstimator.Application.Auth;

public sealed record LoginRequest(string Email);
public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>
/// Development sign-in: the brief asks us to skip real authentication and offer a user picker.
/// The token it issues is real (signed, expiring) and every endpoint validates it.
/// </summary>
public sealed class AuthService(IUserRepository users, ITokenIssuer tokens)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new RequestValidationException("email", "Email is required.");
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct)
                   ?? throw new NotFoundException("User", request.Email);
        var (token, expiresAt) = tokens.Issue(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(ToDto).ToList();

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);
}
