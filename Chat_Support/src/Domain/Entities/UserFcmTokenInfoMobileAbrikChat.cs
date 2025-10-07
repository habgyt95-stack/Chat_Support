using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat_Support.Domain.Entities;

public class UserFcmTokenInfoMobileAbrikChat
{
    [Key]
    public int Id { get; set; }

    public int? UserId { get; set; }

    [StringLength(1000)]
    public string? FcmToken { get; set; }

    [StringLength(200)]
    public string? DeviceId { get; set; }

    // Original SQL stores Unix epoch (bigint). Use long to match and map in config if needed.
    public long? AddedDate { get; set; }

    [ForeignKey("UserId")]
    public virtual KciUser? User { get; set; }
}
