using Microsoft.AspNetCore.StaticFiles;

namespace WhatsAppDemo.Api.Services
{
    /// <summary>
    /// Stores media on the local disk under wwwroot/uploads. The original file name
    /// is preserved by prefixing a unique id (<c>{guid}__{originalName}</c>) so that
    /// downloads can present the correct name without any database schema change.
    /// </summary>
    public class LocalMediaStorageService : IMediaStorageService
    {
        private const string UploadsFolder = "uploads";
        private const string NameSeparator = "__";

        private readonly string _uploadsPath;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        public LocalMediaStorageService(IWebHostEnvironment environment)
        {
            var webRoot = environment.WebRootPath
                ?? Path.Combine(environment.ContentRootPath, "wwwroot");

            _uploadsPath = Path.Combine(webRoot, UploadsFolder);
            Directory.CreateDirectory(_uploadsPath);
        }

        public async Task<StoredMedia> SaveAsync(Stream content, string originalFileName, string contentType)
        {
            var safeName = Path.GetFileName(originalFileName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "file";
            }

            var storedFileName = $"{Guid.NewGuid():N}{NameSeparator}{safeName}";
            var fullPath = Path.Combine(_uploadsPath, storedFileName);

            await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await content.CopyToAsync(fileStream);
            }

            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? ResolveContentType(safeName)
                : contentType;

            return new StoredMedia
            {
                StoredFileName = storedFileName,
                OriginalFileName = safeName,
                ContentType = resolvedContentType,
                Url = $"/{UploadsFolder}/{storedFileName}",
                MessageType = InferMessageType(resolvedContentType)
            };
        }

        public StoredMediaFile? Open(string storedFileName)
        {
            var safeName = Path.GetFileName(storedFileName);
            var fullPath = Path.Combine(_uploadsPath, safeName);

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var originalName = ExtractOriginalName(safeName);

            return new StoredMediaFile
            {
                Content = new FileStream(fullPath, FileMode.Open, FileAccess.Read),
                OriginalFileName = originalName,
                ContentType = ResolveContentType(originalName)
            };
        }

        private static string ExtractOriginalName(string storedFileName)
        {
            var separatorIndex = storedFileName.IndexOf(NameSeparator, StringComparison.Ordinal);
            return separatorIndex >= 0
                ? storedFileName[(separatorIndex + NameSeparator.Length)..]
                : storedFileName;
        }

        private string ResolveContentType(string fileName)
            => _contentTypeProvider.TryGetContentType(fileName, out var contentType)
                ? contentType
                : "application/octet-stream";

        private static string InferMessageType(string contentType)
            => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? "Image"
                : "Document";
    }
}
