
using Application.Contracts;
using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/convert")]
[Authorize(Policy = "User")]
public class ConvertController(IRatesService _ratesService) : ControllerBase
{
    /// <summary>Convert an amount from one currency to another.</summary>
    [HttpGet]
    public async Task<ActionResult<ConversionResultDto>> Convert(
        [FromQuery] decimal amount,
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken ct = default)
    {
        if (amount <= 0) return BadRequest("amount must be > 0");
        var result = await _ratesService.ConvertAsync(amount, from, to, ct);
        return Ok(result);
    }

}
