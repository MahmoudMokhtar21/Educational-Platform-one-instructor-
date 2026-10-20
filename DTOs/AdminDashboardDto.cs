namespace Educatinal_Platform.DTOs;

public class AdminDashboardDto
{
    public long TotalCourses { get; set; }
    public long PublishedCourses { get; set; }
    public long DraftCourses { get; set; }
    public long TotalStudents { get; set; }
    public long TotalEnrollments { get; set; }
    public long ActiveEnrollments { get; set; }
    public List<AdminDashboardCourseDto> RecentCourses { get; set; } = new();
}

public class AdminDashboardCourseDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public DateTime CreatedAt { get; set; }
}
