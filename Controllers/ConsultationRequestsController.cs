using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/consultations")]
public class ConsultationsController(IConsultationRequestService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<ConsultationRequestSubmissionDto>> Create(
        [FromBody] CreateConsultationRequestDto dto)
    {
        var submitted = await service.CreateAsync(dto);
        return StatusCode(StatusCodes.Status201Created, submitted);
    }

    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<ActionResult<List<ConsultationRequestListItemDto>>> List(
        [FromQuery] ConsultationRequestStatus? status = null) =>
        Ok(await service.GetAllAsync(status));

    [Authorize(Roles = "admin")]
    [HttpGet("{id}")]
    public async Task<ActionResult<ConsultationRequestDetailDto>> Detail(string id) =>
        Ok(await service.GetByIdAsync(id));

    [Authorize(Roles = "admin")]
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ConsultationRequestDetailDto>> UpdateStatus(
        string id,
        [FromBody] UpdateConsultationRequestStatusDto dto) =>
        Ok(await service.UpdateStatusAsync(id, dto));
}
