namespace Educatinal_Platform.Options;

/// <summary>
/// Configurable safety limit for public image uploads. This is intentionally
/// independent from lesson media and resource upload limits.
/// </summary>
public sealed class ImageUploadOptions
{
    public const string SectionName = "FileUploads:Images";

    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
}
