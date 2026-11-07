"""The benchmark meetings for the AI draft (SCRUM-36), from complete to missing one part or another.

Each case is a meeting as the Secretary would leave it: an agenda (with the times the Chair started and closed each
item, or not), the Chair's recorded conclusions, closed votes, document names, and what was said, through each
person's mic (`mic`) or the room mic with voices nobody named (`room`). `expect` lists what a correct draft must say
(numbers already in digits, as the rules ask); `forbid` lists what it must not (an invented fact, a guessed name, a
number left in words). The scorer looks for each phrase in the draft, case- and accent-sensitively; `a|b` in
`expect` accepts either spelling.

Times are minutes from the start of the meeting. Speakers are the seeded accounts: CHAIR (Nguyễn Hoàng Thanh Hương),
SEC (Trần Việt Hải), MEM (Lê An).
"""

CHAIR, SEC, MEM = "chutoa", "thuky", "thanhvien"

FULL_TALK = [
    (0.5, CHAIR, "Kính thưa các đồng chí, hôm nay chúng ta họp để rà soát tình hình thực hiện nhiệm vụ quý 3 và thống nhất kế hoạch năm 2027."),
    (1.5, MEM, "Dạ, trong quý 3 đơn vị đã hoàn thành mười tám trên hai mươi nhiệm vụ được giao, đạt chín mươi phần trăm kế hoạch."),
    (3, MEM, "Hai nhiệm vụ chưa hoàn thành là triển khai phần mềm quản lý văn bản tại ba phòng còn lại và số hoá hồ sơ lưu trữ năm 2020."),
    (4, MEM, "Nguyên nhân chủ yếu là nhà cung cấp giao phần mềm chậm ba tuần và thiếu máy quét tài liệu."),
    (5, MEM, "Giải ngân vốn đạt một phẩy hai tỷ đồng trên tổng số một phẩy năm tỷ đồng."),
    (7, SEC, "Tôi xin bổ sung, việc số hoá hồ sơ cần thêm khoảng hai máy quét, mỗi máy khoảng bốn mươi lăm triệu đồng."),
    (8, CHAIR, "Giao phòng hành chính làm tờ trình mua hai máy quét, trình trước ngày mười lăm tháng mười một."),
    (9, MEM, "Về phần mềm, nhà cung cấp cam kết hoàn thành triển khai cho ba phòng còn lại trong tháng mười một."),
    (11, CHAIR, "Tôi đánh giá quý 3 đơn vị đã hoàn thành tốt nhiệm vụ, tuy nhiên cần khắc phục ngay hai nhiệm vụ còn tồn đọng."),
    (20.5, CHAIR, "Chúng ta chuyển sang nội dung thứ hai, kế hoạch năm 2027. Mời đồng chí thư ký trình bày dự thảo."),
    (21.5, SEC, "Dự thảo kế hoạch năm 2027 gồm ba trọng tâm: hoàn thành chuyển đổi số toàn bộ quy trình nội bộ, đào tạo lại toàn bộ cán bộ về kỹ năng số, và cắt giảm ba mươi phần trăm thời gian xử lý hồ sơ."),
    (23, SEC, "Tổng kinh phí dự kiến là bốn phẩy tám tỷ đồng, trong đó đào tạo chiếm sáu trăm triệu đồng."),
    (25, MEM, "Tôi đề nghị cân nhắc chỉ tiêu cắt giảm ba mươi phần trăm, vì năm nay mới đạt khoảng mười hai phần trăm, chỉ tiêu này có thể quá cao."),
    (26.5, SEC, "Tôi đồng ý có thể điều chỉnh xuống hai mươi phần trăm cho an toàn."),
    (28, CHAIR, "Tôi đồng ý điều chỉnh chỉ tiêu xuống hai mươi phần trăm. Các nội dung khác giữ nguyên như dự thảo."),
    (36, CHAIR, "Kết quả biểu quyết đã thông qua. Cuộc họp kết thúc tại đây, cảm ơn các đồng chí."),
]

