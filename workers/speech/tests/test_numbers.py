import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import numbers, pipeline  # noqa: E402


def digits(text):
    return numbers.normalise_line(text)[0]


class CardinalTests(unittest.TestCase):
    def test_numbers_of_several_words_are_written_in_digits(self):
        cases = {
            "hai mươi ba người": "23 người",
            "mười lăm": "15",
            "mười hai con giáp": "12 con giáp",
            "hai mươi mốt": "21",
            "hai mươi một": "21",
            "bốn mươi tư": "44",
            "năm mươi": "50",
            "một trăm": "100",
            "một trăm linh năm": "105",
            "hai trăm lẻ một": "201",
            "một trăm hai mươi": "120",
            "hai nghìn": "2000",
            "ba ngàn": "3000",
            "mười lăm nghìn": "15.000",
            "hai nghìn mười": "2010",
        }
        for spoken, written in cases.items():
            with self.subTest(spoken):
                self.assertEqual(digits(spoken), written)

    def test_a_year_in_full_or_in_short(self):
        self.assertEqual(digits("Hai nghìn không trăm hai mươi sáu."), "2026.")
        self.assertEqual(digits("hai nghìn không trăm hai sáu"), "2026")
        self.assertEqual(digits("hai nghìn hai mươi"), "2020")
        self.assertEqual(digits("hai nghìn không trăm linh năm"), "2005")

    def test_the_tens_shorthand_with_a_unit_only_tens_take(self):
        self.assertEqual(digits("hai tư giờ"), "24 giờ")
        self.assertEqual(digits("ba lăm"), "35")
        self.assertEqual(digits("hai mốt"), "21")

    def test_a_digit_after_a_scale_word_counts_a_tenth_of_it(self):
        self.assertEqual(digits("một trăm hai"), "120")
        self.assertEqual(digits("hai nghìn hai"), "2200")
        self.assertEqual(digits("hai triệu hai"), "2.200.000")

    def test_digits_read_one_by_one_are_a_code_or_a_year(self):
        self.assertEqual(digits("năm hai không hai sáu"), "năm 2026")
        self.assertEqual(digits("số không chín không bốn năm sáu bảy tám"), "số 09045678")

    def test_case_from_the_tagger_does_not_matter(self):
        self.assertEqual(digits("Hai Mươi Ba"), "23")


class LargeNumberTests(unittest.TestCase):
    def test_round_millions_and_billions_keep_the_word(self):
        self.assertEqual(digits("hai trăm triệu đồng"), "200 triệu đồng")
        self.assertEqual(digits("năm triệu đồng"), "5 triệu đồng")
        self.assertEqual(digits("hai tỷ ba trăm triệu"), "2 tỷ 300 triệu")
        self.assertEqual(digits("một nghìn tỷ"), "1000 tỷ")

    def test_any_other_large_number_is_written_out_with_dots(self):
        self.assertEqual(digits("hai triệu năm trăm nghìn đồng"), "2.500.000 đồng")
        self.assertEqual(digits("một triệu hai trăm ba mươi tư nghìn năm trăm sáu mươi bảy"), "1.234.567")


