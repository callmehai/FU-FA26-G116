import struct
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import hotwords  # noqa: E402


def varint(value: int) -> bytes:
    out = bytearray()
    while True:
        byte, value = value & 0x7F, value >> 7
        out.append(byte | (0x80 if value else 0))
        if not value:
            return bytes(out)


def field(number: int, wire: int, payload: bytes) -> bytes:
    head = varint(number << 3 | wire)
    return head + (varint(len(payload)) + payload if wire == 2 else payload)


def sentencepiece_model(pieces: list[tuple[str, float]]) -> bytes:
    """A SentencePiece ModelProto as protoc writes it, with a trainer spec field before the pieces are read."""
    body = b"".join(
        field(1, 2, field(1, 2, piece.encode("utf-8")) + field(2, 5, struct.pack("<f", score)) + field(3, 0, varint(1)))
        for piece, score in pieces
    )
    return body + field(2, 2, field(1, 2, b"train.txt") + field(3, 0, varint(2000)))


class SettingsTests(unittest.TestCase):
    def test_on_by_default_with_the_shipped_list(self):
        chosen = hotwords.settings({})
        self.assertEqual(chosen.path, hotwords.DEFAULT_LIST)
        self.assertEqual(chosen.score, hotwords.DEFAULT_SCORE)

    def test_a_named_file_and_score(self):
        chosen = hotwords.settings({"SPEECH_HOTWORDS_FILE": "/srv/words.txt", "SPEECH_HOTWORDS_SCORE": "2.5"})
        self.assertEqual(chosen, hotwords.Settings(Path("/srv/words.txt"), 2.5))

    def test_off_by_file_or_by_score(self):
        for environ in ({"SPEECH_HOTWORDS_FILE": "0"}, {"SPEECH_HOTWORDS_FILE": "off"},
                        {"SPEECH_HOTWORDS_FILE": "False"}, {"SPEECH_HOTWORDS_SCORE": "0"}):
            self.assertIsNone(hotwords.settings(environ).path, environ)


class PhraseTests(unittest.TestCase):
    def test_upper_cased_without_comments_blank_lines_or_repeats(self):
        text = "# heading\nbiên bản\n\n  chủ   tọa  # who chairs\nBIÊN BẢN\nreport\n"
        self.assertEqual(hotwords.phrases(text), ["BIÊN BẢN", "CHỦ TỌA", "REPORT"])

    def test_a_phrase_keeps_its_own_score(self):
        self.assertEqual(hotwords.phrases("voice id :2.0\nbr :0.5\n"), ["VOICE ID :2.0", "BR :0.5"])

    def test_a_colon_word_that_is_not_a_score_is_part_of_the_phrase(self):
        self.assertEqual(hotwords.phrases("tỷ lệ :a\n"), ["TỶ LỆ :A"])

    def test_the_shipped_list_is_usable(self):
        lines = hotwords.phrases(hotwords.DEFAULT_LIST.read_text(encoding="utf-8"))
        self.assertGreater(len(lines), 10)
        self.assertEqual(len(lines), len(set(lines)))
        for line in lines:
            # The model only writes upper-case letters; a digit or a mark in a phrase would never match its output.
            self.assertRegex(line, r"^[^\W\d_]+( [^\W\d_]+)*( :\d+(\.\d+)?)?$")
            self.assertEqual(line, line.upper())


class VocabTests(unittest.TestCase):
    def test_pieces_and_scores_in_id_order(self):
        model = sentencepiece_model([("<blk>", 0.0), ("▁MỘT", -4.25), ("ER", -7.5)])
        vocab = hotwords.bpe_vocab(model)
        self.assertEqual([piece for piece, _ in vocab], ["<blk>", "▁MỘT", "ER"])
        self.assertAlmostEqual(vocab[1][1], -4.25, places=5)

    def test_rejects_a_file_that_is_not_a_model(self):
        with self.assertRaises(ValueError):
            hotwords.bpe_vocab(field(2, 2, b"no pieces here"))
        with self.assertRaises(ValueError):
            hotwords.bpe_vocab(b"\x0a\xff")


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp())
        (self.root / "gipformer").mkdir()
        (self.root / hotwords.BPE_MODEL).write_bytes(sentencepiece_model([("<blk>", 0.0), ("▁BIÊN", -5.0)]))
        self.words = self.root / "words.txt"
        self.words.write_text("biên bản\nreport :2\n", encoding="utf-8")

    def test_writes_what_sherpa_onnx_reads(self):
        prepared = hotwords.prepare(self.root, hotwords.Settings(self.words, 1.5), self.root / "out")
        self.assertEqual(Path(prepared.hotwords_file).read_text(encoding="utf-8"), "BIÊN BẢN\nREPORT :2\n")
        self.assertEqual(Path(prepared.bpe_vocab).read_text(encoding="utf-8"), "<blk>\t0\n▁BIÊN\t-5\n")
        self.assertEqual(prepared.count, 2)
        self.assertEqual(hotwords.recognizer_options(prepared), {
            "hotwords_file": prepared.hotwords_file, "hotwords_score": 1.5,
            "modeling_unit": "bpe", "bpe_vocab": prepared.bpe_vocab,
        })

    def test_the_same_list_reuses_one_folder(self):
        first = hotwords.prepare(self.root, hotwords.Settings(self.words, 1.5))
        again = hotwords.prepare(self.root, hotwords.Settings(self.words, 2.0))
        self.assertEqual(first.hotwords_file, again.hotwords_file)
        self.words.write_text("dự thảo\n", encoding="utf-8")
        other = hotwords.prepare(self.root, hotwords.Settings(self.words, 1.5))
        self.assertNotEqual(first.hotwords_file, other.hotwords_file)

    def test_off_or_empty_gives_no_options(self):
        self.assertIsNone(hotwords.prepare(self.root, hotwords.Settings(None, 1.5)))
        self.words.write_text("# nothing yet\n", encoding="utf-8")
        self.assertIsNone(hotwords.prepare(self.root, hotwords.Settings(self.words, 1.5)))
        self.assertEqual(hotwords.recognizer_options(None), {})


if __name__ == "__main__":
    unittest.main()
