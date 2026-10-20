using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController, Route("api/site-settings")]
public class SiteSettingsController(ISiteSettingsService service) : ControllerBase
{
    [AllowAnonymous, HttpGet] public Task<SiteSettingsDto> Get() => service.GetAsync();
    [Authorize(Roles = "admin"), HttpPut] public async Task<ActionResult<SiteSettingsDto>> Update(SiteSettingsDto dto)
    {
        try { return Ok(await service.UpdateAsync(dto)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}
