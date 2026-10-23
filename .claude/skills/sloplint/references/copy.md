# Copy tells and fixes (English and Vietnamese)

Generated copy converges the same way generated visuals do: it reaches for the words that are safe
everywhere, so it says nothing about this product in particular. This file covers tells W1–W5 in
`tells.json`, in English and Vietnamese, and ends with a word list the scanner reads.

The rule behind every section: **say what the product does, for whom, with a concrete noun and
verb.** If a sentence would fit any product on the internet, rewrite it or delete it.

---

## 1. Buzzwords (W1)

A buzzword promises an effect without naming a mechanism. "Seamless" tells the reader nothing they
can check; "Imports your Jira backlog in one click" does.

**English.** unlock, supercharge, seamless, elevate, unleash, empower, effortless,
revolutionise, transform your…, reimagine, streamline, harness, leverage, cutting-edge,
next-generation, game-changing, world-class, best-in-class, state-of-the-art, all-in-one
platform, take … to the next level, built for the future, scale without limits, "in today's
fast-paced world", delve.

Sentence shapes to watch, which a word list cannot catch:

- **"Not just X, it's Y."** ("It's not just a calendar, it's your team's command centre.")
- **Two-beat slogans.** "Build faster. Ship smarter." "Less noise. More focus."
- **Abstract triplets.** "Fast, secure and scalable." Three adjectives, no evidence.

**Vietnamese.** Vietnamese marketing copy has its own stock phrases, often calqued from English
and usually stacked two or three per sentence:

nâng tầm, giải pháp toàn diện, trải nghiệm vượt trội, hiệu suất vượt trội, đột phá, bứt phá,
tối ưu hoá mọi…, liền mạch, kỷ nguyên số, thời đại số, chuyển đổi số toàn diện, mở khoá tiềm
năng, khai phá tiềm năng, hàng đầu, tiên phong, dẫn đầu xu hướng, đẳng cấp, đỉnh cao, tầm cao
mới, cách mạng hoá, công nghệ tiên tiến, hệ sinh thái toàn diện, tất cả trong một, "không chỉ
là… mà còn là…", "trong thời đại công nghệ 4.0".

Vietnamese spelling has two accepted tone-mark placements (*hoá* / *hóa*, *khoá* / *khóa*). The
JSON list below carries both, and the scanner should normalise text to Unicode NFC before
matching.

**Fix.** Replace the claim with the fact that would make it true.

| Slop | Rewrite |
|---|---|
| Elevate your meetings with a seamless, all-in-one platform. | Agenda, documents, votes and minutes for one meeting, in one place. |
| Unlock your team's potential. | Assign a reviewer and see who has signed off. |
| Nâng tầm trải nghiệm họp với giải pháp toàn diện, liền mạch. | Lịch họp, tài liệu, biểu quyết và biên bản của một cuộc họp ở cùng một chỗ. |
| Đột phá trong chuyển đổi số toàn diện cho doanh nghiệp. | Gửi giấy mời, nhận xác nhận tham dự và lưu biên bản đã ký, không cần in giấy. |

## 2. Generic calls to action (W2)

A button label should say what happens when you press it. Anthropic's frontend skill puts it as
"'Save changes,' not 'Submit'", and Cloudscape asks for one or two words, a verb with no
articles. The result message then reuses the verb: the button says "Publish", the toast says
"Published".

| Generic | Better (English) | Generic (VI) | Better (VI) |
|---|---|---|---|
| Submit | Save changes, Send invitation, Create meeting | Gửi, Xác nhận | Lưu thay đổi, Gửi lời mời, Tạo cuộc họp |
| Get started | Create your first meeting, Start free trial | Bắt đầu, Bắt đầu ngay | Tạo cuộc họp đầu tiên, Dùng thử miễn phí |
| Learn more | Read the pricing, See how voting works | Tìm hiểu thêm, Xem thêm | Xem bảng giá, Xem cách biểu quyết |
| Click here | (link the words that name the target) | Bấm vào đây, Nhấn vào đây | (gắn link vào chính tên trang đích) |
| OK (on a destructive dialog) | Delete meeting | Đồng ý | Xoá cuộc họp |
| Explore now, Try it now | Open the demo meeting | Khám phá ngay, Trải nghiệm ngay | Mở cuộc họp mẫu |

"Continue" and "Next" are fine inside a multi-step flow: there the next step is the object.

