namespace WhatsAppDemo.Api.Services
{
    /// <summary>
    /// Stores and retrieves media files (images/documents) shared in conversations.
    /// The local-disk implementation is used for the POC; it can later be swapped
    /// for cloud/blob storage without touching the controllers.
    /// </summary>
    public interface IMediaStorageService
    {
        /// <summary>
        /// Persists an uploaded file and returns a stored result describing where
        /// it lives and the public URL that can be used to open/download it.
        /// </summary>
        Task<StoredMedia> SaveAsync(Stream content, string originalFileName, string contentType);

        /// <summary>
        /// Opens a previously stored file for reading. Returns null when it is
        /// missing. The original file name and content type are returned so the
        /// caller can present a proper download.
        /// </summary>
        StoredMediaFile? Open(string storedFileName);
    }

    /// <summary>Metadata returned after a file is stored.</summary>
    public class StoredMedia
    {
        public string StoredFileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;

        /// <summary>Relative, browser-openable URL, e.g. /uploads/{storedFileName}.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Text/Image/Document, inferred from the content type.</summary>
        public string MessageType { get; set; } = "Document";
    }

    /// <summary>An opened stored file ready to be streamed back to the client.</summary>
    public class StoredMediaFile
    {
        public Stream Content { get; set; } = Stream.Null;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
