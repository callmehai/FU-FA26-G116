using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// The designed HTML part of every email. Set EMAIL_PREVIEW_DIR to also write each sample to that folder, with an
/// index page, to look at them in a browser.
/// </summary>
public sealed class EmailTemplatesTests
{
    // 09:00–10:30 on Monday 16/11/2026 in Hà Nội.
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddMinutes(90);
    private const string MeetingLink = "https://hop.example.vn/mgmt/meetings/01a1df5c-7a54-7d3e-8b18-d3e6b04be715";
    private const string GuestLink = "https://hop.example.vn/guest/01a1df5c-81d9-7683-aaa4-5df0fae044ad#token=abc&x=1";

    private static InvitationPayload Payload(string? role = "Member", string? reason = null,
        DateTimeOffset? previousStart = null, DateTimeOffset? previousEnd = null)
        => new("Nguyễn Văn An", "an@example.vn", "Rà soát kế hoạch chuyển đổi số quý IV",
            "Đánh giá tiến độ các hạng mục.\nTài liệu: https://hop.example.vn/docs/q4.", Start, End,
            "Phòng họp A, Tầng 5", "Hybrid", role, reason, previousStart, previousEnd);

    [Fact]
    public void Dates_read_in_Vietnamese_in_Ha_Noi_time()
    {
        Assert.Equal("09:00 – 10:30, Thứ Hai, 16/11/2026", EmailLayout.When(Start, End));
        Assert.Equal("23:00, Chủ Nhật, 15/11/2026 – 01:00, Thứ Hai, 16/11/2026",
            EmailLayout.When(Start.AddHours(-10), Start.AddHours(-8)));
        Assert.Equal("09:00, Thứ Hai, 16/11/2026", EmailLayout.At(Start));
    }

    [Fact]
    public void Text_people_typed_is_escaped_everywhere()
    {
        var payload = Payload(reason: "<b>hoãn</b> & \"đổi\"") with
        {
            Name = "<img src=x onerror=alert(1)>",
            MeetingTitle = "Họp <script>alert(1)</script>",
        };

        foreach (var kind in Enum.GetValues<InvitationEventKind>())
        {
            var html = EmailTemplates.Meeting(kind, payload, MeetingLink, guest: false);
            Assert.DoesNotContain("<script>", html);
            Assert.DoesNotContain("<img", html);
            Assert.Contains("Họp &lt;script&gt;", html);
        }
        Assert.Contains("&lt;b&gt;hoãn&lt;/b&gt; &amp; &quot;đổi&quot;",
            EmailTemplates.Meeting(InvitationEventKind.Cancelled, payload, null, guest: false));
    }

    [Fact]
    public void Internal_invitation_names_place_role_and_description_and_links_the_meeting()
    {
        var html = EmailTemplates.Meeting(InvitationEventKind.Invitation, Payload(), MeetingLink, guest: false);

        Assert.Contains("Phòng họp A, Tầng 5", html);
        Assert.Contains("Thành viên", html);
        Assert.Contains("Kết hợp", html);
        Assert.Contains("09:00 – 10:30, Thứ Hai, 16/11/2026", html);
        // The description's own address becomes a link too, without its trailing full stop.
        Assert.Contains("href=\"https://hop.example.vn/docs/q4\"", html);
        Assert.Contains($"href=\"{MeetingLink}\"", html);
        Assert.Contains(".ics", html);
    }

    [Fact]
    public void Guest_invitation_leads_to_verification_and_hides_place_and_description()
    {
        var html = EmailTemplates.Meeting(InvitationEventKind.Invitation, Payload(role: null), GuestLink, guest: true);

        Assert.Contains("href=\"https://hop.example.vn/guest/01a1df5c-81d9-7683-aaa4-5df0fae044ad#token=abc&amp;x=1\"", html);
        Assert.Contains("Xác minh email để vào cuộc họp", html);
        Assert.DoesNotContain("Phòng họp A", html);
        Assert.DoesNotContain("Đánh giá tiến độ", html);
        Assert.DoesNotContain("/mgmt/meetings", html);
    }

    [Fact]
    public void Withdrawals_offer_no_way_in()
    {
        Assert.DoesNotContain("href=", EmailTemplates.Meeting(InvitationEventKind.Cancelled, Payload(reason: "Lãnh đạo đi công tác"), null, false));
        Assert.DoesNotContain("href=", EmailTemplates.Meeting(InvitationEventKind.Removed, Payload(), null, false));
    }