## 3. Exclamation and cheerfulness (W4)

Atlassian and Fluent both say to avoid exclamation marks in UI copy; Carbon says not to joke in
empty states. Cheer in a work tool reads as noise the tenth time someone sees it, and it is
painful next to an error.

| Slop | Calm |
|---|---|
| Oops! Something went wrong! | Couldn't save the agenda. Check your connection and try again. |
| Awesome! Your meeting is created! 🎉 | Meeting created. |
| Woohoo, you're all caught up! | No new notifications. |
| Ối! Có lỗi xảy ra rồi! | Chưa lưu được chương trình họp. Kiểm tra kết nối rồi thử lại. |
| Tuyệt vời! Bạn đã tạo cuộc họp thành công! | Đã tạo cuộc họp. |
| Chúc mừng! Bạn đã hoàn thành tất cả! | Không còn việc chờ xử lý. |

One exclamation mark in a whole product is a choice. One per screen is a tell.

## 4. Template leakage (W5)

Placeholder content that ships is the strongest copy tell, because no human would publish it on
purpose. It comes from component demos, seed data and prompts that were never replaced.

- **Filler text:** lorem ipsum, "dolor sit amet", "Sample text", "Your text here".
- **Unresolved slots:** `[Name]`, `{{user}}`, `{name}`, `undefined`, `NaN`, `null` in visible text.
- **Placeholder people and companies:** John Doe, Jane Doe, Acme Inc, Acme Corp, "Your Company".
- **Placeholder contact data:** `example.com`, `user@example.com`, `123 Main Street`, `(555) …`.
- **Vietnamese equivalents:** Nguyễn Văn A, Trần Thị B, Công ty ABC, Công ty XYZ, "Tên công ty",
  "Nội dung mẫu", "Văn bản mẫu", `email@example.com`.

**Fix.** Use real data from the product, realistic test data that is clearly yours, or an honest
empty state. In seed data that a user may see, prefer plausible names over textbook placeholders.
`example.com` is fine inside documentation and form placeholders that show a format; it is a tell
when it appears as content.

