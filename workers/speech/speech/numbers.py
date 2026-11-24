"""Write the numbers of a Vietnamese transcript line in digits (inverse text normalisation).

Gipformer spells every number out: "hai nghìn không trăm hai mươi sáu", "năm phẩy năm", "mười lăm phần
trăm". This module turns them into what a person would write ("2026", "5,5", "15%"), and leaves alone
the words that only look like numbers ("thứ hai", "một số", "hai người", "có một không hai").

The rule is conservative: a wrong digit misleads the reader more than a number left in words.

* A number of two words or more that follows the grammar of Vietnamese numerals is converted:
  "hai mươi ba" → 23, "mười lăm" → 15, "một trăm linh năm" → 105, "hai nghìn không trăm hai sáu" → 2026,
  "hai trăm triệu" → "200 triệu".
* A number of a single word ("một", "hai", "mười") stays a word, unless what comes next makes it a
  quantity: a decimal ("năm phẩy năm" → 5,5), a percentage ("năm phần trăm" → 5%), a time ("ba giờ
  mười lăm phút" → "3 giờ 15 phút") or a date ("ngày hai tháng chín" → "ngày 2 tháng 9"). "Một" stays a
  word before a time unit too ("chờ một phút").
* Two digit words in a row are a range in speech ("hai ba người", "sáu bảy triệu"), so they stay words,
  except the tens shorthand with a unit only tens take ("hai mốt" → 21, "hai tư" → 24, "ba lăm" → 35),
  and inside a larger number or as the day of a date ("ngày hai hai tháng mười một" → "ngày 22 tháng 11").
* Four digit words or more read one by one are a code or a year: "hai không hai sáu" → 2026.
* After "thứ" a single word is a day of the week or an ordinal and stays: "thứ hai", "thứ năm".
* "năm" is a number only where the grammar needs it ("năm mươi", "năm trăm", "năm triệu"); after
  "mươi" and "mười" a 5 is said "lăm", so "hai mươi năm" is "20 năm" (twenty years).
* A punctuation mark the tagger put between two words ends a number there, except where the words around it can
  only be one number (`_mends`): after "phẩy" ("không phẩy. bảy hai" → 0,72), between an hour and its minutes ("14
  giờ. hai mươi lăm" → "14 giờ 25"), and before a unit left alone as the last word ("một trăm năm mươi. Trang" →
  "150 trang").
* "từ hai hai đến hai bảy" is a range of two-digit numbers, "từ 22 đến 27", not of single digits.
* "chấm" between two single digits is a version or a section: "bốn chấm năm" → 4.5.
* After an hour in digits, "mười" and "không năm" are its minutes: "9 giờ mười" → "9 giờ 10", "13 giờ 05".

Numbers from ten thousand up are grouped with dots ("15.000"), four-digit ones are not (a year), decimals
take a comma (Vietnamese usage). A round number of millions or billions keeps the word ("200 triệu",
"2 tỷ 300 triệu"); any other large number is written out ("2.500.000").

Each output word comes with the span of input words it replaces, so the word times and scores the
recogniser gave stay aligned one for one: a merged word starts when its first word starts and is as
sure as its least sure word (`carry`).
"""

from __future__ import annotations

from collections.abc import Callable, Sequence
from dataclasses import dataclass

MARKS = ".,:;?!…"

DIGITS = {"không": 0, "một": 1, "hai": 2, "ba": 3, "bốn": 4, "năm": 5, "sáu": 6, "bảy": 7, "bẩy": 7, "tám": 8,
          "chín": 9}
# The unit after "mươi": 1 is "mốt" (or "một"), 4 may be "tư", 5 is "lăm"/"nhăm" — "năm" there is "year".
AFTER_MUOI = {"mốt": 1, "một": 1, "hai": 2, "ba": 3, "bốn": 4, "tư": 4, "lăm": 5, "nhăm": 5, "sáu": 6, "bảy": 7,
              "bẩy": 7, "tám": 8, "chín": 9}