    [Fact]
    public void Reschedule_strikes_the_old_time_through()
    {
        var html = EmailTemplates.Meeting(InvitationEventKind.Rescheduled,
            Payload(role: null, previousStart: Start.AddDays(-1), previousEnd: End.AddDays(-1)), GuestLink, guest: true);

        Assert.Contains("09:00 – 10:30, Chủ Nhật, 15/11/2026", html);
        Assert.Contains("line-through", html);
        Assert.Contains("09:00 – 10:30, Thứ Hai, 16/11/2026", html);
    }

    [Fact]
    public void Smtp_sends_the_designed_part_when_there_is_one()
    {
        var queued = new EmailMessage
        {
            DeduplicationKey = "test", ToAddress = "an@example.vn", Subject = "Thư", Body = "Nội dung",
            HtmlBody = EmailTemplates.GuestCode("An", "Họp giao ban", "042917", 15),
        };

        var message = SmtpEmailSender.BuildMessage(queued, new SmtpOptions { FromAddress = "no-reply@example.vn" });

        Assert.Equal(queued.HtmlBody, message.HtmlBody);
        Assert.Equal("Nội dung", message.TextBody);
    }

    [Fact]
    public void Writes_every_sample_when_asked()
    {
        var samples = new (string File, string Title, string Html)[]
        {
            ("01-moi-hop.html", "Mời họp (người trong hệ thống)",
                EmailTemplates.Meeting(InvitationEventKind.Invitation, Payload(), MeetingLink, false)),
            ("02-moi-hop-khach.html", "Mời họp (Khách)",
                EmailTemplates.Meeting(InvitationEventKind.Invitation, Payload(role: null), GuestLink, true)),
            ("03-ma-xac-minh.html", "Mã xác minh của Khách",
                EmailTemplates.GuestCode("Nguyễn Văn An", "Rà soát kế hoạch chuyển đổi số quý IV", "042917", 15)),
            ("04-doi-vai-tro.html", "Đổi vai trò",
                EmailTemplates.Meeting(InvitationEventKind.RoleChanged, Payload(role: "Secretary"), MeetingLink, false)),
            ("05-doi-lich.html", "Dời lịch (Khách)",
                EmailTemplates.Meeting(InvitationEventKind.Rescheduled,
                    Payload(role: null, previousStart: Start.AddDays(-1), previousEnd: End.AddDays(-1)), GuestLink, true)),
            ("06-huy-hop.html", "Huỷ họp",
                EmailTemplates.Meeting(InvitationEventKind.Cancelled, Payload(reason: "Lãnh đạo đi công tác, dời sang tuần sau."), null, false)),
            ("07-thu-hoi.html", "Thu hồi lời mời",
                EmailTemplates.Meeting(InvitationEventKind.Removed, Payload(), null, false)),
            ("08-trinh-bay.html", "Phân công trình bày (Khách)",
                EmailTemplates.GuestPresenter("Nguyễn Văn An", "Báo cáo tiến độ hạ tầng", "Rà soát kế hoạch chuyển đổi số quý IV", Start)),
            ("09-dat-lai-mat-khau.html", "Đặt lại mật khẩu",
                EmailTemplates.PasswordReset("Nguyễn Văn An", "https://hop.example.vn/reset-password#token=abc", TimeSpan.FromMinutes(30))),
        };
        foreach (var sample in samples) Assert.StartsWith("<!doctype html>", sample.Html);

        var folder = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        foreach (var sample in samples) File.WriteAllText(Path.Combine(folder, sample.File), sample.Html);
        File.WriteAllText(Path.Combine(folder, "index.html"),
            "<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><title>Email TrustCabinet</title>"
            + "<body style=\"margin:0;padding:24px;background:#e9edf2;font-family:Arial,sans-serif\">"
            + string.Concat(samples.Select(s => $"<h2 style=\"font-size:15px;color:#2a4d94\">{s.Title}</h2>"
                + $"<iframe src=\"{s.File}\" style=\"width:100%;max-width:680px;height:900px;border:0;display:block;margin-bottom:24px\"></iframe>"))
            + "</body></html>");
    }
}