"Your Company", "Tên công ty", `NaN` and `null` are listed above for people reviewing a screen, but
they are left out of the scanner list: they are also real form labels or ordinary words ("nan
giải" is everyday Vietnamese), and W5 weighs 3, so a false match would cost too much.

## 5. Copy that explains the screen (W3)

Generated screens often narrate themselves. The heading already says where you are; a subtitle
that restates it adds a line to read and nothing to know.

Patterns:

- **The restated title.** "Users" over "Manage all your users in one place."
- **The tour guide.** "This page shows…", "Here you can…", "Use the button below to…";
  "Trang này hiển thị…", "Tại đây bạn có thể…", "Bấm nút bên dưới để…".
- **Section notes.** A grey sentence under every card heading describing what the card is.
- **Developer leakage.** "Data loaded from the API", "Built with Next.js", "Powered by AI",
  "This is a demo component", TODO notes and stack descriptions in visible text.
- **The everything-subtitle.** "Manage, track and optimise your meetings efficiently."

**Fix.** Delete it. If the line under a heading is worth keeping, make it carry data: a count, a
date range, the filter in force ("12 meetings this week"). Explanations that a first-time user
genuinely needs go in a tooltip, a help link or the empty state.

## 6. Error messages

An error message has two jobs: name what failed, and say what the user can do next. Anthropic's
skill says errors "don't apologize"; Primer says error states are not playful; Atlassian and
Fluent drop the exclamation marks.

**Pattern:** `[What failed, in the user's words]. [What to do now].`

| Slop | Fixed |
|---|---|
| Sorry! Something went wrong. Please try again later. | Couldn't upload "minutes.pdf": the file is over 20 MB. Compress it or split it into parts. |
| An error occurred (500). | The server didn't respond. Your draft is kept; try saving again in a minute. |
| Invalid input! | Enter a date after the meeting start (14:00). |
| Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại sau! | Chưa tải lên được "bien-ban.pdf": tệp lớn hơn 20 MB. Hãy nén hoặc tách thành nhiều tệp. |
| Dữ liệu không hợp lệ! | Nhập giờ kết thúc sau giờ bắt đầu (14:00). |
| Lỗi hệ thống. | Máy chủ chưa phản hồi. Bản nháp vẫn được giữ; thử lưu lại sau một phút. |

Rules of thumb:

- No "Sorry", "Oops", "Xin lỗi", "Rất tiếc" as an opener. An apology delays the information.
- No raw codes as the whole message. A code can follow the sentence for support.
- Keep what the user typed. Say so when you did ("Your draft is kept").
- Put field errors next to the field, not only in a toast.
- An empty state is not an error state (Cloudscape): "No meetings yet" and "Couldn't load
  meetings" are different screens.

---

## Scanner word lists

The DOM checker reads the block below. Conventions:

- Entries are lowercase and in Unicode NFC. Matching is case-insensitive; page text should be in
  NFC too (most pages are).
- `buzzwords` and `templateLeak` match anywhere in visible text, but bounded: the character just
  before and just after a match must not be a letter. So "nguyễn văn a" matches "Nguyễn Văn A"
  but not "Nguyễn Văn An". Unresolved `{{…}}` and `${…}` slots are caught by a separate built-in
  pattern in the checker, so they need no entry here.
- `genericCta` match a **whole** button or link label after trimming (so "Gửi" matches a button
  labelled exactly "Gửi", not "Gửi lời mời").
- No regex syntax; every entry is a literal string.
- Some Vietnamese entries also occur in plain speech ("hàng đầu" in "hàng đầu tiên", the first
  row). W1 carries weight 2 and the score counts a tell once per page, so one stray match does not
  decide anything; a project that keeps hitting one can allow W1 for that selector.

```json
{
  "buzzwords": {
    "en": [
      "unlock",
      "supercharge",
      "seamless",
      "seamlessly",
      "elevate",
      "unleash",
      "empower",
      "effortless",
      "effortlessly",
      "revolutionise",
      "revolutionize",
      "transform your",
      "reimagine",
      "streamline",
      "harness the power",
      "leverage",
      "cutting-edge",
      "next-generation",
      "next-gen",
      "game-changing",
      "game-changer",
      "world-class",
      "best-in-class",
      "state-of-the-art",
      "to the next level",
      "built for the future",
      "scale without limits",
      "fast-paced world",
      "delve",
      "not just a",
      "all-in-one",
      "reimagined",
      "redefine",
      "synergy",
      "like never before",
      "next-level",
      "blazing fast",
      "lightning-fast"
    ],
    "vi": [
      "nâng tầm",
      "giải pháp toàn diện",
      "trải nghiệm vượt trội",
      "hiệu suất vượt trội",
      "đột phá",
      "bứt phá",
      "tối ưu hoá mọi",
      "tối ưu hóa mọi",
      "liền mạch",
      "kỷ nguyên số",
      "thời đại số",
      "chuyển đổi số toàn diện",
      "mở khoá tiềm năng",
      "mở khóa tiềm năng",
      "khai phá tiềm năng",
      "hàng đầu",
      "tiên phong",
      "dẫn đầu xu hướng",
      "đẳng cấp",
      "đỉnh cao",
      "tầm cao mới",
      "cách mạng hoá",
      "cách mạng hóa",
      "công nghệ tiên tiến",
      "hệ sinh thái toàn diện",
      "tất cả trong một",
      "công nghệ 4.0",
      "vượt trội"
    ]
  },
  "genericCta": {
    "en": [
      "submit",
      "get started",
      "get started now",
      "learn more",
      "read more",
      "find out more",
      "discover more",
      "click here",
      "explore now",
      "try it now",
      "start now",
      "let's go",
      "explore"
    ],
    "vi": [
      "gửi",
      "xác nhận",
      "bắt đầu",
      "bắt đầu ngay",
      "tìm hiểu thêm",
      "xem thêm",
      "bấm vào đây",
      "nhấn vào đây",
      "click vào đây",
      "khám phá ngay",
      "trải nghiệm ngay",
      "đăng ký ngay",
      "khám phá"
    ]
  },
  "templateLeak": [
    "lorem ipsum",
    "dolor sit amet",
    "john doe",
    "jane doe",
    "acme inc",
    "acme corp",
    "sample text",
    "your text here",
    "example.com",
    "123 main street",
    "[name]",
    "nguyễn văn a",
    "trần thị b",
    "công ty abc",
    "công ty xyz",
    "nội dung mẫu",
    "văn bản mẫu",
    "undefined",
    "[object object]"
  ]
}
```
