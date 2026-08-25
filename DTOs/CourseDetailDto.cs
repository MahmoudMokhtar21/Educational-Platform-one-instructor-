namespace Educatinal_Platform.DTOs
{
    
    public class CourseDetailDto : CourseResponseDto
    {
            public string Description { get; set; } = string.Empty;
            public List<LessonDto> Lessons { get; set; } = new();
            public CategoryEmbeddedDto? Category { get; set; }
            public List<string> Tags { get; set; } = new();
            public bool IsEnrolled { get; set; } 
            public decimal? ProgressPercent { get; set; } 
    }
    public class CategoryEmbeddedDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }
}