# The unit after "mười": as after "mươi", but 11 is "mười một".
AFTER_MUOI_10 = {k: v for k, v in AFTER_MUOI.items() if k != "mốt"}
# Units only tens take: "hai mốt", "hai tư", "ba lăm" can only be 21, 24, 35.
TENS_ONLY = {"mốt": 1, "tư": 4, "lăm": 5, "nhăm": 5}
# After "linh"/"lẻ": "một trăm linh năm" (105), "linh tư" (104).
AFTER_LINH = {**{k: v for k, v in DIGITS.items() if v}, "tư": 4}
LINH = {"linh", "lẻ"}
# A digit after a scale word, spoken alone, counts a tenth of it: "hai nghìn hai" is 2200, "trăm mốt" 110.
# "một" and "năm" are left out: "hai nghìn năm" may be 2005, 2500 or "two thousand years".
COLLOQUIAL = {"mốt": 1, "hai": 2, "ba": 3, "bốn": 4, "tư": 4, "sáu": 6, "bảy": 7, "bẩy": 7, "tám": 8, "chín": 9}
SCALES = {"nghìn": 1000, "ngàn": 1000, "triệu": 10**6, "tỷ": 10**9, "tỉ": 10**9}
TIME_UNITS = {"giờ", "phút", "giây"}
# Words a number can end with: a whole number, or the last digit of a decimal.
NUMBER_WORDS = set(DIGITS) | set(AFTER_MUOI) | {"mười", "mươi", "trăm", "linh", "lẻ"} | set(SCALES)
# What a number counts or measures. A one-word "sentence" made of one of these after a number belongs to it ("150.
# Trang" is 150 pages); after "giờ" they mean the number is not the minutes ("3 giờ. Hai mươi người").
MEASURES = {"trang", "người", "đồng", "phút", "giây", "giờ", "lần", "ngày", "tuần", "tháng", "năm"}
SENTENCE_MARKS = ".?!…"
# A word after which a number goes on ("hai mươi", "ba trăm", "năm nghìn"): a number cut before it is not whole.
GOES_ON = set(DIGITS) | {"mười", "mươi", "trăm"} | set(SCALES)
# "ba chấm năm nay" is three marks this year, not 3.5.
YEAR_WORDS = {"nay", "ngoái", "sau", "trước", "học"}
# Read one by one ("hai không hai sáu"): from this many digit words on, it is a code or a year.
MIN_DIGIT_RUN = 4


@dataclass(frozen=True)
class Word:
    text: str  # as written, case and mark included
    key: str  # lower case, mark off: what is matched
    mark: str  # the mark the tagger appended, or ""


def split_word(token: str) -> Word:
    core = token.rstrip(MARKS)
    if not core:  # a token that is only a mark
        return Word(token, "", "")
    return Word(token, core.lower(), token[len(core):])


@dataclass(frozen=True)
class Number:
    """A parsed number: its value, where it ends, and the scale words met (index, scale) for writing it."""

    value: int
    end: int
    scales: tuple[tuple[int, int], ...] = ()
    single: bool = False  # one bare word ("hai", "mười"): converted only by what follows


