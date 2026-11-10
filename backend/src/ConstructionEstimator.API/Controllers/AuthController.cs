using ConstructionEstimator.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstructionEstimator.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(AuthService auth, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Development sign-in: exchange a seeded user's email for a signed JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Seeded users for the picker. Development only — returns 404 in any other environment.</summary>
    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Users(CancellationToken ct) =>
        env.IsDevelopment() ? Ok(await auth.ListUsersAsync(ct)) : NotFound();
}
