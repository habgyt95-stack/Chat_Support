using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat_Support.Domain.Entities;

public class AbrikChatUsersToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [StringLength(100)]
    public string? DeviceId { get; set; }

    [Required]
    [StringLength(500)]
    public required string RefreshToken { get; set; }

    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.Now;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public bool IsRevoked { get; set; } = false;

    [ForeignKey("UserId")]
    public virtual KciUser? User { get; set; }
}