class _Parser:
    def __init__(self, words: Sequence[Word]) -> None:
        self.keys = [w.key for w in words]
        # A number cannot run past a word that carries a mark.
        self.open = [not w.mark for w in words]

    def key(self, i: int) -> str | None:
        return self.keys[i] if 0 <= i < len(self.keys) else None

    def next_key(self, i: int) -> str | None:
        """The word after i, if a number may go on into it."""
        return self.key(i + 1) if 0 <= i < len(self.keys) and self.open[i] else None

    # --- below a hundred / a thousand ----------------------------------------------------------

    def tens(self, i: int, inside: bool) -> tuple[int, int] | None:
        """10–99 from i: "mười [unit]", "<2–9> mươi [unit]", or the shorthand "hai tư"; (value, end)."""
        word, after = self.key(i), self.next_key(i)
        if word == "mười":
            unit = AFTER_MUOI_10.get(self.next_key(i) or "")
            return (10 + unit, i + 2) if unit is not None else (10, i + 1)
        digit = DIGITS.get(word or "")
        if digit is None or digit < 2:
            return None
        if after == "mươi":
            unit = AFTER_MUOI.get(self.next_key(i + 1) or "")
            return (digit * 10 + unit, i + 3) if unit is not None else (digit * 10, i + 2)
        if after in TENS_ONLY:
            return digit * 10 + TENS_ONLY[after], i + 2
        # "hai sáu" for 26 only where a number is already under way: alone, two digits are a range.
        if inside and after in DIGITS and after not in ("không", "năm") and word != "năm":
            if self.next_key(i + 1) not in SCALES and self.next_key(i + 1) not in ("trăm", "mươi"):
                return digit * 10 + DIGITS[after], i + 2
        return None

    def below_thousand(self, i: int, inside: bool) -> tuple[int, int, bool] | None:
        """0–999 from i; (value, end, single). `inside`: after a scale word, where "không trăm" is allowed."""
        word, after = self.key(i), self.next_key(i)
        digit = DIGITS.get(word or "")
        if digit is not None and after == "trăm" and (digit or inside):
            value, at = digit * 100, i + 2
            if self.open[i + 1]:
                tail = self.key(at)
                if tail in LINH and self.next_key(at) in AFTER_LINH:
                    return value + AFTER_LINH[self.next_key(at)], at + 2, False
                tens = self.tens(at, inside=True)
                if tens:
                    return value + tens[0], tens[1], False
                if tail in COLLOQUIAL and self.next_key(at) not in SCALES:
                    return value + COLLOQUIAL[tail] * 10, at + 1, False
            return value, at, False
        tens = self.tens(i, inside)
        if tens:
            return tens[0], tens[1], tens[1] - i == 1
        if digit is not None and digit > 0:
            return digit, i + 1, True
        return None

    # --- whole numbers -------------------------------------------------------------------------

    def number(self, i: int, inside: bool = False) -> Number | None:
        """The longest number from i, scale words included, or None."""
        total, at, scales, last = 0, i, [], None
        while True:
            part = self.below_thousand(at, inside=inside or last is not None)
            if part is None:
                break
            value, end, single = part
            scale_word = self.key(end) if end > at and self.open[end - 1] else None
            scale = SCALES.get(scale_word or "")
            if scale and (last is None or scale < last):
                index = end
                # "một nghìn tỷ": a thousand billion.
                if scale == 1000 and self.next_key(end) in ("triệu", "tỷ", "tỉ") and (last is None or scale * SCALES[self.next_key(end)] < last):
                    scale *= SCALES[self.next_key(end)]
                    index = end + 1
                total += value * scale
                scales.append((index, scale))
                at, last = index + 1, scale
                if not self.open[index]:
                    break
                continue
            if last is not None and single and self.key(at) != "mười":
                # After a scale word a bare digit is a tenth of it ("hai nghìn hai"); "một" and "năm" end the number.
                colloquial = COLLOQUIAL.get(self.key(at) or "")
                if colloquial is not None:
                    total += colloquial * last // 10
                    at = end
                break
            total += value
            at = end
            if last is None:
                return Number(total, at, (), single)
            break
        if at == i:
            return None
        return Number(total, at, tuple(scales), False)

    def digit_run(self, i: int, minimum: int, allow_five_first: bool = False) -> tuple[str, int] | None:
        """Digits read one by one ("hai không hai sáu"), at least `minimum` of them; (digits, end)."""
        if self.key(i) == "năm" and not allow_five_first:
            return None
        digits, at = "", i
        while self.key(at) in DIGITS:
            digits += str(DIGITS[self.key(at)])
            at += 1
            if not self.open[at - 1]:
                break
        return (digits, at) if len(digits) >= minimum else None

    def fraction(self, i: int) -> tuple[str, int] | None:
        """What follows "phẩy": "mười bốn" (14), or digits one by one ("không năm" → 05)."""
        tens = self.tens(i, inside=False)
        if tens and (tens[1] - i > 1 or self.key(i) == "mười"):
            return str(tens[0]), tens[1]
        word = self.key(i)
        if word in ("lăm", "tư"):
            return str(TENS_ONLY[word]), i + 1
        return self.digit_run(i, 1, allow_five_first=True)


