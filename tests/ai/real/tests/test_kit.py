"""Claim splitting, auto-check, candidate lines and draft naming of kit.py. Standard library only:

    python3 -m unittest discover -s tests/ai/real/tests
"""

import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
import kit  # noqa: E402

DRAFT = """## I. Mục tiêu và phạm vi cuộc họp

Cuộc họp nhằm rà soát tiến độ dự án. Phạm vi gồm hai nội dung.

## II. Nội dung thảo luận

1. Tiến độ phần giao diện
- Anh Hải báo cáo đã xong màn hình đăng nhập; còn thiếu màn hình lịch.
- Chị Mai đề xuất dời hạn chót sang 30/11, đồng thời đề nghị nhóm kiểm thử chạy lại toàn bộ.
- Nhóm thống nhất dùng màu xanh và trắng.
2. Kiểm thử
   Người nói 2 cho biết đã chạy 120 ca kiểm thử và Lộc sẽ tổng hợp kết quả vào thứ Sáu.

## III. Kết luận

1. Kết luận của chủ toạ
- Hoàn thành màn hình lịch — Trần Việt Hải — 30/11
"""


def facts(**changes):
    meeting = {
        "meeting": {"title": "Họp tiến độ 21/11", "plannedStart": "2026-11-21T07:00:00+00:00",
                    "plannedEnd": "2026-11-21T08:00:00+00:00", "actualStart": "2026-11-21T07:05:00+00:00",
                    "actualEnd": "2026-11-21T08:10:00+00:00"},
        "agenda": [{"position": 1, "title": "Tiến độ phần giao diện", "start": None, "end": None,
                    "conclusion": "Dời hạn chót màn hình lịch sang ngày 30/11", "presenter": "Trần Việt Hải"}],
        "participants": [
            {"name": "Trần Việt Hải", "role": "Secretary", "attendance": "Present", "current": True},
            {"name": "Hoàng Đức Lộc", "role": "Chair", "attendance": "Present", "current": True},
            {"name": "Nguyễn Thị Mai", "role": "Member", "attendance": "Absent", "current": True},
            {"name": "Lê Văn Cũ", "role": "Member", "attendance": "Unknown", "current": False},
        ],
        "guests": [],
        "votes": [{"title": "Chọn màu giao diện", "opened": None, "closed": None, "conclusion": "Passed",
                   "options": [{"label": "Xanh", "count": 2}, {"label": "Đỏ", "count": 1}]}],
        "documents": [{"fileName": "ke-hoach.pdf"}],
        "transcript": [
            {"n": 1, "at": "2026-11-21T07:06:00+00:00", "clock": "14:06:00", "speaker": "Trần Việt Hải",
             "text": "Em xong màn hình đăng nhập rồi, còn màn hình lịch thôi.", "recordingOffset": "00:01:00"},
            {"n": 2, "at": "2026-11-21T07:07:00+00:00", "clock": "14:07:00", "speaker": "Người nói 2",
             "text": "Bên em chạy một trăm hai mươi ca kiểm thử rồi.", "recordingOffset": "00:02:00"},
            {"n": 3, "at": "2026-11-21T07:08:00+00:00", "clock": "14:08:00", "speaker": "Hoàng Đức Lộc",
             "text": "Thứ sáu anh tổng hợp kết quả kiểm thử.", "recordingOffset": "00:03:00"},
        ],
    }
    meeting.update(changes)
    return kit.facts_of(meeting)


