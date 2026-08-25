namespace Educatinal_Platform.DTOs
{
    


   
    public class CourseResponseDto
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string? ShortDescription { get; set; }
            public decimal Price { get; set; }
            public string? ThumbnailUrl { get; set; }
            public string Level { get; set; } = string.Empty;
            public decimal TotalHours { get; set; }
            public int TotalStudents { get; set; }
            public decimal AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public int TotalLessons { get; set; } = 0;
    }
}

