using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>Who chairs the meeting and whom to ask about it, read when the email is built.</summary>
/// <param name="OrganizerEmail">Null in a Guest's email: a Guest is told whom to ask, not given a staff address.</param>
internal sealed record MeetingPeople(string? Chair, string Organizer, string? OrganizerEmail)
{
    public string Contact => OrganizerEmail is null ? Organizer : $"{Organizer} ({OrganizerEmail})";
}

/// <summary>
/// The HTML part of each email TrustCabinet sends, from the same facts as its plain-text part. A Guest's email never
/// carries the meeting's place or description: a Guest learns them only after verifying the address (UC-04).
/// </summary>
internal static class EmailTemplates
{
    private const string OpenMeeting = "Xem cuộc họp";
    private const string VerifyAsGuest = "Xác minh email để vào cuộc họp";

    /// <param name="link">The meeting page, or a Guest's verification link; null when the email offers no way in.</param>
    public static string Meeting(InvitationEventKind kind, InvitationPayload payload, string? link, bool guest, MeetingPeople people)
    {
        var when = EmailLayout.When(payload.StartsAt, payload.EndsAt);
        var mode = EmailLayout.ModeLabel(payload.Mode);
        var role = MeetingInvitationEvents.RoleLabel(payload.Role);
        var greeting = $"Xin chào {payload.Name},";
        var button = link is null ? null : new EmailButton(guest ? VerifyAsGuest : OpenMeeting, link);
        var guestNotice = guest
            ? new EmailNotice("Để xem địa điểm, tài liệu và vào phòng họp, bạn cần xác minh email này. Bấm nút dưới đây, "
                + "hệ thống sẽ gửi một mã 6 số tới email này.")
            : null;

        List<EmailFact> Details(bool withRole)
        {
            var facts = new List<EmailFact> { new("Thời gian", when, Strong: true), new("Hình thức", mode) };
            if (!guest && payload.Place.Length > 0) facts.Add(new("Địa điểm", payload.Place));
            if (people.Chair is { } chair) facts.Add(new("Chủ toạ", chair));
            if (withRole) facts.Add(new("Vai trò của bạn", role));
            if (!guest && payload.Description.Trim().Length > 0) facts.Add(new("Mô tả", payload.Description.Trim()));
            return facts;
        }

        var content = kind switch
        {
            InvitationEventKind.Invitation => new EmailContent(
                Preheader: $"{when} · {mode}",
                Kicker: MeetingInvitationEvents.Title(kind),
                Heading: payload.MeetingTitle,
                Greeting: greeting,
                Lead: [guest
                    ? "Bạn được mời tham dự cuộc họp dưới đây với tư cách Khách."
                    : $"Bạn được mời tham dự cuộc họp dưới đây với vai trò {role}."],
                FactsTitle: "Thông tin cuộc họp",
                Facts: Details(withRole: !guest),
                Notice: guestNotice,
                Button: button,
                After: ["Thư có kèm tệp lịch (.ics): mở tệp để thêm cuộc họp vào lịch của bạn.", Ask(people)]),

            InvitationEventKind.RoleChanged => new EmailContent(
                Preheader: $"Vai trò mới: {role}",
                Kicker: MeetingInvitationEvents.Title(kind),
                Heading: payload.MeetingTitle,
                Greeting: greeting,
                Lead: [$"Vai trò của bạn trong cuộc họp dưới đây đã đổi thành {role}."],
                FactsTitle: "Thông tin cuộc họp",
                Facts: [new("Vai trò mới", role, Strong: true), .. Details(withRole: false)],
                Button: button,
                After: [Ask(people)]),

            InvitationEventKind.Rescheduled => new EmailContent(
                Preheader: $"Thời gian mới: {when}",
                Kicker: MeetingInvitationEvents.Title(kind),
                Heading: payload.MeetingTitle,
                Greeting: greeting,
                Lead: ["Cuộc họp bạn được mời đã đổi sang thời gian mới. Các thông tin khác giữ nguyên."],
                FactsTitle: "Thời gian họp",
                Facts: payload.PreviousStartsAt is { } oldStart && payload.PreviousEndsAt is { } oldEnd
                    ? [new("Thời gian cũ", EmailLayout.When(oldStart, oldEnd), Struck: true), new("Thời gian mới", when, Strong: true),
                        new("Hình thức", mode)]
                    : Details(withRole: false),
                Notice: guestNotice,
                Button: button,
                After: ["Nếu bạn đã thêm cuộc họp vào lịch của mình, hãy sửa lại theo thời gian mới.", Ask(people)]),

            InvitationEventKind.Cancelled => new EmailContent(
                Preheader: $"Lý do: {payload.Reason}",
                Kicker: MeetingInvitationEvents.Title(kind),
                Heading: payload.MeetingTitle,
                Greeting: greeting,
                Lead: ["Cuộc họp dưới đây đã bị huỷ. Bạn không cần tham dự."],
                FactsTitle: "Thông tin cuộc họp",
                Facts: [new("Thời gian dự kiến", EmailLayout.When(payload.StartsAt, payload.EndsAt), Struck: true),
                    new("Lý do huỷ", payload.Reason ?? "", Strong: true)],
                After: ["Thư có kèm tệp lịch (.ics): mở tệp để gỡ cuộc họp khỏi lịch nếu bạn đã thêm trước đó.", Ask(people)]),

            _ => new EmailContent(
                Preheader: "Bạn không còn trong thành phần tham dự cuộc họp này.",
                Kicker: MeetingInvitationEvents.Title(kind),
                Heading: payload.MeetingTitle,
                Greeting: greeting,
                Lead: ["Bạn không còn trong thành phần tham dự cuộc họp dưới đây, nên không cần tham dự."],
                FactsTitle: "Thông tin cuộc họp",
                Facts: [new("Thời gian", when, Struck: true)],
                After: [$"Nếu bạn cho rằng đây là nhầm lẫn, hãy liên hệ người tổ chức: {people.Contact}."]),
        };
        return EmailLayout.Render(content);
    }