class SplitClaims(unittest.TestCase):
    def setUp(self):
        self.claims = kit.split_claims(DRAFT)
        self.texts = [c["text"] for c in self.claims]

    def test_headings_are_sections_not_claims(self):
        self.assertNotIn("Tiến độ phần giao diện", self.texts)
        self.assertNotIn("Kết luận của chủ toạ", self.texts)
        self.assertEqual(self.claims[2]["section"], "II. Nội dung thảo luận › 1. Tiến độ phần giao diện")

    def test_sentences_and_semicolons(self):
        self.assertEqual(self.texts[:2], ["Cuộc họp nhằm rà soát tiến độ dự án.", "Phạm vi gồm hai nội dung."])
        self.assertIn("Anh Hải báo cáo đã xong màn hình đăng nhập", self.texts)
        self.assertIn("Còn thiếu màn hình lịch.", self.texts)

    def test_dong_thoi_splits_two_clauses(self):
        self.assertIn("Chị Mai đề xuất dời hạn chót sang 30/11", self.texts)
        self.assertIn("Đề nghị nhóm kiểm thử chạy lại toàn bộ.", self.texts)

    def test_va_between_nouns_stays(self):
        self.assertIn("Nhóm thống nhất dùng màu xanh và trắng.", self.texts)

    def test_va_between_clauses_splits_and_indented_paragraph_kept(self):
        self.assertIn("Người nói 2 cho biết đã chạy 120 ca kiểm thử", self.texts)
        self.assertIn("Lộc sẽ tổng hợp kết quả vào thứ Sáu.", self.texts)

    def test_task_line_is_one_claim_and_numbering_is_dense(self):
        self.assertEqual(self.texts[-1], "Hoàn thành màn hình lịch — Trần Việt Hải — 30/11")
        self.assertEqual([c["n"] for c in self.claims], list(range(1, len(self.claims) + 1)))

    def test_numbered_list_item_with_sentence_is_a_claim(self):
        claims = kit.split_claims("1. Hải đã sửa lỗi đăng nhập.\n2. Lộc sẽ kiểm thử lại.")
        self.assertEqual([c["text"] for c in claims], ["Hải đã sửa lỗi đăng nhập.", "Lộc sẽ kiểm thử lại."])

    def test_deterministic(self):
        self.assertEqual(kit.split_claims(DRAFT), kit.split_claims(DRAFT))


class AutoCheck(unittest.TestCase):
    def setUp(self):
        self.facts = facts()

    def test_attendee_name_matches_with_role(self):
        self.assertEqual(kit.auto_check("Hoàn thành màn hình lịch — Trần Việt Hải", self.facts),
                         "Khớp dữ liệu: Trần Việt Hải = Trần Việt Hải (Thư ký)")

    def test_given_name_after_honorific(self):
        self.assertIn("Khớp dữ liệu: Mai = Nguyễn Thị Mai", kit.auto_check("Chị Mai đề xuất dời hạn", self.facts))

    def test_wrong_tone_and_unknown_name(self):
        self.assertIn("sai dấu", kit.auto_check("Anh Hai báo cáo tiến độ", self.facts))
        self.assertTrue(kit.auto_check("Trường Huy nêu cần kiểm thử", self.facts).startswith("Không khớp: không có 'Trường Huy'"))

    def test_removed_participant_is_not_an_attendee(self):
        self.assertTrue(kit.auto_check("Ông Lê Văn Cũ phát biểu", self.facts).startswith("Không khớp"))

    def test_speaker_label(self):
        self.assertEqual(kit.auto_check("Người nói 2 cho biết", self.facts), "Khớp dữ liệu: Người nói 2 có trong bản gỡ băng")
        self.assertTrue(kit.auto_check("Người nói 9 cho biết", self.facts).startswith("Không khớp"))

    def test_meeting_time(self):
        self.assertIn("14:05 = bắt đầu thực tế", kit.auto_check("Cuộc họp bắt đầu lúc 14 giờ 05", self.facts))
        self.assertTrue(kit.auto_check("Cuộc họp bắt đầu lúc 9h30", self.facts).startswith("Không khớp: 09:30"))
        self.assertEqual(kit.auto_check("Hạn nộp là 9h30 sáng mai", self.facts), "")

    def test_attendance_count(self):
        self.assertTrue(kit.auto_check("Có 2 thành viên có mặt", self.facts).startswith("Khớp dữ liệu"))
        self.assertTrue(kit.auto_check("Có 3/3 thành viên có mặt", self.facts).startswith("Không khớp"))

    def test_votes(self):
        self.assertIn("Khớp dữ liệu", kit.auto_check("Biểu quyết chọn màu giao diện: Xanh 2 phiếu, thông qua", self.facts))
        self.assertTrue(kit.auto_check("Biểu quyết chọn màu giao diện được 5 phiếu", self.facts).startswith("Không khớp"))
        self.assertTrue(kit.auto_check("Biểu quyết chọn màu giao diện không thông qua", self.facts).startswith("Không khớp"))
        no_votes = facts(votes=[])
        self.assertEqual(kit.auto_check("Biểu quyết thông qua phương án A", no_votes),
                         "Không khớp: hệ thống không có biểu quyết nào")
        self.assertEqual(kit.auto_check("Chưa biểu quyết nội dung này", no_votes), "")

    def test_agenda_title_and_conclusion(self):
        self.assertIn("mục 1 'Tiến độ phần giao diện' có trong chương trình",
                      kit.auto_check("Thảo luận về tiến độ phần giao diện", self.facts))
        self.assertIn("trùng kết luận đã ghi của mục 1",
                      kit.auto_check("Dời hạn chót màn hình lịch sang ngày 30/11", self.facts))

    def test_documents(self):
        self.assertIn("Khớp dữ liệu", kit.auto_check("Tài liệu ke-hoach.pdf đã gửi", self.facts))
        self.assertTrue(kit.auto_check("Tài liệu bao-cao.docx đã gửi", self.facts).startswith("Không khớp"))

    def test_plain_claim_left_blank(self):
        self.assertEqual(kit.auto_check("Nhóm thống nhất dùng màu xanh", facts(votes=[])), "")


