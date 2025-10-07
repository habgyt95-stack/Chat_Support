using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat_Support.Domain.Entities;

public class ChatUserRefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(256)]
    public required string Token { get; set; } 

    [Required]
    public DateTime ExpirationTime { get; set; }

    public bool IsRevoked { get; set; } = false;

    [Required]
    public DateTime CreationDate { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    public virtual KciUser? User { get; set; }
}
