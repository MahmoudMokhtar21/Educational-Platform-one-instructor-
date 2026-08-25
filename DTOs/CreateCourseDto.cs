namespace Educatinal_Platform.DTOs
{
    
    public class CreateCourseDto
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? ShortDescription { get; set; }
            public decimal Price { get; set; } = 0;
            public string Level { get; set; } = "beginner";
            public string? CategoryId { get; set; }
            public List<string> Tags { get; set; } = new();
            public IFormFile? Thumbnail { get; set; }
            public IFormFile? Trailer { get; set; }
    }
    }

