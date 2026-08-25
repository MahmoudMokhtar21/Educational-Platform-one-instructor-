using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class UpdateCategoryDto
    {
        [MinLength(2)]
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Url]
        public string? ImageUrl { get; set; }

        public bool? IsActive { get; set; }
    }

}

