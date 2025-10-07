using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Chat_Support.Application.Chats.Commands;

public record UploadFileCommand(
    IFormFile File,
    MessageType Type
) : IRequest<UploadFileResult>;

public record UploadFileResult(
    string FileUrl,
    string FileName,
    long FileSize,
    string FileType
);

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, UploadFileResult>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IUser _user;
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private readonly string[] _allowedFileExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar" };
    private readonly string[] _allowedAudioExtensions = { ".mp3", ".wav", ".ogg", ".m4a", ".webm" };
    private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB
    private readonly long _maxImageSize = 5 * 1024 * 1024; // 5MB

    public UploadFileCommandHandler(
        IFileStorageService fileStorage,
        IUser user)
    {
        _fileStorage = fileStorage;
        _user = user;
    }

    public async Task<UploadFileResult> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        var file = request.File;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Validate file
        switch (request.Type)
        {
            case MessageType.Image:
                if (!_allowedImageExtensions.Contains(extension))
                    throw new ArgumentException("Invalid image format");
                if (file.Length > _maxImageSize)
                    throw new ArgumentException("Image size exceeds 5MB limit");
                break;

            case MessageType.File:
                if (!_allowedFileExtensions.Contains(extension))
                    throw new ArgumentException("File type not allowed");
                if (file.Length > _maxFileSize)
                    throw new ArgumentException("File size exceeds 10MB limit");
                break;

            case MessageType.Audio:
                if (!_allowedAudioExtensions.Contains(extension))
                    throw new ArgumentException("Invalid audio format");
                if (file.Length > _maxFileSize)
                    throw new ArgumentException("Audio size exceeds 10MB limit");
                break;

            default:
                throw new ArgumentException("Invalid message type for file upload");
        }

        // Generate unique file name
        var uniqueFileName = $"{_user.Id}_{DateTime.Now.Ticks}{extension}";
        var folderPath = request.Type switch
        {
            MessageType.Image => "images",
            MessageType.Audio => "audio",
            _ => "files"
        };

        // Upload file
        var fileUrl = await _fileStorage.UploadFileAsync(
            file.OpenReadStream(),
            uniqueFileName,
            folderPath,
            file.ContentType,
            cancellationToken);

        return new UploadFileResult(
            fileUrl,
            file.FileName,
            file.Length,
            file.ContentType
        );
    }
}