class DecimalPercentTimeDateTests(unittest.TestCase):
    def test_decimals_take_a_comma(self):
        self.assertEqual(digits("năm phẩy năm"), "5,5")
        self.assertEqual(digits("không phẩy sáu"), "0,6")
        self.assertEqual(digits("ba phẩy mười bốn"), "3,14")
        self.assertEqual(digits("hai phẩy không năm"), "2,05")
        self.assertEqual(digits("Vòng Phẩy: Năm Lăm Tới Không Phẩy Sáu."), "Vòng Phẩy: 55 Tới 0,6.")

    def test_percentages(self):
        self.assertEqual(digits("mười lăm phần trăm."), "15%.")
        self.assertEqual(digits("năm phần trăm"), "5%")
        self.assertEqual(digits("năm phẩy năm phần trăm"), "5,5%")
        self.assertEqual(digits("tám mươi phần trăm cái khả năng"), "80% cái khả năng")
        self.assertEqual(digits("một trăm phần trăm"), "100%")

    def test_times(self):
        self.assertEqual(digits("ba giờ mười lăm phút"), "3 giờ 15 phút")
        self.assertEqual(digits("ba mươi phút đến hai mươi phút"), "30 phút đến 20 phút")
        self.assertEqual(digits("mười hai giờ"), "12 giờ")

    def test_dates(self):
        self.assertEqual(digits("ngày hai mươi hai tháng mười một năm hai nghìn không trăm hai mươi sáu"),
                         "ngày 22 tháng 11 năm 2026")
        self.assertEqual(digits("ngày hai hai tháng mười một"), "ngày 22 tháng 11")
        self.assertEqual(digits("mùng hai tháng chín"), "mùng 2 tháng 9")
        self.assertEqual(digits("tháng hai năm hai nghìn hai mươi"), "tháng 2 năm 2020")


class WordsThatStayTests(unittest.TestCase):
    def test_words_that_only_look_like_numbers(self):
        for text in [
            "thứ hai tôi họp",
            "thứ năm, thứ tư",
            "một số người, một lúc, hai người",
            "mười phân vẹn mười",
            "có một không hai",
            "chờ một phút",
            "hàng trăm người, trăm năm, hàng nghìn",
            "hai ba người, sáu bảy triệu",
            "hai ba trăm người",
            "tháng hai",
            "ngày hai",
            "hai tháng",
            "không phải",
        ]:
            with self.subTest(text):
                self.assertEqual(digits(text), text)

    def test_five_after_tens_is_lam_so_nam_there_is_years(self):
        self.assertEqual(digits("hai mươi năm qua"), "20 năm qua")
        self.assertEqual(digits("hai nghìn năm"), "2000 năm")

    def test_an_ordinal_of_several_words_is_written_in_digits(self):
        self.assertEqual(digits("thứ mười lăm"), "thứ 15")

    def test_a_mark_between_two_words_ends_the_number(self):
        self.assertEqual(digits("hai nghìn. không trăm. hai sáu"), "2000. không trăm. hai sáu")


class TeamSentenceTests(unittest.TestCase):
    """What the team said to test it on 21/11/2026."""

    def test_two_digit_words_stay_a_range_unless_only_tens_take_the_unit(self):
        self.assertEqual(digits("hai hai, hai ba, hai tư"), "hai hai, hai ba, 24")

    def test_digits_read_one_by_one_before_a_scale_word(self):
        self.assertEqual(digits("hai không hai sáu bảy triệu"), "20267 triệu")

    def test_a_year_with_its_hundreds_said(self):
        self.assertEqual(digits("hai nghìn không trăm hai sáu"), "2026")


class AlignmentTests(unittest.TestCase):
    def test_a_merged_word_starts_with_its_first_word_and_is_as_sure_as_its_least_sure(self):
        text, times, scores = numbers.normalise_line(
            "Năm nay có hai mươi ba người.", [0, 300, 600, 900, 1100, 1300, 1600], [99, 98, 97, 90, 35, 80, 99])
        self.assertEqual(text, "Năm nay có 23 người.")
        self.assertEqual(times, [0, 300, 600, 900, 1600])
        self.assertEqual(scores, [99, 98, 97, 35, 99])
        self.assertEqual(len(times), len(text.split()))

    def test_a_number_split_round_its_scale_word_keeps_a_time_for_each_part(self):
        text, times, scores = numbers.normalise_line("hai tỷ ba trăm triệu đồng", [0, 1, 2, 3, 4, 5], [9, 8, 7, 6, 5, 4])
        self.assertEqual(text, "2 tỷ 300 triệu đồng")
        self.assertEqual(times, [0, 1, 2, 4, 5])
        self.assertEqual(scores, [9, 8, 6, 5, 4])

    def test_a_percentage_takes_in_the_words_phan_tram(self):
        text, times, scores = numbers.normalise_line("tăng mười lăm phần trăm.", [0, 10, 20, 30, 40], [90, 80, 70, 60, 50])
        self.assertEqual((text, times, scores), ("tăng 15%.", [0, 10], [90, 50]))

    def test_unknown_or_misaligned_values_stay_unknown(self):
        self.assertEqual(numbers.normalise_line("hai mươi ba", None, None), ("23", None, None))
        self.assertEqual(numbers.normalise_line("hai mươi ba", [0, 1], [5, 5, 5]), ("23", None, [5]))

    def test_a_line_without_numbers_keeps_its_values(self):
        self.assertEqual(numbers.normalise_line("Xin chào.", [0, 400], [90, 40]), ("Xin chào.", [0, 400], [90, 40]))

    def test_every_rewritten_word_comes_with_the_span_it_replaces(self):
        spans = numbers.rewrite("ngày hai hai tháng mười một")
        self.assertEqual(spans, [("ngày", 0, 1), ("22", 1, 3), ("tháng", 3, 4), ("11", 4, 6)])


