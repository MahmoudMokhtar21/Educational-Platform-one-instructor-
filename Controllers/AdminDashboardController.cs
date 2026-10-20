using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboard;

    public AdminDashboardController(IAdminDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _dashboard.GetSummaryAsync());
}
