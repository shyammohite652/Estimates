using ConstructionEstimator.Application.Catalogue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstructionEstimator.API.Controllers;

[ApiController]
[Route("api/catalogue")]
[Authorize]
public sealed class CatalogueController(CatalogueService catalogue) : ControllerBase
{
    /// <summary>Catalogue items priced on <paramref name="pricingDate"/> (R2). Labour component only for permitted roles (R17).</summary>
    [HttpGet]
    public Task<IReadOnlyList<CatalogueItemDto>> Search([FromQuery] DateOnly pricingDate, [FromQuery] string? search, CancellationToken ct) =>
        catalogue.SearchPricedOnAsync(pricingDate, search, ct);
}