AGENDA = [
    {"title": "Báo cáo tình hình thực hiện nhiệm vụ quý 3", "presenter": MEM, "start": 1, "end": 20,
     "conclusion": "Phòng Hành chính trình mua 2 máy quét trước ngày 15/11; hoàn thành triển khai phần mềm cho 3 phòng còn lại trong tháng 11."},
    {"title": "Kế hoạch năm 2027", "presenter": SEC, "start": 20.2, "end": 37, "conclusion": None},
]

VOTE = {"title": "Thông qua kế hoạch năm 2027", "anonymous": False, "conclusion": "Passed", "note": "",
        "options": [("Đồng ý", 3), ("Không đồng ý", 0), ("Không ý kiến", 0)]}

FULL_EXPECT = ["18/20|18 trên 20", "90%", "2 máy quét", "45 triệu", "4,8 tỷ", "600 triệu", "20%", "Lê An",
               "Đồng ý 3 phiếu", "Kết luận: Thông qua"]
NUMBERS_IN_WORDS = ["mười tám", "chín mươi phần trăm", "bốn mươi lăm triệu", "bốn phẩy tám"]

CASES = [
    {
        "key": "1-day-du",
        "title": "Đầy đủ: chương trình có giờ, kết luận, biểu quyết, tài liệu, lời thoại",
        "agenda": AGENDA, "votes": [VOTE], "documents": ["Báo cáo quý 3.pdf", "Dự thảo kế hoạch 2027.docx"],
        "mic": FULL_TALK, "room": [],
        "expect": FULL_EXPECT, "forbid": NUMBERS_IN_WORDS + ["Không có nội dung"],
    },
    {
        "key": "2-khong-bieu-quyet",
        "title": "Không có biểu quyết",
        "agenda": AGENDA, "votes": [], "documents": [], "mic": FULL_TALK, "room": [],
        "expect": ["18/20|18 trên 20", "2 máy quét", "20%"], "forbid": NUMBERS_IN_WORDS + ["Đồng ý 3 phiếu"],
    },
    {
        "key": "3-khong-chuong-trinh",
        "title": "Không có chương trình: mục II phải chia theo vấn đề",
        "agenda": [], "votes": [VOTE], "documents": [], "mic": FULL_TALK, "room": [],
        "expect": ["18/20|18 trên 20", "2 máy quét", "4,8 tỷ", "20%", "Kết luận: Thông qua"], "forbid": NUMBERS_IN_WORDS,
    },
    {
        "key": "4-chuong-trinh-chua-bam-gio",
        "title": "Chương trình có nhưng chủ trì không bấm bắt đầu/kết thúc từng mục",
        "agenda": [{**a, "start": None, "end": None} for a in AGENDA], "votes": [VOTE], "documents": [],
        "mic": FULL_TALK, "room": [],
        "expect": ["18/20|18 trên 20", "2 máy quét", "20%", "Kết luận: Thông qua"], "forbid": NUMBERS_IN_WORDS,
    },
    {
        "key": "5-khong-loi-thoai",
        "title": "Không có lời thoại: chỉ chương trình, kết luận và biểu quyết",
        "agenda": AGENDA, "votes": [VOTE], "documents": ["Báo cáo quý 3.pdf"], "mic": [], "room": [],
        "expect": ["Chưa ghi nhận ý kiến thảo luận", "2 máy quét", "Kết luận: Thông qua"],
        # Nothing was said, so no figure from the talk may appear.
        "forbid": ["18/20|18 trên 20", "90%", "4,8 tỷ", "45 triệu"],
    },
    {
        "key": "6-mic-phong-chua-gan-ten",
        "title": "Mic phòng, giọng chưa gán tên (Người nói 1, 2): không được đoán tên",
        "agenda": AGENDA, "votes": [VOTE], "documents": [], "mic": [],
        "room": [(m, {CHAIR: "SPEAKER_00", MEM: "SPEAKER_01", SEC: "SPEAKER_02"}[who], text) for m, who, text in FULL_TALK],
        "expect": ["Người nói", "18/20|18 trên 20", "2 máy quét", "20%"],
        # The voices were never named: a name in section II is a guess. The presenter line is the agenda's, so allowed.
        "forbid": NUMBERS_IN_WORDS + ["Nguyễn Hoàng Thanh Hương phát biểu", "Trần Việt Hải đề"],
    },
    {
        "key": "7-chi-ten-va-vai-cau",
        "title": "Rất ít dữ liệu: chỉ tên cuộc họp và 2 câu",
        "agenda": [], "votes": [], "documents": [],
        "mic": [(1, CHAIR, "Hôm nay chúng ta bàn nhanh lịch trực Tết Nguyên đán."),
                (2, MEM, "Phòng tổ chức đề nghị chốt danh sách trực trước ngày hai mươi tháng một.")],
        "room": [],
        "expect": ["Tết Nguyên đán", "20/01|20/1|20 tháng 1"], "forbid": ["hai mươi tháng một", "Đồng ý"],
    },
    {
        "key": "8-hop-dai",
        "title": "Họp dài: một mục có lời thoại vượt một lần gọi AI (phải chia nhỏ rồi gộp)",
        "agenda": [{"title": "Rà soát dự toán ngân sách năm 2027", "presenter": SEC, "start": 0, "end": 90, "conclusion": None}],
        "votes": [], "documents": [],
        "mic": [(i * 0.9, [CHAIR, SEC, MEM][i % 3], f"Về khoản mục số {i + 1}, đơn vị đề xuất kinh phí {100 + i * 5} triệu đồng cho hạng mục mua sắm thiết bị và bảo trì hệ thống, cần rà soát lại định mức theo quy định hiện hành của Bộ Tài chính.") for i in range(90)],
        "room": [],
        "expect": ["Bộ Tài chính", "triệu đồng"], "forbid": ["Không có nội dung"],
    },
    {
        "key": "9-noi-chem-tieng-anh",
        "title": "Nói chêm tiếng Anh: máy gỡ băng chép từ tiếng Anh theo âm",
        "agenda": [], "votes": [], "documents": [], "mic": [],
        # Written the way the Vietnamese recogniser spells English by ear (as on Test04.mp3), with words other than the
        # ones the API rule gives as examples, so the bench measures the rule, not the examples.
        "room": [
            (0.5, "SPEAKER_00", "Hôm nay mình họp để chốt kế hoạch rì lít app phiên bản hai chấm không trên sàn Mi Ti Hôm."),
            (1.5, "SPEAKER_01", "Bên em đã chạy xong ba trăm tét kây, còn mười hai lỗi chưa phích bấc, dự kiến xong trước ngày hai mươi tháng mười một."),
            (3, "SPEAKER_02", "Khách hàng đã gửi phít bách về màn hình đa sờ bọt, muốn thêm biểu đồ doanh thu theo tuần."),
            (4, "SPEAKER_00", "Thế thì em làm bản đem mô cho khách xem vào thứ sáu, rồi mình rì viu lại cùng nhau."),
            (5, "SPEAKER_01", "Môi trường u ây ti đang ổn, nhưng sẹc vơ hay bị chậm khi gọi a pi thanh toán."),
            (6, "SPEAKER_02", "Em đề xuất nâng sẹc vơ lên tám gigabyte RAM, chi phí khoảng hai triệu đồng một tháng."),
            (7, "SPEAKER_00", "Đồng ý. Ríp pọt kết quả kiểm thử gửi chị trước khi rì lít nhé."),
            (8, "SPEAKER_01", "Dạ, em sẽ gửi ríp pọt vào thứ năm."),
        ],
        "expect": ["300", "12", "20/11|20 tháng 11", "2 triệu", "Mi Ti Hôm"],
        "forbid": ["ba trăm", "mười hai lỗi", "hai triệu"],
        # (as recorded, what was said): the draft should carry the second (or a Vietnamese word for it), not the first.
        "words": [
            ("rì lít", "release|phát hành"),
            ("tét kây", "test case|kịch bản kiểm thử|ca kiểm thử"),
            ("phích bấc", "fix bug|sửa lỗi"),
            ("phít bách", "feedback|góp ý|phản hồi"),
            ("đa sờ bọt", "dashboard|bảng điều khiển"),
            ("đem mô", "demo|bản trình diễn|bản chạy thử"),
            ("rì viu", "review|rà soát|xem lại"),
            ("u ây ti", "UAT|nghiệm thu"),
            ("sẹc vơ", "server|máy chủ"),
            ("a pi", "API"),
            ("ríp pọt", "report|báo cáo"),
        ],
    },
]
