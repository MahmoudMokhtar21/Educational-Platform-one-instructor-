namespace Educatinal_Platform.DTOs
{
    public class UpdateReviewDto
    {
        public int Rating { get; set; }

        public string Comment { get; set; } = string.Empty;
    }
}