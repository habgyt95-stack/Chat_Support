

// Namespace انتیتی MessageReaction شما

namespace Chat_Support.Application.Chats.DTOs;

public class ReactionInfo
{
    public string Emoji { get; set; } = null!;
    public int Count { get; set; } // این پراپرتی را برای نمایش تعداد کل یک نوع اموجی اضافه می‌کنیم
    public bool IsReactedByCurrentUser { get; set; }
    public List<string> UserFullNames { get; set; } = new(); // لیستی از نام کاربرانی که این اموجی را گذاشته‌اند

    // این پروفایل را اضافه کنید
    private class Mapping : Profile
    {
        public Mapping()
        {
            // این مپینگ برای زمانی است که بخواهیم یک ری‌اکشن تکی را مپ کنیم
            // ما در QueryHandler از روش دیگری استفاده خواهیم کرد
        }
    }
}
