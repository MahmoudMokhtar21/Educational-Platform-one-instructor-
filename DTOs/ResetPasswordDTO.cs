using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs
{
    public class ResetPasswordDTO
        {
        
        [DataType(DataType.Password)]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 6 characters")]
        [MaxLength(50, ErrorMessage = "Password must be at most 50 characters")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).+$",
            ErrorMessage =
            "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string NewPassword { get; set; } = string.Empty;
        }
    
}
