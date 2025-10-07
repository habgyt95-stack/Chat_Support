using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat_Support.Domain.Entities;

public class ChatLoginOtp
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(256)]
    public required string CodeHash { get; set; }

    [Required]
    public DateTime ExpirationTime { get; set; }

    public bool IsUsed { get; set; } = false;

    public int Attempts { get; set; } = 0; 

    [Required]
    public DateTime CreationDate { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    public virtual KciUser? User { get; set; }
}
