using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Results;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Chat_Support.Application.Chats.Commands;

public class UploadChatFileCommand : IRequest<Result<ChatFileUploadResult>>
{
    public int ChatRoomId { get; set; }
    public IFormFile File { get; set; } = null!;
    public MessageType Type { get; set; }
}

public class ChatFileUploadResult
{
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

public class UploadChatFileCommandHandler : IRequestHandler<UploadChatFileCommand, Result<ChatFileUploadResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IFileStorageService _fileStorage;

    // Configuration
    private const long MaxFileSize = 20 * 1024 * 1024; // 20MB

    private readonly HashSet<string> _allowedImageExtensions = new()
    { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };

    private readonly HashSet<string> _allowedVideoExtensions = new()
    { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".m4v" };

    private readonly HashSet<string> _allowedAudioExtensions = new()
    { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac", ".wma","" };

    private readonly HashSet<string> _allowedDocumentExtensions = new()
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".rtf", ".odt", ".ods", ".odp", ".csv", ".zip",
        ".rar", ".7z", ".tar", ".gz"
    };

    public UploadChatFileCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IFileStorageService fileStorage)
    {
        _context = context;
        _user = user;
        _fileStorage = fileStorage;
    }

    public async Task<Result<ChatFileUploadResult>> Handle(
        UploadChatFileCommand request,
        CancellationToken cancellationToken)
    {
        // Validate user is member of chat room
        var isMember = await _context.ChatRoomMembers
            .AnyAsync(x => x.ChatRoomId == request.ChatRoomId &&
                          x.UserId == _user.Id,
                      cancellationToken);

        if (!isMember)
            return Result<ChatFileUploadResult>.Failure("You are not a member of this chat room");

        // Validate file
        if (request.File == null || request.File.Length == 0)
            return Result<ChatFileUploadResult>.Failure("No file uploaded");

        if (request.File.Length > MaxFileSize)
            return Result<ChatFileUploadResult>.Failure($"File size exceeds {MaxFileSize / 1024 / 1024}MB limit");
        var fileName = request.File.FileName;



        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if ((string.IsNullOrWhiteSpace(extension) || string.IsNullOrEmpty(extension)) && request.Type is MessageType.Voice or MessageType.Audio)
        {
            fileName += ".mp3";
            extension = ".mp3";
        }

        // Validate file type based on MessageType
        var isValidExtension = request.Type switch
        {
            MessageType.Image => _allowedImageExtensions.Contains(extension),
            MessageType.Video => _allowedVideoExtensions.Contains(extension),
            MessageType.Audio or MessageType.Voice => _allowedAudioExtensions.Contains(extension),
            MessageType.File => _allowedDocumentExtensions.Contains(extension) ||
                               _allowedImageExtensions.Contains(extension) ||
                               _allowedVideoExtensions.Contains(extension) ||
                               _allowedAudioExtensions.Contains(extension),
            _ => false
        };

        if (!isValidExtension)
            return Result<ChatFileUploadResult>.Failure($"File type {extension} is not allowed for {request.Type}");

        // Additional security checks
        if (!IsFileSafe(request.File))
            return Result<ChatFileUploadResult>.Failure("File contains potentially dangerous content");

        // Generate unique file name
        var uniqueFileName = $"chat_{request.ChatRoomId}_{_user.Id}_{DateTime.Now.Ticks}{extension}";
        var folderPath = request.Type switch
        {
            MessageType.Image => "chat/images",
            MessageType.Video => "chat/videos",
            MessageType.Audio or MessageType.Voice => "chat/audio",
            _ => "chat/files"
        };

        try
        {
            // Upload file
            using var stream = request.File.OpenReadStream();
            var fileUrl = await _fileStorage.UploadFileAsync(
                stream,
                uniqueFileName,
                folderPath,
                request.File.ContentType,
                cancellationToken);

            // Save file metadata (optional - if you want to track all uploaded files)
            var fileMetadata = new ChatFileMetadata
            {
                FileName = fileName,
                FilePath = fileUrl,
                FileSize = request.File.Length,
                ContentType = request.File.ContentType,
                ChatRoomId = request.ChatRoomId,
                UploadedById = _user.Id,
                UploadedDate = DateTime.Now,
                MessageType = request.Type
            };

            _context.ChatFileMetadatas.Add(fileMetadata);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<ChatFileUploadResult>.Success(new ChatFileUploadResult
            {
                FileUrl = fileUrl,
                FileName = fileName,
                FileSize = request.File.Length,
                ContentType = request.File.ContentType
            });
        }
        catch (Exception ex)
        {
            return Result<ChatFileUploadResult>.Failure($"Failed to upload file: {ex.Message}");
        }
    }

    private bool IsFileSafe(IFormFile file)
    {
        // Check for dangerous file signatures
        var dangerousSignatures = new Dictionary<string, byte[]>
        {
            { "exe", new byte[] { 0x4D, 0x5A } }, // MZ header
            { "dll", new byte[] { 0x4D, 0x5A } },
            { "com", new byte[] { 0x4D, 0x5A } },
            { "bat", new byte[] { 0x40, 0x65, 0x63, 0x68, 0x6F } }, // @echo
            { "cmd", new byte[] { 0x40, 0x65, 0x63, 0x68, 0x6F } },
            { "scr", new byte[] { 0x4D, 0x5A } },
            { "vbs", new byte[] { 0x4D, 0x73, 0x67, 0x42, 0x6F, 0x78 } }, // MsgBox
            { "js", new byte[] { 0x76, 0x61, 0x72, 0x20 } }, // var 
        };

        using var stream = file.OpenReadStream();
        var buffer = new byte[512];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0)
                break;
            totalRead += bytesRead;
        }
        stream.Position = 0;

        foreach (var signature in dangerousSignatures.Values)
        {
            if (buffer.Take(signature.Length).SequenceEqual(signature))
                return false;
        }

        // Check file extension in content
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (dangerousSignatures.ContainsKey(extension.TrimStart('.')))
            return false;

        // Additional MIME type validation
        var contentType = file.ContentType.ToLowerInvariant();
        if (contentType.Contains("executable") ||
            contentType.Contains("application/x-msdownload") ||
            contentType.Contains("application/x-msdos-program"))
            return false;

        return true;
    }
}