def group(value: int) -> str:
    """15000 → "15.000"; four digits stay as they are, a year among them."""
    return f"{value:,}".replace(",", ".") if value >= 10000 else str(value)


Span = tuple[str, int, int]  # an output word and the input words [start, end) it replaces


def _write(number: Number, start: int, words: Sequence[Word]) -> list[Span]:
    """A whole number as output words: one word, or "200 triệu" / "2 tỷ 300 triệu" for round millions."""
    big = [(index, scale) for index, scale in number.scales if scale >= 10**6]
    if number.value >= 10**6 and number.value % 10**6 == 0 and len(big) == len(number.scales):
        out: list[Span] = []
        at, left = start, number.value
        for index, scale in big:
            base = 10**9 if scale >= 10**9 else 10**6
            count = left // base if base == 10**9 else (left % 10**9) // base
            out.append((group(count), at, index))
            out.append((words[index].key, index, index + 1))
            at = index + 1
            left = left % 10**9 if base == 10**9 else left
        return out
    return [(group(number.value), start, number.end)]


def rewrite(text: str) -> list[Span]:
    """The words of `text` with its numbers written in digits, each with the input words it replaces.

    A mark the tagger put inside a number is taken off only when the number then converts across it; otherwise the
    line is read as the tagger wrote it.
    """
    words = [split_word(token) for token in text.split()]
    for mend in _mends(words):
        trial = _apply(words, mend)
        spans = _rewrite(trial)
        after = not mend.both_sides or _is_converted(spans, trial, mend.at + 1)
        if _is_converted(spans, trial, mend.at - 1) and after:
            words = trial
    return _rewrite(words)


def _rewrite(words: Sequence[Word]) -> list[Span]:
    parser = _Parser(words)
    out: list[Span] = []
    i = 0
    while i < len(words):
        spans = _number_at(parser, words, i, out)
        if spans:
            for written, start, end in spans:
                out.append((written + words[end - 1].mark, start, end))
            i = spans[-1][2]
            continue
        out.append((words[i].text, i, i + 1))
        i += 1
    return out


def _is_converted(out: list[Span], words: Sequence[Word], index: int) -> bool:
    """Whether input word `index` was written as a number."""
    return any(start <= index < end and written[:1].isdigit() for written, start, end in out)


