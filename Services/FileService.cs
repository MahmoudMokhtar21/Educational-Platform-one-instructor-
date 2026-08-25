using Educatinal_Platform.Exceptions;

namespace Educatinal_Platform.Services
{
    public interface IFileService
    {
        Task<string> UploadAsync(
        IFormFile file,
        string folder,
        string[] allowedExtensions,
        long maxSize);
        Task DeleteAsync(string fileUrl);
    }
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;

        public FileService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> UploadAsync(
     IFormFile file,
     string folder,
     string[] allowedExtensions,
     long maxSize)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("File is empty.");

            if (file.Length > maxSize)
                throw new BadRequestException(
                    $"File size cannot exceed {maxSize / (1024 * 1024)} MB.");

            var extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new BadRequestException(
                    $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            var uploadsFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folder);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName =
                $"{Guid.NewGuid()}{extension}";

            var filePath =
                Path.Combine(uploadsFolder, fileName);

            await using var stream =
                new FileStream(filePath, FileMode.Create);

            await file.CopyToAsync(stream);

            return $"/uploads/{folder}/{fileName}";
        }

        public Task DeleteAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return Task.CompletedTask;

            var relativePath =
                fileUrl.TrimStart('/');

            var filePath =
                Path.Combine(
                    _environment.WebRootPath,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }
    }
}
