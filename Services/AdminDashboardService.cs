using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;
using MongoDB.Driver;

namespace Educatinal_Platform.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetSummaryAsync();
}

public class AdminDashboardService : IAdminDashboardService
{
    private readonly ICourseRepository _courses;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IMongoCollection<User> _users;

    public AdminDashboardService(ICourseRepository courses, IEnrollmentRepository enrollments, IMongoDatabase database)
    {
        _courses = courses;
        _enrollments = enrollments;
        _users = database.GetCollection<User>("Users");
    }

    public async Task<AdminDashboardDto> GetSummaryAsync()
    {
        var totalCoursesTask = _courses.CountAsync();
        var publishedCoursesTask = _courses.CountByStatusAsync("published");
        var draftCoursesTask = _courses.CountByStatusAsync("draft");
        var studentsTask = _users.CountDocumentsAsync(user => user.Role == "student");
        var totalEnrollmentsTask = _enrollments.CountAsync();
        var activeEnrollmentsTask = _enrollments.CountAsync("active");
        var recentCoursesTask = _courses.GetAllAsync(1, 5, sort: "recent");

        await Task.WhenAll(totalCoursesTask, publishedCoursesTask, draftCoursesTask, studentsTask, totalEnrollmentsTask, activeEnrollmentsTask, recentCoursesTask);

        return new AdminDashboardDto
        {
            TotalCourses = totalCoursesTask.Result,
            PublishedCourses = publishedCoursesTask.Result,
            DraftCourses = draftCoursesTask.Result,
            TotalStudents = studentsTask.Result,
            TotalEnrollments = totalEnrollmentsTask.Result,
            ActiveEnrollments = activeEnrollmentsTask.Result,
            RecentCourses = recentCoursesTask.Result.Select(course => new AdminDashboardCourseDto
            {
                Id = course.Id,
                Title = course.Title,
                Status = course.Status,
                TotalStudents = course.TotalStudents,
                CreatedAt = course.CreatedAt
            }).ToList()
        };
    }
}
