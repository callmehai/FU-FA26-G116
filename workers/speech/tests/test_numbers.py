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


if __name__ == "__main__":
    unittest.main()
