using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Options;
using Microsoft.Extensions.Options;

namespace Educatinal_Platform.Services
{
    public interface IFileService
    {
        Task<string> UploadAsync(
            IFormFile file,
            string folder,
            string[] allowedExtensions,
            long? maxSize);
        Task<string> UploadImageAsync(IFormFile file, string folder, long? maxSize = null);
        Task DeleteAsync(string fileUrl);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ImageUploadOptions _imageUploadOptions;

        public FileService(
            IWebHostEnvironment environment,
            IOptions<ImageUploadOptions> imageUploadOptions)
        {
            _environment = environment;
            _imageUploadOptions = imageUploadOptions.Value;
        }

        public Task<string> UploadAsync(
            IFormFile file,
            string folder,
            string[] allowedExtensions,
            long? maxSize)
        {
            return UploadInternalAsync(file, folder, allowedExtensions, maxSize, requireImageSignature: false);
        }

        public Task<string> UploadImageAsync(IFormFile file, string folder, long? maxSize = null)
        {
            return UploadInternalAsync(
                file,
                folder,
                [".jpg", ".jpeg", ".png", ".webp"],
                maxSize ?? _imageUploadOptions.MaxBytes,
                requireImageSignature: true);
        }

        private async Task<string> UploadInternalAsync(
            IFormFile file,
            string folder,
            string[] allowedExtensions,
            long? maxSize,
            bool requireImageSignature)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("File is empty.");

            if (maxSize is { } maxAllowedSize && file.Length > maxAllowedSize)
                throw new BadRequestException(
                    $"File size cannot exceed {maxAllowedSize / (1024 * 1024)} MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new BadRequestException(
                    $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            if (string.IsNullOrWhiteSpace(folder) ||
                folder.Contains("..", StringComparison.Ordinal))
            {
                throw new BadRequestException("Invalid upload folder.");
            }

            var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));
            var uploadsFolder = Path.GetFullPath(Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folder));

            if (!uploadsFolder.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Invalid upload folder.");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var source = file.OpenReadStream();

            if (requireImageSignature)
            {
                if (!source.CanSeek)
                    throw new BadRequestException("The uploaded image cannot be validated safely.");

                await ValidateImageSignatureAsync(source, extension);
                source.Position = 0;
            }

            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);

                await source.CopyToAsync(stream);
            }
            catch
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch
                {
                    // Preserve the original upload failure; cleanup is best effort.
                }

                throw;
            }

            return $"/uploads/{folder}/{fileName}";
        }

        private static async Task ValidateImageSignatureAsync(Stream stream, string extension)
        {
            var header = new byte[12];
            var read = 0;

            while (read < header.Length)
            {
                var bytesRead = await stream.ReadAsync(header.AsMemory(read, header.Length - read));
                if (bytesRead == 0)
                    break;

                read += bytesRead;
            }

            var isValidImage = extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => read >= 8 &&
                          header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".webp" => read >= 12 &&
                           header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                           header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => false
            };

            if (!isValidImage)
                throw new BadRequestException("The uploaded file is not a valid supported image.");
        }

        public Task DeleteAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return Task.CompletedTask;

            if (!fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            var relativePath = fileUrl.TrimStart('/');

            var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"))
                + Path.DirectorySeparatorChar;
            var filePath = Path.GetFullPath(Path.Combine(
                _environment.WebRootPath,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));

            if (filePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }
    }
}
