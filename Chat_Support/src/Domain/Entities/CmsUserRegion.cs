using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat_Support.Domain.Entities;

[Table("CMS_UserRegions")]
public partial class CmsUserRegion
{
    /// <summary>
    /// کلید
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// شناسه ناحیه ی اختصای به کاربر
    /// </summary>
    public int? RegionId { get; set; }

    public virtual KciUser? User { get; set; }
    public virtual Region? Region { get; set; }
}