def _number_at(parser: _Parser, words: Sequence[Word], i: int, out: list[Span]) -> list[Span] | None:
    key = parser.key
    before = key(i - 1) if i > 0 and parser.open[i - 1] else None
    if key(i) not in DIGITS and key(i) not in ("mười", "tư"):
        return None

    # The day of a date: "ngày hai hai tháng mười một", "mùng hai tháng chín".
    if before in ("ngày", "mùng", "mồng"):
        day = parser.number(i, inside=True)
        if day and parser.open[day.end - 1] and key(day.end) == "tháng" and 1 <= day.value <= 31:
            return [(str(day.value), i, day.end)]
    # The month: "ngày 22 tháng mười một", "tháng hai năm hai nghìn".
    if before == "tháng":
        month = parser.number(i)
        if key(i) == "tư":
            month = Number(4, i + 1, (), True)
        if month and 1 <= month.value <= 12:
            dated = i >= 2 and _is_converted(out, words, i - 2)
            year_follows = parser.open[month.end - 1] and key(month.end) == "năm" and parser.open[month.end] \
                and (parser.number(month.end + 1) or parser.digit_run(month.end + 1, MIN_DIGIT_RUN))
            if not month.single or dated or year_follows:
                return [(str(month.value), i, month.end)]

    # A version or a section, "chấm" between two single digits: "opus bốn chấm năm" → 4.5.
    if key(i) in DIGITS and before not in NUMBER_WORDS and parser.next_key(i) == "chấm" \
            and parser.next_key(i + 1) in DIGITS:
        after = parser.next_key(i + 2)
        # "bốn chấm không?" ends on the question word "không", not on a 0.
        question = key(i + 2) == "không" and after is None
        if after not in NUMBER_WORDS and after != "chấm" and not question \
                and not (key(i + 2) == "năm" and after in YEAR_WORDS):
            return [(f"{DIGITS[key(i)]}.{DIGITS[key(i + 2)]}", i, i + 3)]

    # Minutes after an hour in digits: "9 giờ mười" → 9 giờ 10, "13 giờ không năm" → 13 giờ 05.
    if before == "giờ" and _hour_before(parser, out, i):
        after = parser.next_key(i)
        if key(i) == "mười" and after not in AFTER_MUOI_10 and after not in MEASURES - {"phút"}:
            return [("10", i, i + 1)]
        if key(i) == "không" and after in DIGITS and after != "không" and parser.next_key(i + 1) not in GOES_ON:
            return [(f"0{DIGITS[after]}", i, i + 2)]

    # A range of two-digit numbers: "từ hai hai đến hai bảy" → từ 22 đến 27, where single digits make no sense.
    pair = _pair(parser, i)
    if pair is not None:
        if before == "từ" and parser.open[pair[1] - 1] and key(pair[1]) in RANGE_TO and parser.open[pair[1]]:
            if _pair(parser, pair[1] + 1) is not None or _several_words(parser, pair[1] + 1):
                return [(str(pair[0]), i, pair[1])]
        if before in RANGE_TO and _range_from(out, words, i):
            return [(str(pair[0]), i, pair[1])]

    number = parser.number(i)
    run = parser.digit_run(i, MIN_DIGIT_RUN)
    if run and (number is None or run[1] > number.end):
        return [(run[0], i, run[1])]
    # Two digits in a row are a range: "sáu bảy triệu" is 6–7 million, "hai ba trăm" 200–300. After "năm" (the
    # year) and "không" (not) a number may well start.
    if before in DIGITS and before not in ("không", "năm") and key(i) in DIGITS:
        return None
    integer: str | None = None
    if number is None:
        if key(i) == "không":  # "không phẩy năm"
            number = Number(0, i + 1, (), True)
        else:
            return None
    end = number.end

    # A decimal: "năm phẩy năm", "không phẩy sáu", "ba phẩy mười bốn".
    if parser.open[end - 1] and key(end) == "phẩy" and parser.open[end]:
        fraction = parser.fraction(end + 1)
        if fraction:
            integer = f"{group(number.value)},{fraction[0]}"
            end = fraction[1]
    if integer is None and number.value == 0:
        return None

    # A percentage: "mười lăm phần trăm", "năm phẩy năm phần trăm".
    if parser.open[end - 1] and key(end) == "phần" and parser.open[end] and key(end + 1) == "trăm":
        return [((integer or group(number.value)) + "%", i, end + 2)]
    if integer is not None:
        return [(integer, i, end)]

    if number.single:
        if before == "thứ":
            return None
        unit = key(end) if parser.open[end - 1] else None
        if unit in TIME_UNITS and key(i) != "một":
            return [(str(number.value), i, end)]
        return None
    if before == "thứ" and end - i == 1:
        return None
    return _write(number, i, words)


RANGE_TO = {"đến", "tới"}


def _pair(parser: _Parser, i: int) -> tuple[int, int] | None:
    """Two digit words read as one number ("hai bảy" → 27), with no number word after them; (value, end) or None."""
    tens = parser.tens(i, inside=True)
    if tens is None or tens[1] - i != 2 or parser.key(i + 1) == "mươi":
        return None
    after = parser.next_key(tens[1] - 1)
    return None if after in NUMBER_WORDS else tens


def _several_words(parser: _Parser, i: int) -> bool:
    number = parser.number(i)
    return number is not None and not number.single


def _range_from(out: list[Span], words: Sequence[Word], i: int) -> bool:
    """Whether "đến" at i - 1 closes "từ <number in digits>", so what follows is the other end of the range."""
    if len(out) < 3 or out[-1][1] != i - 1 or words[i - 2].mark:
        return False
    written, start, end = out[-2]
    return end == i - 1 and written[:1].isdigit() and start > 0 and words[start - 1].key == "từ" \
        and not words[start - 1].mark


def _hour_before(parser: _Parser, out: list[Span], i: int) -> bool:
    """Whether "giờ" at i - 1 follows an hour written in digits (0–24)."""
    if len(out) < 2 or out[-1][1] != i - 1 or not parser.open[i - 2]:
        return False
    written = out[-2][0]
    return written.isdigit() and int(written) <= 24


