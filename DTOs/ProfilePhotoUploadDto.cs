using System.ComponentModel.DataAnnotations;

namespace Educatinal_Platform.DTOs;

/// <summary>Multipart payload for an authenticated profile-photo replacement.</summary>
public sealed class ProfilePhotoUploadDto
{
    [Required]
    public IFormFile? File { get; set; }
}