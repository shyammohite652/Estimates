namespace ConstructionEstimator.API.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; init; } = "construction-estimator";
    public string Audience { get; init; } = "construction-estimator-ui";
    /// <summary>Symmetric signing key. Development-only value lives in appsettings.Development.json.</summary>
    public string SigningKey { get; init; } = "";
    public int ExpiryMinutes { get; init; } = 480;
}
