using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class CreateCategoryDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Url]
        public string? ImageUrl { get; set; }
    }
}