class LineWordsTests(unittest.TestCase):
    def test_the_worker_sends_digits_with_times_and_scores_still_one_per_word(self):
        self.assertEqual(
            pipeline.line_words("Còn mười lăm phút.", [0, 200, 400, 700], [90, 60, 95, 99]),
            ("Còn 15 phút.", [0, 200, 700], [90, 60, 99]),
        )

    def test_with_numbers_off_the_words_stay(self):
        self.assertEqual(
            pipeline.line_words("Còn mười lăm phút.", [0, 200, 400, 700], [90, 60, 95, 99], digits=False),
            ("Còn mười lăm phút.", [0, 200, 400, 700], [90, 60, 95, 99]),
        )

    def test_times_that_do_not_match_the_punctuated_words_are_not_sent(self):
        self.assertEqual(pipeline.line_words("Còn mười lăm phút.", [0, 200], None), ("Còn 15 phút.", None, None))


class MeetingSentenceTests(unittest.TestCase):
    """Edge cases from the room recording of the meeting of 22/11/2026."""

    def test_a_mark_the_tagger_put_after_phay_does_not_cut_the_decimal(self):
        self.assertEqual(digits("giống nhau từ không phẩy. bảy hai lên trở lên"), "giống nhau từ 0,72 lên trở lên")
        self.assertEqual(digits("giống nhau chia không phẩy. bảy hai."), "giống nhau chia 0,72.")
        # The tagger started a sentence after the number: its mark moves there.
        self.assertEqual(digits("để cả hai còn không phẩy? bảy Ý em nghĩ"), "để cả hai còn 0,7? Ý em nghĩ")

    def test_a_mark_between_an_hour_and_its_minutes_goes(self):
        self.assertEqual(digits("Bây giờ là mười bốn giờ. hai mươi lăm này"), "Bây giờ là 14 giờ 25 này")
        self.assertEqual(digits("cho đến mười ba giờ. Mười lẻ hai giây,"), "cho đến 13 giờ 10 lẻ 2 giây,")
        self.assertEqual(digits("mười giờ. Không năm phút"), "10 giờ 05 phút")

    def test_a_unit_left_alone_after_a_mark_belongs_to_the_number(self):
        self.assertEqual(digits("không được phép vượt quá một trăm năm mươi. Trang"),
                         "không được phép vượt quá 150 trang.")
        self.assertEqual(digits("vượt quá một trăm năm mươi. Trang."), "vượt quá 150 trang.")

    def test_a_mark_stays_where_the_words_may_be_two_sentences(self):
        # A count of people, not minutes; "phẩy" after no number; a name starting a sentence; 100 then 50 pages,
        # where the tagger may be right; nothing a decimal could end with.
        self.assertEqual(digits("ba giờ. Hai mươi người đến"), "3 giờ. 20 người đến")
        self.assertEqual(digits("dấu phẩy. Bảy người"), "dấu phẩy. Bảy người")
        self.assertEqual(digits("một trăm năm mươi. Trang nói"), "150. Trang nói")
        self.assertEqual(digits("không được quá một trăm. năm mươi trang là tương đối"),
                         "không được quá 100. 50 trang là tương đối")
        self.assertEqual(digits("ba phẩy. Không phải"), "ba phẩy. Không phải")

    def test_two_digit_pairs_in_a_range_from_to(self):
        self.assertEqual(digits("khoảng ngày từ hai hai đến hai bảy Này thì"), "khoảng ngày từ 22 đến 27 Này thì")
        self.assertEqual(digits("từ hai ba tới hai tám người"), "từ 23 tới 28 người")
        self.assertEqual(digits("từ mười hai đến hai bảy"), "từ 12 đến 27")
        self.assertEqual(digits("từ hai mươi đến hai bảy"), "từ 20 đến 27")
        # Without "từ … đến" two digit words stay a range of single digits.
        self.assertEqual(digits("hai ba người đến"), "hai ba người đến")
        self.assertEqual(digits("từ hai ba người"), "từ hai ba người")
        self.assertEqual(digits("từ năm hai đến năm ba"), "từ năm hai đến năm ba")

    def test_cham_between_two_single_digits_is_a_version(self):
        self.assertEqual(digits("lúc đấy opus bốn chấm năm mới thay đổi"), "lúc đấy opus 4.5 mới thay đổi")
        self.assertEqual(digits("chương hai chấm ba"), "chương 2.3")
        self.assertEqual(digits("Bốn chấm năm á? Bốn chấm năm là nhiều"), "4.5 á? 4.5 là nhiều")
        self.assertEqual(digits("em nhớ bốn chấm một hay gì bốn chấm không?"), "em nhớ 4.1 hay gì bốn chấm không?")
        self.assertEqual(digits("bản bốn chấm không mới ra"), "bản 4.0 mới ra")
        self.assertEqual(digits("ba chấm năm nay"), "ba chấm năm nay")
        self.assertEqual(digits("bốn chấm"), "bốn chấm")
        self.assertEqual(digits("hai chấm điểm"), "hai chấm điểm")

    def test_the_converter_never_adds_a_hundred(self):
        # "400 năm" in the meeting came from the recogniser's "bốn trăm năm"; "chấm" is never read as "trăm".
        self.assertEqual(digits("opus bốn trăm năm ra"), "opus 400 năm ra")
        self.assertEqual(digits("opus bốn chấm năm ra"), "opus 4.5 ra")

    def test_minutes_said_as_one_word_after_an_hour_in_digits(self):
        self.assertEqual(digits("từ chín giờ mười đến mười hai giờ hai mươi"), "từ 9 giờ 10 đến 12 giờ 20")
        self.assertEqual(digits("mười ba giờ không năm lẻ hai giây"), "13 giờ 05 lẻ 2 giây")
        self.assertEqual(digits("hai giờ mười người"), "2 giờ mười người")
        self.assertEqual(digits("bây giờ mười người"), "bây giờ mười người")

    def test_a_word_said_twice_before_the_hour_stays(self):
        # "lúc mười… mười sáu giờ": the speaker started again, the first "mười" is not part of the hour.
        self.assertEqual(digits("lúc mười mười sáu giờ, mười bảy giờ rồi"), "lúc mười 16 giờ, 17 giờ rồi")

    def test_times_and_scores_stay_one_per_word_across_a_mended_mark(self):
        text, times, scores = numbers.normalise_line(
            "còn không phẩy? bảy Ý em", [0, 10, 20, 30, 40, 50], [90, 80, 70, 60, 50, 40])
        self.assertEqual(text, "còn 0,7? Ý em")
        self.assertEqual(times, [0, 10, 40, 50])
        self.assertEqual(scores, [90, 60, 50, 40])
        text, times, _ = numbers.normalise_line("quá một trăm năm mươi. Trang", [0, 1, 2, 3, 4, 5], None)
        self.assertEqual((text, times), ("quá 150 trang.", [0, 1, 5]))


if __name__ == "__main__":
    unittest.main()
