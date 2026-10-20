namespace Educatinal_Platform.DTOs
{
    public class AdminCourseDto : CourseDetailDto
    {
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