@dataclass(frozen=True)
class _Mend:
    """A mark the tagger put inside a number: taken off word `at`, and moved to word `to` (or dropped when None)."""

    at: int
    to: int | None
    lower: int | None = None  # a unit the tagger capitalised as if it started a sentence
    both_sides: bool = True  # the words on both sides of the mark must end up in digits


def _mends(words: Sequence[Word]) -> list[_Mend]:
    """Marks the tagger put between the words of one number, by the patterns that can only be one number."""
    parser = _Parser(words)
    key = parser.key
    found = []
    for i, word in enumerate(words[:-1]):
        if not word.mark or i == 0 or not parser.open[i - 1]:
            continue
        end = None
        # "không phẩy. bảy hai": a decimal comma, then its digits.
        if word.key == "phẩy" and key(i - 1) in NUMBER_WORDS:
            fraction = parser.fraction(i + 1)
            # A lone "không" after the mark is "no" ("ba phẩy. Không phải"), not a digit.
            if fraction and not (key(i + 1) == "không" and fraction[1] == i + 2):
                end = fraction[1]
        # "mười bốn giờ. hai mươi lăm": the minutes of an hour.
        elif word.key == "giờ" and word.mark in SENTENCE_MARKS and key(i - 1) in NUMBER_WORDS:
            minutes = parser.number(i + 1)
            if key(i + 1) == "không" and key(i + 2) in DIGITS and parser.open[i + 1] \
                    and parser.next_key(i + 2) not in GOES_ON:
                end = i + 3
            elif minutes and (not minutes.single or key(i + 1) == "mười") and 1 <= minutes.value <= 59:
                after = key(minutes.end) if parser.open[minutes.end - 1] else None
                if after not in MEASURES - {"phút"} and after not in GOES_ON:
                    end = minutes.end + 1 if after == "phút" else minutes.end
        # "một trăm năm mươi. Trang": a unit alone as the last word, or as a sentence of its own.
        elif word.mark in SENTENCE_MARKS and key(i - 1) in NUMBER_WORDS and key(i) in NUMBER_WORDS \
                and key(i + 1) in MEASURES and (i + 2 == len(words) or words[i + 1].mark):
            found.append(_Mend(i, None if words[i + 1].mark else i + 1, lower=i + 1, both_sides=False))
            continue
        if end is None:
            continue
        # The tagger ended a sentence here: the mark goes after the number when the next word starts one.
        moved = end - 1 if not words[end - 1].mark and (end == len(words) or words[end].text[:1].isupper()) else None
        found.append(_Mend(i, moved))
    return found


def _apply(words: Sequence[Word], mend: _Mend) -> list[Word]:
    out = list(words)
    mark = out[mend.at].mark
    out[mend.at] = Word(out[mend.at].text[:-len(mark)], out[mend.at].key, "")
    if mend.lower is not None:
        out[mend.lower] = Word(out[mend.lower].text.lower(), out[mend.lower].key, out[mend.lower].mark)
    if mend.to is not None:
        target = out[mend.to]
        out[mend.to] = Word(target.text + mark, target.key, mark)
    return out


def carry(spans: Sequence[Span], values: Sequence | None, words: int,
          merge: Callable[[Sequence], object]) -> list | None:
    """Per-word values (times, scores) for the rewritten words: one per span, merged with `merge`.

    None stays None, and so does a list that did not match the input words one for one.
    """
    if values is None or len(values) != words:
        return None
    return [values[start] if end - start == 1 else merge(values[start:end]) for _, start, end in spans]


def first(values: Sequence) -> object:
    return values[0]


def normalise_line(text: str, times: list | None = None, scores: list | None = None
                   ) -> tuple[str, list | None, list | None]:
    """A transcript line with its numbers in digits, and its word times (first word's) and scores (least sure)."""
    count = len(text.split())
    spans = rewrite(text)
    return (" ".join(written for written, _, _ in spans), carry(spans, times, count, first),
            carry(spans, scores, count, min))
