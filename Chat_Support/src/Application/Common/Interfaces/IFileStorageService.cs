namespace Chat_Support.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)> DownloadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