class CandidatesAndNames(unittest.TestCase):
    def test_top_line_shares_words_and_spoken_numbers(self):
        meeting = facts()
        lines = [{"n": 1, "speaker": "A", "text": "Em xong màn hình đăng nhập rồi.", "clock": "14:06:00"},
                 {"n": 2, "speaker": "B", "text": "Chạy năm phút một lần thôi.", "clock": "14:07:00"},
                 {"n": 3, "speaker": "C", "text": "Thời tiết hôm nay đẹp.", "clock": "14:08:00"}]
        finder = kit.Candidates(lines)
        self.assertEqual(finder.top("Đã xong màn hình đăng nhập")[0]["n"], 1)
        self.assertEqual(finder.top("Ngắt khoảng 5 phút một lần")[0]["n"], 2)
        self.assertEqual(finder.top("Không liên quan gì cả xyz"), [])
        self.assertTrue(meeting["speakers"])

    def test_describe_has_clock_offset_and_speaker(self):
        line = {"clock": "14:06:00", "recordingOffset": "00:01:00", "speaker": "A", "text": "x" * 400}
        text = kit.describe(line)
        self.assertTrue(text.startswith("[14:06:00 (bản ghi 00:01:00)] A: "))
        self.assertTrue(text.endswith("…"))

    def test_draft_name_parsing(self):
        slugs = ["test-mic", "test"]
        self.assertEqual(kit.parse_draft_name("test-mic-api-2.md", slugs), ("test-mic", "api", 2))
        self.assertEqual(kit.parse_draft_name("test-mic-qwen2.5.7binstruct-1.md", slugs),
                         ("test-mic", "qwen2.5.7binstruct", 1))
        self.assertIsNone(kit.parse_draft_name("other-api-1.md", slugs))
        self.assertIsNone(kit.parse_draft_name("test-mic-api.md", slugs))

    def test_names(self):
        self.assertEqual(kit.find_names("Trần Việt Hải đề xuất tắt panel"), ["Trần Việt Hải"])
        self.assertEqual(kit.find_names("anh Hải trình bày; Kiểm tra cột Huy Ý"), ["Hải"])
        self.assertEqual(kit.find_names("Người nói 3 cho biết"), [])

    def test_reference_sheet_by_day_and_title(self):
        meeting = {"slug": "thu-ho", "meeting": {"title": "Test thu hộ 20/11", "actualStart": "2026-11-21T09:09:37+00:00",
                                                 "plannedStart": "2026-11-21T07:15:00+00:00"}}
        sheets = ["21-11 test mic", "21-11 Test thu ho", "22-11 hop voi thay"]
        self.assertEqual(kit.match_sheet(meeting, sheets, {}), "21-11 Test thu ho")
        self.assertEqual(kit.match_sheet(meeting, sheets, {"thu-ho": "X"}), "X")


if __name__ == "__main__":
    unittest.main()
