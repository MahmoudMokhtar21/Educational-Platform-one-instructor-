namespace Educatinal_Platform.DTOs
{
    public class ReviewResponseDto
    {
        public string Id { get; set; } = string.Empty;

        public string StudentId { get; set; } = string.Empty;

        public string CourseId { get; set; } = string.Empty;

        public int Rating { get; set; }

        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}