using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Educatinal_Platform.Controllers;
[ApiController, Route("api/student-reviews")]
public class StudentReviewsController(IStudentReviewService service) : ControllerBase { [AllowAnonymous, HttpGet] public Task<List<StudentReviewDto>> Get() => service.GetAllAsync(); [Authorize(Roles="admin"), HttpPost] public Task<StudentReviewDto> Create(StudentReviewUpsertDto dto) => service.CreateAsync(dto); [Authorize(Roles="admin"), HttpPut("{id}")] public Task<StudentReviewDto> Update(string id, StudentReviewUpsertDto dto) => service.UpdateAsync(id,dto); [Authorize(Roles="admin"), HttpDelete("{id}")] public async Task<IActionResult> Delete(string id) { await service.DeleteAsync(id); return NoContent(); } }