    private static string Ask(MeetingPeople people) => $"Cần trao đổi thêm, liên hệ người tổ chức: {people.Contact}.";

    /// <summary>UC-04: the one-time code a Guest types to enter the meeting (BR-153).</summary>
    public static string GuestCode(string name, string meetingTitle, string code, int minutes)
        => EmailLayout.Render(new EmailContent(
            Preheader: $"Mã có hiệu lực trong {minutes} phút.",
            Kicker: "Mã xác minh vào cuộc họp",
            Heading: meetingTitle,
            Greeting: $"Xin chào {name},",
            Lead: ["Nhập mã dưới đây trên trang xác minh để vào cuộc họp."],
            Code: code,
            After: [$"Mã có hiệu lực trong {minutes} phút và chỉ dùng được một lần. Nếu bạn không yêu cầu mã này, hãy bỏ qua thư."]));

    /// <summary>UC-07: the administrator's reset link.</summary>
    public static string PasswordReset(string name, string link, TimeSpan validFor)
        => EmailLayout.Render(new EmailContent(
            Preheader: $"Liên kết có hiệu lực trong {validFor.TotalMinutes:0} phút.",
            Kicker: "Tài khoản TrustCabinet",
            Heading: "Đặt lại mật khẩu",
            Greeting: $"Xin chào {name},",
            Lead: ["Quản trị viên TrustCabinet vừa yêu cầu đặt lại mật khẩu cho tài khoản của bạn."],
            Notice: new EmailNotice($"Liên kết có hiệu lực trong {validFor.TotalMinutes:0} phút và chỉ dùng được một lần."),
            Button: new EmailButton("Đặt mật khẩu mới", link),
            After: ["Mật khẩu hiện tại vẫn dùng được cho tới khi bạn đặt mật khẩu mới. Nếu bạn không chờ thư này, "
                + "hãy bỏ qua nó và báo cho quản trị viên."]));

    /// <summary>BR-307: a Guest assigned to present an agenda item, with the same way in as their invitation.</summary>
    /// <param name="link">The Guest's verification link; null only when it cannot be issued right now.</param>
    public static string GuestPresenter(string name, string itemTitle, string meetingTitle, DateTimeOffset startsAt,
        DateTimeOffset endsAt, string mode, string? link, string organizer)
        => EmailLayout.Render(new EmailContent(
            Preheader: $"Mục: {itemTitle} · {EmailLayout.When(startsAt, endsAt)}",
            Kicker: "Phân công trình bày mục nghị sự",
            Heading: meetingTitle,
            Greeting: $"Xin chào {name},",
            Lead: ["Bạn được phân công trình bày một mục trong chương trình cuộc họp dưới đây."],
            FactsTitle: "Phần trình bày",
            Facts: [new("Mục nghị sự", itemTitle, Strong: true), new("Thời gian họp", EmailLayout.When(startsAt, endsAt)),
                new("Hình thức", EmailLayout.ModeLabel(mode))],
            Notice: link is null ? null : new EmailNotice("Bấm nút dưới đây để xác minh email, rồi xem chương trình, tài liệu "
                + "và vào phòng họp. Hệ thống sẽ gửi một mã 6 số tới email này."),
            Button: link is null ? null : new EmailButton("Xác minh email để vào cuộc họp", link),
            After: [Ask(new MeetingPeople(null, organizer, null))]));
}
