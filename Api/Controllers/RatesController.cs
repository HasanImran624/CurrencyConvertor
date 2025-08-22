
using Application.Contracts;
using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;


[ApiController]
[Route("api/v1/rates")]
[Authorize(Policy = "User")]
public class RatesController(IRatesService ratesService) : ControllerBase
{
    private readonly IRatesService _ratesService = ratesService;

    /// <summary>
    /// Retrieve latest exchange rates for a given base currency.
    /// </summary>
    /// <param name="baseCurrency">Currency code (e.g., EUR, USD, GBP)</param>
    [HttpGet("latest")]
    public async Task<ActionResult<LatestRatesDto>> GetLatest(
      [FromQuery(Name = "base")] string baseCurrency = "EUR",
      CancellationToken ct = default)
    {
        var result = await _ratesService.GetLatestAsync(baseCurrency, ct);
        return Ok(result);
    }

    /// <summary>Retrieve historical rates with pagination over a date range.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<Paged<HistoricalRatesDto>>> GetHistory(
        [FromQuery] DateOnly start,
        [FromQuery] DateOnly end,
        [FromQuery(Name = "base")] string baseCurrency = "EUR",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page <= 0 || pageSize <= 0) return BadRequest("page and pageSize must be > 0");
        var result = await _ratesService.GetHistoricalAsync(start, end, baseCurrency, page, pageSize, ct);
        return Ok(result);
    }

}
