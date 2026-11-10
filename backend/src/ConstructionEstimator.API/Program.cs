using System.Text.Json.Serialization;
using ConstructionEstimator.API.Middleware;
using ConstructionEstimator.API.Security;
using ConstructionEstimator.Application;
using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Infrastructure;
using ConstructionEstimator.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---- logging: structured JSON, scopes carry TraceId/UserId
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    o.UseUtcTimestamp = true;
});

// ---- composition root
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
foreach (var service in ApplicationServices.Scoped) builder.Services.AddScoped(service);
builder.Services.AddInfrastructure(builder.Configuration);

// ---- authentication / authorisation
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        bearer.MapInboundClaims = false; // keep "sub" and "role" as issued
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = o.Issuer,
            ValidAudience = o.Audience,
            IssuerSigningKey = JwtTokenIssuer.SigningKey(o),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = AuthConstants.RoleClaim,
            NameClaimType = "name",
        };
    });
builder.Services.AddAuthorization(options =>
{
    foreach (var (policy, roles) in AuthConstants.PolicyRoles)
        options.AddPolicy(policy, p => p.RequireAuthenticatedUser().RequireRole(roles.Select(r => r.ToString())));
});

// ---- HTTP pipeline services
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Construction Estimator API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "POST /api/auth/login, then paste the accessToken here.",
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "ConstructionEstimator.API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

// ---- database: migrate + seed (development convenience; production would run migrations as a deploy step)
if (app.Configuration.GetValue("Database:MigrateAndSeedOnStartup", app.Environment.IsDevelopment()))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>()
        .MigrateAndSeedAsync(app.Configuration.GetValue("Seed:LargeEstimateLines", 0), CancellationToken.None);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
