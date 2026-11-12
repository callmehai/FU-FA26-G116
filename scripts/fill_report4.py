"""Fill Report 4 (Software Design Specification) section II from repo diagrams.

Source of truth: docs/08-reports/README.md -- the DOCX in
docs/08-reports/source/ is edited, never the Markdown snapshot.

What the script does:
  - md5-syncs images whose bytes differ from the repo PNGs (section I:
    architecture + package diagrams; section II: class + sequence diagrams);
  - rebuilds section I.3: the target table-form ERD straight under H2 plus
    one numbered H3 per entity spec table copied from Report 3 section 3.2
    (single source; Chen view and the old "As-built" H3 are not used);
  - inserts missing section-II subsections (FE-08, FE-09, FE-10, FE-15,
    FE-16), the FE-11 class diagram (replacing its stale "no class diagram"
    note), and the missing FE-11 sequence headings (UC-52a..UC-57);
  - renumbers section II in FE order (old 8/9/10 -> 11/12/13) and updates the
    TOC label texts + inserts TOC lines for the new headings;
  - writes Figure captions under every diagram in sections I and II
    ("Figure 11.2.8: Sequence Diagram of UC-76a - ...",
    "Figure 11.1: Class Diagram of FE-11 Voting Management",
    "Figure 2.2: Back-End Package Diagram"), regenerated from the heading
    numbers on each run;
  - preserves the manual UC-33b split (two cropped halves): never touched.

Deliberately NOT done by the script (Word finishing pass, as before):
  - TOC page numbers (Update Field in Word), Change Log row, landscape
    tuning for wide diagrams, splitting very tall images.

Usage:
    python scripts/fill_report4.py                      # dry run: print plan
    python scripts/fill_report4.py --output /tmp/R4.docx # write a copy
    python scripts/fill_report4.py --write               # modify source in place (.bak kept)

After --write: open the file in Word, Update Field (entire TOC), add the
Change Log row, then run check_docx.py.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import struct
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPO = ROOT
SEQ_DIR = REPO / "docs" / "06-sequencediagram"
CLS_DIR = REPO / "docs" / "07-classdiagram"
DEFAULT_SOURCE = REPO / "docs" / "08-reports" / "source" / "Report-4_Software Design Specification.docx"

def _load_check_docx():
    """Load the house validator without touching sys.path (keeps editors clean)."""
    import importlib.util
    path = Path(__file__).resolve().parent / "check_docx.py"
    spec = importlib.util.spec_from_file_location("check_docx", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


check_docx = _load_check_docx()  # house validator, must stay green

EMU_PER_INCH = 914400
PORTRAIT_CONTENT_EMU = 9026 * 635  # 11906 - 2x1440 twips, A4 portrait
TALL_WARN_EMU = int(9.5 * EMU_PER_INCH)

P_RE = re.compile(r"<w:p\b[^>]*>.*?</w:p>", re.S)
T_RE = re.compile(r"<w:t(?:\s[^>]*)?>(.*?)</w:t>", re.S)
STYLE_RE = re.compile(r'<w:pStyle w:val="([^"]+)"')
UC_ID_RE = re.compile(r"UC-\d+[a-z]?")
HEADING_NUM_RE = re.compile(r"^(\d+(?:\.\d+){1,3})\s+(.*)$")


# ---------------------------------------------------------------- manifest

# (png file name, txt file name for the title). txt None -> title from png stem.
SEQ = SEQ_DIR
CLS = CLS_DIR


def UC(folder: str, png: str, txt: str | None = None):  # noqa: N802 - manifest DSL
    if txt is None:  # same-stem .txt holds the title (verified for every entry)
        cand = SEQ / folder / (Path(png).stem + ".txt")
        txt = str(cand) if cand.is_file() else None
    else:
        txt = str(SEQ / folder / txt) if not Path(txt).is_absolute() else txt
    return (str(SEQ / folder / png), txt)


FE_SECTIONS = [
    {"fe": "FE-01", "title": "Authentication",
     "class_png": str(CLS / "feature-01-auth-class-diagram.drawio.png"),
     "seq": [UC("auth", "UC01_SignIn.png"), UC("auth", "UC02_SignInWithGoogle.png"),
             UC("auth", "UC03_SignOut.png"), UC("auth", "UC04_AccessMeetingViaGuestInvitation.png")]},
    {"fe": "FE-02", "title": "User Management",
     "class_png": str(CLS / "feature-02-user-management-class-diagram.drawio.png"),
     "seq": [UC("user-management", "UC05_ViewUserAccount.png"),
             UC("user-management", "UC06a_CreateUserAccount.png"),
             UC("user-management", "UC06b_UpdateUserAccount.png"),
             UC("user-management", "UC06c_DeactivateReactivateUserAccount.png"),
             UC("user-management", "UC07_ResetUserPassword.png"),
             UC("user-management", "UC08_AssignSystemRoleToUser.png"),
             UC("user-management", "UC09_UnlockUserAccount.png")]},
    {"fe": "FE-03", "title": "Role Management",
     "class_png": str(CLS / "feature-03-role-management-class-diagram.drawio.png"),
     "seq": [UC("role-management", "UC10_ViewRolePermissionMatrix.png"),
             UC("role-management", "UC11_ConfigureRolePermissions.png"),
             UC("role-management", "UC12a_CreateRole.png"),
             UC("role-management", "UC12b_UpdateRole.png"),
             UC("role-management", "UC12c_DeleteRole.png"),
             UC("role-management", "UC13_ViewAuditLog.png")]},
    {"fe": "FE-04", "title": "Meeting Management",
     "class_png": str(CLS / "feature-04-meeting-management-class-diagram.drawio.png"),
     "seq": [UC("meeting-management", "UC14_CreateMeeting.png"),
             UC("meeting-management", "UC15_UpdateMeeting.png"),
             UC("meeting-management", "UC16_ViewMeetingDetails.png"),
             UC("meeting-management", "UC17_ViewMeetingList.png"),
             UC("meeting-management", "UC18_SubmitMeetingForApproval.png"),
             UC("meeting-management", "UC19_ReviewMeetingApproval.png"),
             UC("meeting-management", "UC20_PublishMeeting.png"),
             UC("meeting-management", "UC21_StartMeeting.png"),
             UC("meeting-management", "UC22_EndMeeting.png"),
             UC("meeting-management", "UC75_CancelMeeting.png")]},
    {"fe": "FE-05", "title": "Agenda Management",
     "class_png": str(CLS / "feature-05-agenda-management-class-diagram.drawio.png"),
     "seq": [UC("agenda-management", "UC23a_CreateAgendaItem.png"),
             UC("agenda-management", "UC23b_UpdateAgendaItem.png"),
             UC("agenda-management", "UC23c_DeleteAgendaItem.png"),
             UC("agenda-management", "UC23d_ReorderAgendaItems.png"),
             UC("agenda-management", "UC24_SetCurrentAgendaItem.png"),
             UC("agenda-management", "UC25_CompleteAgendaItem.png"),
             UC("agenda-management", "UC26_ViewMeetingAgenda.png"),
             UC("agenda-management", "UC77_RecordAgendaItemConclusion.png")]},
    {"fe": "FE-06", "title": "Meeting Participant Management",
     "class_png": str(CLS / "feature-06-meeting-participant-management-class-diagram.drawio.png"),
     "seq": [UC("meeting-participant-management", "UC27_ViewMeetingParticipants.png"),
             UC("meeting-participant-management", "UC28a_AddMeetingParticipant.png"),
             UC("meeting-participant-management", "UC28b_AssignChangeMeetingRole.png"),
             UC("meeting-participant-management", "UC28c_RemoveMeetingParticipant.png"),
             UC("meeting-participant-management", "UC29_RespondToMeetingInvitation.png"),
             UC("meeting-participant-management", "UC30_ViewParticipantResponseStatus.png"),
             UC("meeting-participant-management", "UC31_RecordAttendance.png"),
             UC("meeting-participant-management", "UC73_HandOverChair.png")]},
    {"fe": "FE-07", "title": "Meeting Document Management",
     "class_png": str(CLS / "feature-07-document-management-class-diagram.drawio.png"),
     "frozen": {"UC-33b"},  # manual split into two halves (29/10) - never touch
     "seq": [UC("document-management", "UC32a_ViewMeetingDocuments.png"),
             UC("document-management", "UC32c_PreviewMeetingDocument.png"),
             UC("document-management", "UC33a_AddDocumentsFromLibrary.png",
                "UC33a_AddDocumentsFromLibrary.txt"),
             UC("document-management", "UC33a_UploadDocumentsToMeeting.png",
                "UC33a_UploadDocumentsToMeeting.txt"),
             UC("document-management", "UC33b_AttachDocumentsToAgendaItem.png"),
             UC("document-management", "UC33c_RemoveDocumentFromMeeting.png"),
             UC("document-management", "UC33d_DetachDocumentFromAgendaItem.png"),
             UC("document-management", "UC34_ViewPersonalLibrary.png"),
             UC("document-management", "UC34b_ViewLockedAccountLibrary.png"),
             UC("document-management", "UC35_CommentOnMeetingDocument.png"),
             UC("document-management", "UC36_UploadToPersonalLibrary.png"),
             UC("document-management", "UC71a_DeleteLibraryDocument.png"),
             UC("document-management", "UC71b_RenameLibraryDocument.png"),
             UC("document-management", "UC71c_MoveLibraryDocument.png"),
             UC("document-management", "UC72a_CreateFolder.png"),
             UC("document-management", "UC72b_RenameFolder.png"),
             UC("document-management", "UC72c_MoveFolder.png"),
             UC("document-management", "UC72d_DeleteFolder.png"),
             UC("document-management", "UC74_ConfirmDocumentRead.png")]},
    {"fe": "FE-08", "title": "Online Meeting Room", "class_png": None,
     "class_note": "FE-08 has no class diagram in docs/07-classdiagram yet: "
                   "this section shows only the sequence diagrams of its use cases.",
     "seq": [UC("online-meeting-room", "UC37_JoinMeetingRoom.png"),
             UC("online-meeting-room", "UC38_LeaveMeetingRoom.png"),
             UC("online-meeting-room", "UC39a_ManageMicrophone.png", "UC39a_ManageMicrophone.txt"),
             UC("online-meeting-room", "UC39a_RecordSpeechWhileMicrophoneOn.png",
                "UC39a_RecordSpeechWhileMicrophoneOn.txt"),
             UC("online-meeting-room", "UC39b_ManageCamera.png"),
             UC("online-meeting-room", "UC39c_ManageScreenSharing.png"),
             UC("online-meeting-room", "UC39d_ChooseAudioAndVideoDevices.png"),
             UC("online-meeting-room", "UC40_ViewMeetingRoomParticipants.png"),
             UC("online-meeting-room", "UC41_ModerateMeetingRoom.png"),
             UC("online-meeting-room", "UC42_UseInRoomChat.png"),
             UC("online-meeting-room", "UC78_FloatingWindow.png"),
             UC("online-meeting-room", "UC79_RegisterToSpeak.png")]},
    {"fe": "FE-09", "title": "Meeting Recording",
     "class_png": str(CLS / "feature-09-meeting-recording-class-diagram.drawio.png"),
     "seq": [UC("meeting-recording", "UC43a_ViewMeetingRecordings.png"),
             UC("meeting-recording", "UC43b_PlayMeetingRecording.png"),
             UC("meeting-recording", "UC43c_DownloadMeetingRecording.png"),
             UC("meeting-recording", "UC44a_StartRoomRecording.png"),
             UC("meeting-recording", "UC44b_StopAndSaveRoomRecording.png"),
             UC("meeting-recording", "UC44c_UploadMeetingAudioFile.png"),
             UC("meeting-recording", "UC44d_DeleteRecordingTake.png"),
             UC("meeting-recording", "UC44e_EraseMeetingRecording.png"),
             UC("meeting-recording", "UC80_ManageMyRecordedSpeech.png")]},
    {"fe": "FE-10", "title": "Transcription & Voice ID",
     "class_png": str(CLS / "feature-10-transcription-voice-id-class-diagram.drawio.png"),
     "seq": [UC("transcription-voice-id", "UC45a_ViewRecordingTranscript.png"),
             UC("transcription-voice-id", "UC45b_ExportMeetingTranscript.png"),
             UC("transcription-voice-id", "UC45c_TranscribeMeetingRecording.png",
                "UC45c_TranscribeMeetingRecording.txt"),
             UC("transcription-voice-id", "UC45c_ProcessTranscriptionQueue.png",
                "UC45c_ProcessTranscriptionQueue.txt"),
             UC("transcription-voice-id", "UC46a_EditTranscriptText.png"),
             UC("transcription-voice-id", "UC46b_AssignSpeaker.png"),
             UC("transcription-voice-id", "UC46c_ExcludeOrRestoreSentence.png"),
             UC("transcription-voice-id", "UC46d_DeleteSentence.png"),
             UC("transcription-voice-id", "UC47_GenerateMeetingSummary.png"),
             UC("transcription-voice-id", "UC48_ViewMeetingSummary.png"),
             UC("transcription-voice-id", "UC49_EditMeetingSummary.png"),
             UC("transcription-voice-id", "UC50_ApproveMeetingSummary.png"),
             UC("transcription-voice-id", "UC81a_ViewMyVoiceId.png"),
             UC("transcription-voice-id", "UC81b_GiveVoiceIdConsent.png"),
             UC("transcription-voice-id", "UC81c_RecordVoiceSample.png"),
             UC("transcription-voice-id", "UC81d_PlayVoiceSample.png"),
             UC("transcription-voice-id", "UC81e_DeleteVoiceSample.png"),
             UC("transcription-voice-id", "UC81f_WithdrawVoiceIdConsent.png")]},
    {"fe": "FE-11", "title": "Voting Management",
     "class_png": str(CLS / "feature-11-voting-management-class-diagram.drawio.png"),
     "seq": [UC("voting-management", "UC52a_ViewMeetingVotes.png"),
             UC("voting-management", "UC52b_ViewVoteStatus.png"),
             UC("voting-management", "UC53_CreateConfigureVote.png"),
             UC("voting-management", "UC54_OpenVote.png"),
             UC("voting-management", "UC55_CastVote.png"),
             UC("voting-management", "UC56_CloseVote.png"),
             UC("voting-management", "UC57_RecordVoteConclusion.png"),
             UC("voting-management", "UC76a_AttachDocumentsToVote.png"),
             UC("voting-management", "UC76b_DetachDocumentFromVote.png")]},
    {"fe": "FE-12", "title": "Meeting Minutes & Approval", "class_png": None,
     "class_note": None,  # keep the existing note paragraph untouched
     "seq": [UC("minutes-management", "UC58_ViewMeetingMinutes.png"),
             UC("minutes-management", "UC59_GenerateMeetingMinutes.png"),
             UC("minutes-management", "UC60_EditMeetingMinutes.png"),
             UC("minutes-management", "UC61_SubmitMeetingMinutesForApproval.png"),
             UC("minutes-management", "UC64_ApproveAndIssueMeetingMinutes.png"),
             UC("minutes-management", "UC65_ReturnMeetingMinutesForRevision.png"),
             UC("minutes-management", "UC83_CorrectIssuedMeetingMinutes.png")]},
    {"fe": "FE-13", "title": "Notification",
     "class_png": str(CLS / "feature-13-notification-management-class-diagram.drawio.png"),
     "seq": [UC("notification-management", "UC66_ViewNotifications.png"),
             UC("notification-management", "UC67a_MarkNotificationAsRead.png"),
             UC("notification-management", "UC67b_MarkAllNotificationsAsRead.png"),
             UC("notification-management", "UC68_OpenNotificationTarget.png")]},
    # FE-14 (dashboard, UC-69 placeholder) has no diagram in the repo: skipped.
    {"fe": "FE-15", "title": "Reporting & Statistics", "class_png": None,
     "class_note": "FE-15 has no class diagram in docs/07-classdiagram yet: "
                   "this section shows only the sequence diagrams of its use cases.",
     "seq": [UC("reporting", "UC70a_ViewReportsAndStatistics.png"),
             UC("reporting", "UC70b_ExportReport.png")]},
    {"fe": "FE-16", "title": "Working Calendar & Meeting Time Search", "class_png": None,
     "class_note": "FE-16 has no class diagram in docs/07-classdiagram yet: "
                   "this section shows only the sequence diagrams of its use cases.",
     "seq": [UC("calendar-sharing", "UC84a_RequestCalendarShare.png"),
             UC("calendar-sharing", "UC84b_ShareCalendarDirectly.png"),
             UC("calendar-sharing", "UC84c_RespondToCalendarShare.png"),
             UC("calendar-sharing", "UC84d_ChangeCalendarShare.png"),
             UC("calendar-sharing", "UC84e_WithdrawOrEndCalendarShare.png"),
             UC("calendar-sharing", "UC84f_ViewCalendarShares.png"),
             UC("calendar-sharing", "UC85a_FindMeetingTimeSlots.png"),
             UC("calendar-sharing", "UC85b_CreateDraftMeetingFromSlot.png"),
             UC("calendar-sharing", "UC85c_ViewSharedCalendar.png")]},
]

# positional section numbers in the new document (FE order; FE-14 skipped)
NEW_POS = {"FE-01": 1, "FE-02": 2, "FE-03": 3, "FE-04": 4, "FE-05": 5,
           "FE-06": 6, "FE-07": 7, "FE-08": 8, "FE-09": 9, "FE-10": 10,
           "FE-11": 11, "FE-12": 12, "FE-13": 13, "FE-15": 14, "FE-16": 15}

# Section I figures: exact heading label -> repo PNG. md5-synced like section II.
SEC1_FIGURES = [
    ("1. Software Architecture", "docs/16-architecture-diagram/kien-truc-phan-mem.png"),
    ("2.1 Overview", "docs/15-package-diagram/package-tong-quan.png"),
    ("2.2 Back-End Package Diagram", "docs/15-package-diagram/package-backend.png"),
    ("2.3 Domain Package Diagram", "docs/15-package-diagram/package-domain.png"),
    ("2.4 Front-End Package Diagram", "docs/15-package-diagram/package-frontend.png"),
]

# Section I.3: the target table-form ERD figure straight under H2, then one
# numbered H3 per entity spec table copied from Report 3 section 3.2
# (Report 3 stays the authored source; reruns re-copy when the table count
# differs). The Chen view and the old "As-built" H3 are not used here.
I3_ERD_PNG = "docs/13-erd/muc-tieu/trustcabinet-erd-muc-tieu.drawio.png"
I3_ERD_CAPTION = "Figure 3: Database Design"
R3_SOURCE = REPO / "docs" / "08-reports" / "source" / "Report_3_Software_Requirement_Specification.docx"
R3_TABLES_H3 = "3.2 Entity Descriptions"
R3_NEXT_H3 = "3.3 Data Business Rules"


# ---------------------------------------------------------------- helpers

def png_size(data: bytes) -> tuple[int, int]:
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG file")
    w, h = struct.unpack(">II", data[16:24])
    return w, h


def fit_emu(png: Path, width_emu: int | None = None) -> tuple[int, int]:
    w, h = png_size(png.read_bytes())
    if width_emu is None:
        width_emu = min(int(w * EMU_PER_INCH / 96), PORTRAIT_CONTENT_EMU)
    height_emu = int(width_emu * h / w)
    return width_emu, height_emu


def para_text(xml: str) -> str:
    import html as _html
    return _html.unescape("".join(T_RE.findall(xml))).strip()


def para_style(xml: str) -> str:
    m = STYLE_RE.search(xml)
    return m.group(1) if m else ""


def set_para_text(xml: str, text: str) -> str:
    """Put all text into the first <w:t>, empty the rest. Keeps runs/bookmarks."""
    import html as _html
    first = True

    def _sub(m: re.Match) -> str:
        nonlocal first
        if first:
            first = False
            return m.group(1) + _html.escape(text) + m.group(3)
        return m.group(1) + m.group(3)

    return re.sub(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", _sub, xml, flags=re.S)


def set_first_run_text(xml: str, text: str) -> str:
    """Replace the content of the FIRST <w:t> only; keep all other runs/fields intact."""
    import html as _html
    done = False

    def _sub(m: re.Match) -> str:
        nonlocal done
        if done:
            return m.group(0)
        done = True
        return m.group(1) + _html.escape(text) + m.group(3)

    return re.sub(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", _sub, xml, flags=re.S, count=1)


def uc_key_from_png(png: str) -> str:
    """Authoritative UC id from the PNG file name: UC12a_... -> UC-12a."""
    m = re.match(r"UC(\d+[a-z]?)", Path(png).stem)
    if not m:
        raise ValueError(f"cannot derive UC id from {png}")
    return f"UC-{m.group(1)}"


def humanize(stem: str) -> str:
    name = re.sub(r"^UC\d+[a-z]?[_ ]", "", stem)
    name = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", name).replace("_", " ").strip()
    return re.sub(r"\s+", " ", name)


def uc_title(png: str, txt: str | None) -> str:
    if txt:
        line = Path(txt).read_text(encoding="utf-8").splitlines()[0]
        line = line[len("title "):] if line.startswith("title ") else line
        return line.replace(" - ", " \u2013 ", 1)
    key = uc_key_from_png(png)
    return f"{key} \u2013 {humanize(Path(png).stem)}"


def checked_title(png: str, txt: str | None) -> tuple[str, str | None]:
    """Title for a new heading; flag when the .txt names another UC (stale
    source, e.g. role-management DCD-10: file UC10_*.png titled UC-08)."""
    key = uc_key_from_png(png)
    if not txt:
        return f"{key} \u2013 {humanize(Path(png).stem)}", f"no .txt for {Path(png).name}"
    title = uc_title(png, txt)
    m = UC_ID_RE.search(title)
    if not m or m.group(0) != key:
        return (f"{key} \u2013 {humanize(Path(png).stem)}",
                f"STALE-TITLE {Path(png).name}: txt says {(m.group(0) if m else 'nothing')}")
    return title, None


CAPTION_RE = re.compile(r"^Figure\s+\d")


def caption_para(caption: str) -> str:
    """Centered italic caption paragraph placed right under a figure."""
    import html as _h
    return ('<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:i/><w:iCs/></w:rPr>'
            f'<w:t xml:space="preserve">{_h.escape(caption)}</w:t></w:r></w:p>')


def caption_for_seq(num: str, title: str) -> str:
    return f"Figure {num}: Sequence Diagram of {title}"


def caption_for_class(num: str, fe: str, fe_title: str) -> str:
    return f"Figure {num}: Class Diagram of {fe} {fe_title}"


# ---------------------------------------------------------------- model of document.xml

class Doc:
    def __init__(self, xml: str):
        self.xml = xml
        spans = [(m.start(), m.end(), m.group(0)) for m in P_RE.finditer(xml)]
        self.paras = [{"start": s, "end": e, "xml": x,
                       "style": para_style(x), "text": para_text(x)} for s, e, x in spans]

    def headings(self, style: str) -> list[int]:
        return [i for i, p in enumerate(self.paras) if p["style"] == style]


# ---------------------------------------------------------------- planner + builder

class Filler:
    def __init__(self, source: Path):
        self.source = source
        self.zin = zipfile.ZipFile(source)
        self.files = {info.filename: self.zin.read(info.filename) for info in self.zin.infolist()}
        self.doc = Doc(self.files["word/document.xml"].decode("utf8"))
        self.rels_xml = self.files["word/_rels/document.xml.rels"].decode("utf8")
        self.plan: list[str] = []
        self.warn: list[str] = []
        self.relabel: dict[int, str] = {}  # para idx -> new text (edits applied at the end)
        self.replacements: dict[tuple[int, int], str] = {}  # (start, end) -> new para xml
        self.inserts: list[tuple[int, int, str]] = []  # (pos, seq, xml); see apply()
        self._emit_seq = 0
        self.new_files: dict[str, bytes] = {}
        self.rels_add: list[str] = []
        rel_ids = [int(x) for x in re.findall(r'Id="rId(\d+)"', self.rels_xml)]
        self.next_rid = max(rel_ids) + 1
        doc_ids = [int(x) for x in re.findall(r'wp:docPr[^>]*\bid="(\d+)"', self.doc.xml)]
        self.next_docpr = max(doc_ids) + 1000
        bm_ids = [int(x) for x in re.findall(r'w:bookmarkStart[^>]*w:id="(\d+)"', self.doc.xml)]
        self.next_bmid = (max(bm_ids) if bm_ids else 0) + 1000
        toc_names = [int(x) for x in re.findall(r"_Toc(\d+)", self.doc.xml)]
        self.next_toc = (max(toc_names) if toc_names else 244118900) + 1
        self._touched_media: set[str] = set()
        self._heading_used: set[int] = set()
        self._doomed: list[tuple[int, int]] = []  # spans replaced wholesale (I.3); captions skip these
        self.media_of_rid = dict(re.findall(
            r'<Relationship Id="(rId[^"]+)"[^>]*Target="(media/[^"]+)"', self.rels_xml))
        self._templates()

    def pt(self, idx: int) -> str:
        """Current text of a paragraph, including pending relabels."""
        hit = self.relabel.get(idx)
        if hit is not None:
            return hit
        text = self.doc.paras[idx]["text"]
        assert isinstance(text, str)
        return text

    # -- templates cloned from existing paragraphs -------------------------
    def _templates(self):
        d = self.doc
        # match by FE tag / UC id so both pre-fill ("8. FE-11") and filled ("11. FE-11") docs work
        h2 = next(i for i in d.headings("Heading2") if "FE-11" in d.paras[i]["text"])
        h3 = next(i for i in d.headings("Heading3")
                  if re.search(r"FE-11|Sequence Diagram", d.paras[i]["text"])
                  and "Class Diagram" not in d.paras[i]["text"])
        h4 = next(i for i in d.headings("Heading4") if "UC-76a" in d.paras[i]["text"])
        img = next(i for i in range(h4, len(d.paras)) if "<w:drawing>" in d.paras[i]["xml"])
        note = next(i for i in d.headings("Heading2"))
        note = next(i for i in range(note, len(d.paras))
                    if d.paras[i]["style"] == "" and "has no class diagram yet" in d.paras[i]["text"])
        toc2 = next(i for i, p in enumerate(d.paras) if p["style"] == "TOC2")
        toc3 = next(i for i, p in enumerate(d.paras) if p["style"] == "TOC3")
        toc4s = [i for i, p in enumerate(d.paras) if p["style"] == "TOC4"]
        self.tpl = {k: d.paras[v]["xml"] for k, v in
                    {"h2": h2, "h3": h3, "h4": h4, "img": img, "note": note,
                     "toc2": toc2, "toc3": toc3}.items()}
        self.tpl["toc4"] = d.paras[toc4s[0]]["xml"] if toc4s else None

    # -- id minting ---------------------------------------------------------
    def _fresh_heading(self, template_xml: str) -> str:
        xml = re.sub(r'w14:paraId="[^"]*"', "", template_xml)
        xml = re.sub(r'w14:textId="[^"]*"', "", xml)
        old_ids = re.findall(r'w:bookmarkStart[^>]*w:id="(\d+)"[^>]*w:name="(_Toc\d+)"', xml)
        for old_id, old_name in old_ids:
            new_name = f"_Toc{self.next_toc}"
            self.next_toc += 1
            xml = xml.replace(old_name, new_name)
        for old_id, _ in old_ids:
            xml = xml.replace(f'w:id="{old_id}"', f'w:id="{self.next_bmid}"', 1)
            # pair bookmarkEnd with the same new id (first remaining occurrence)
            xml = re.sub(f'w:bookmarkEnd[^>]*w:id="{old_id}"',
                         f'w:bookmarkEnd w:id="{self.next_bmid}"', xml, count=1)
            self.next_bmid += 1
        return xml

    def last_toc_name(self, heading_xml: str) -> str | None:
        m = re.search(r'w:name="(_Toc\d+)"', heading_xml)
        return m.group(1) if m else None

    # -- image paragraphs ----------------------------------------------------
    def _image_para(self, png: Path, width_emu: int | None = None,
                    alt: str | None = None) -> tuple[str, str, str]:
        """Return (para_xml, media_name, rid) for a new image paragraph."""
        w_emu, h_emu = fit_emu(png, width_emu)
        if h_emu > TALL_WARN_EMU:
            self.warn.append(f"{png.name}: {h_emu / EMU_PER_INCH:.1f}in tall - "
                             "split manually in Word (precedent: UC-33b, 29/10).")
        rid = f"rId{self.next_rid}"
        self.next_rid += 1
        media = f"media/r4fill-{rid}-{png.name}"
        self.new_files[f"word/{media}"] = png.read_bytes()
        self.rels_add.append(
            f'<Relationship Id="{rid}" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" '
            f'Target="{media}"/>')
        xml = self.tpl["img"]
        self.next_docpr += 1
        xml = re.sub(r'a:blip r:embed="rId[^"]+"', f'a:blip r:embed="{rid}"', xml)
        xml = re.sub(r'<wp:extent cx="\d+" cy="\d+"',
                     f'<wp:extent cx="{w_emu}" cy="{h_emu}"', xml)
        xml = re.sub(r'<a:ext cx="\d+" cy="\d+"',
                     f'<a:ext cx="{w_emu}" cy="{h_emu}"', xml)
        if alt is None:
            import html as _h
            alt = _h.escape(uc_title(str(png), None))
        docpr = f'<wp:docPr id="{self.next_docpr}" name="Picture {self.next_docpr}" descr="{alt}"/>'
        xml2 = re.sub(r'<wp:docPr\b[^>]*/>', docpr, xml, count=1)
        if xml2 == xml:  # non-self-closing form (not seen in this file, be safe)
            xml2 = re.sub(r'<wp:docPr\b[^>]*>', docpr, xml, count=1)
        xml = xml2
        return xml, media, rid

    # -- section II range ----------------------------------------------------
    def section2(self) -> tuple[int, int]:
        h1 = self.doc.headings("Heading1")
        start = next(i for i in h1 if self.doc.paras[i]["text"] == "II. Detailed Code Design")
        end = next(i for i in h1 if self.doc.paras[i]["text"].startswith("III."))
        return start, end

    def images_after(self, idx: int, end: int) -> list[int]:
        """Image para indices after heading idx up to (not incl.) next heading."""
        out = []
        for j in range(idx + 1, end if end else len(self.doc.paras)):
            st = self.doc.paras[j]["style"]
            if st in ("Heading1", "Heading2", "Heading3", "Heading4"):
                break
            if "<w:drawing>" in self.doc.paras[j]["xml"]:
                out.append(j)
        return out

    # -- main -----------------------------------------------------------------
    def run(self) -> None:
        self._scan_titles()
        self._replace_stale_images()
        self._sync_section_one()
        self._renumber_and_extend()

    def _scan_titles(self) -> None:
        for fe in FE_SECTIONS:
            for png_s, txt in fe["seq"]:
                if not Path(png_s).is_file():
                    self.plan.append(f"MISSING-FILE {png_s}")
                    continue
                _, problem = checked_title(png_s, txt)
                if problem:
                    self.warn.append(problem)

    def _replace_stale_images(self) -> None:
        s2, e2 = self.section2()
        # class diagrams: image para right after each "Class Diagram" H3
        for fe in FE_SECTIONS:
            png = Path(fe["class_png"]) if fe.get("class_png") else None
            if not png or not png.is_file():
                if fe.get("class_png"):
                    self.plan.append(f"MISSING-FILE {png}")
                continue
            h3 = next((i for i in range(s2, e2)
                       if self.doc.paras[i]["style"] == "Heading3"
                       and self.pt(i).endswith("Class Diagram")
                       and self._fe_of(i) == fe["fe"]), None)
            if h3 is None:
                continue  # new FE section: image added by _insert_new_sections
            imgs = self.images_after(h3, e2)
            if len(imgs) != 1:
                self.warn.append(f"{fe['fe']} class: expected 1 image para, found {len(imgs)}")
                continue
            self._swap_image(imgs[0], png, f"class {fe['fe']}")
        # sequence diagrams
        # map uc id -> heading indices (section II, Heading4)
        uc_heads: dict[str, list[int]] = {}
        for i in range(s2, e2):
            p = self.doc.paras[i]
            if p["style"] == "Heading4":
                m = UC_ID_RE.search(p["text"])
                if m:
                    uc_heads.setdefault(m.group(0), []).append(i)
        for fe in FE_SECTIONS:
            frozen = fe.get("frozen", set())
            for png_s, txt in fe["seq"]:
                png = Path(png_s)
                if not png.is_file():
                    self.plan.append(f"MISSING-FILE {png}")
                    continue
                uc = uc_key_from_png(png_s)
                heads = uc_heads.get(uc, [])
                if uc in frozen:
                    imgs = [j for h in heads for j in self.images_after(h, e2)]
                    self.plan.append(f"FROZEN {uc}: keep {len(imgs)} manual halve(s), repo PNG not applied")
                    continue
                if not heads:
                    continue  # handled by _renumber_and_extend (new headings)
                # existing heading(s): exactly one image para expected per heading here
                # (multi-PNG UCs have one heading per PNG: 33a, 39a, 45c)
                target = self._pick_heading(heads, png_s, txt)
                if target in self._heading_used:
                    self.warn.append(f"{uc}: two PNGs map to one heading - manual check")
                    continue
                self._heading_used.add(target)
                imgs = self.images_after(target, e2)
                if len(imgs) != 1:
                    self.warn.append(f"{uc}: expected 1 image para, found {len(imgs)} - manual check")
                    continue
                j = imgs[0]
                self._swap_image(j, png, uc)

    def _swap_image(self, para_idx: int, png: Path, tag: str) -> None:
        j = para_idx
        emb = re.search(r'a:blip r:embed="(rId[^"]+)"', self.doc.paras[j]["xml"])
        media = self.media_of_rid.get(emb.group(1)) if emb else None
        if not media:
            self.warn.append(f"{tag}: image rel not found - manual check")
            return
        old = self.files[f"word/{media}"]
        new = png.read_bytes()
        if hashlib.md5(old).digest() == hashlib.md5(new).digest():
            self.plan.append(f"SKIP {tag}: image up to date")
        else:
            if media in self._touched_media:
                self.warn.append(f"{media} mapped twice - manual check")
                return
            self._touched_media.add(media)
            m = re.search(r'<wp:extent cx="(\d+)"', self.doc.paras[j]["xml"])
            assert m is not None
            cur_w = int(m.group(1))
            _, h_emu = fit_emu(png, cur_w)
            self.plan.append(f"REPLACE {tag}: {media} ({len(old)} -> {len(new)} bytes)")
            self.new_files[f"word/{media}"] = new
            self._extent_edit(j, cur_w, h_emu, png.name)

    def _fe_of(self, idx: int) -> str:
        """FE tag of the nearest preceding Heading2 (searches backwards)."""
        for i in range(idx, -1, -1):
            p = self.doc.paras[i]
            if p["style"] == "Heading2":
                m = re.search(r"(FE-\d+)", p["text"])
                if m:
                    return m.group(1)
        return ""

    def _pick_heading(self, heads: list[int], png_s: str, txt: str | None) -> int:
        """For UCs with several headings (33a/39a/45c), pick by matching title tail.

        Exact tail match first, then longest tail containing the wanted title.
        Never match a shorter tail inside a longer want (that maps two PNGs
        onto the first heading and clobbers it on reruns).
        """
        if len(heads) == 1:
            return heads[0]
        title = uc_title(png_s, txt)
        want = title.split("\u2013", 1)[1].strip().lower() if "\u2013" in title else title.lower()
        cands: list[tuple[int, str]] = []
        for h in heads:
            t = self.pt(h)
            tail = t.split("\u2013", 1)[1].strip().lower() if "\u2013" in t else t.lower()
            cands.append((h, tail))
        for h, tail in cands:
            if tail == want:
                return h
        longer = [(h, tail) for h, tail in cands if want in tail]
        if longer:
            return max(longer, key=lambda ht: len(ht[1]))[0]
        self.warn.append(f"{png_s}: no heading matches '{want[:50]}' - using first")
        return heads[0]

    def _extent_edit(self, para_idx: int, w_emu: int, h_emu: int, name: str) -> None:
        p = self.doc.paras[para_idx]
        xml = p["xml"]
        xml = re.sub(r'<wp:extent cx="\d+" cy="\d+"',
                     f'<wp:extent cx="{w_emu}" cy="{h_emu}"', xml)
        xml = re.sub(r'<a:ext cx="\d+" cy="\d+"',
                     f'<a:ext cx="{w_emu}" cy="{h_emu}"', xml)
        self.replacements[(p["start"], p["end"])] = xml
        if h_emu > TALL_WARN_EMU:
            self.warn.append(f"{name}: {h_emu / EMU_PER_INCH:.1f}in tall - split manually in Word.")

    # -- renumber + new sections + TOC --------------------------------------
    def _sync_section_one(self) -> None:
        """md5-sync the section I figures (architecture, package) in place."""
        d = self.doc
        h1_ii = next(i for i in d.headings("Heading1") if d.paras[i]["text"].startswith("II."))
        for label, png_s in SEC1_FIGURES:
            png = Path(png_s)
            if not png.is_file():
                self.plan.append(f"MISSING-FILE {png}")
                continue
            first = label.split(" ", 1)[0]
            lvl = "Heading2" if first.endswith(".") else "Heading3"
            idx = next((i for i in d.headings(lvl)
                        if self.pt(i) == label and i < h1_ii), None)
            if idx is None:
                self.warn.append(f"sec-I heading missing: '{label}' - image not synced")
                continue
            imgs = self.images_after(idx, h1_ii)
            if len(imgs) == 1:
                self._swap_image(imgs[0], png, f"sec-I {label}")
            else:
                self.warn.append(f"{label}: expected 1 image para, found {len(imgs)} - manual check")

    def _r3_entities(self) -> tuple[list[tuple[str, str]], int]:
        """Split Report 3 section 3.2 into one chunk per entity.

        Returns ([(entity name, chunk xml without the "Entity N:" marker para)],
        total table count). Bookmark ids/names are reminted so they cannot
        collide with Report 4's own bookmarks/TOC anchors.
        """
        raw = zipfile.ZipFile(R3_SOURCE).read("word/document.xml").decode("utf8")
        paras = [(m.start(), m.end(), m.group(0)) for m in P_RE.finditer(raw)]
        def _tx(x: str) -> str:
            import html as _h
            return _h.unescape("".join(T_RE.findall(x))).strip()
        start = next(e for s, e, x in paras
                     if para_style(x) == "Heading3" and _tx(x) == R3_TABLES_H3)
        stop = next(s for s, e, x in paras
                    if para_style(x) == "Heading3" and _tx(x) == R3_NEXT_H3)
        assert start < stop
        xml = raw[start:stop]
        xml = re.sub(r'w14:paraId="[^"]*"', "", xml)
        xml = re.sub(r'w14:textId="[^"]*"', "", xml)
        for old_id, old_name in re.findall(
                r'w:bookmarkStart[^>]*w:id="(\d+)"[^>]*w:name="(_Toc\d+)"', xml):
            new_name = f"_Toc{self.next_toc}"
            self.next_toc += 1
            xml = xml.replace(old_name, new_name)
            xml = xml.replace(f'w:id="{old_id}"', f'w:id="{self.next_bmid}"', 1)
            xml = re.sub(f'w:bookmarkEnd[^>]*w:id="{old_id}"',
                         f'w:bookmarkEnd w:id="{self.next_bmid}"', xml, count=1)
            self.next_bmid += 1
        marks: list[tuple[int, int, str]] = []  # (rel start, rel end, name)
        for m in P_RE.finditer(xml):
            t = _tx(m.group(0))
            mm = re.match(r"^Entity\s+\S+:\s*(.+)$", t)
            if mm:
                marks.append((m.start(), m.end(), mm.group(1).strip()))
        entities: list[tuple[str, str]] = []
        for k, (ms, me, name) in enumerate(marks):
            chunk_end = marks[k + 1][0] if k + 1 < len(marks) else len(xml)
            entities.append((name, xml[me:chunk_end]))
        return entities, len(re.findall("<w:tbl>", xml))

    def _rebuild_i3(self) -> None:
        """Replace the I.3 template zone with the target ERD + per-table H3s.

        Layout: the target ERD figure straight under H2 (caption
        "Figure 3: Database Design"), then one H3 per entity ("3.1 User",
        "3.2 Role", ...) with its description + table copied from Report 3
        section 3.2. Reruns md5-sync the ERD image when the headings and
        table count already match; otherwise the zone is rebuilt (this also
        drops the old "As-built"/Chen H3s).
        """
        d = self.doc
        h2_3 = next((i for i in d.headings("Heading2") if self.pt(i) == "3. Database Design"), None)
        h1_ii = next((i for i in d.headings("Heading1") if d.paras[i]["text"].startswith("II.")), None)
        if h2_3 is None or h1_ii is None:
            self.warn.append("I.3 or II. heading not found - skipping I.3 rebuild")
            return
        if not R3_SOURCE.is_file():
            self.warn.append(f"Report 3 not found at {R3_SOURCE} - skipping I.3 tables")
            return
        entities, r3_tables = self._r3_entities()
        want = [f"3.{k} {name}" for k, (name, _) in enumerate(entities, start=1)]
        zone_h3 = [self.pt(i) for i in range(h2_3 + 1, h1_ii) if d.paras[i]["style"] == "Heading3"]
        zone_raw_tables = self.doc.xml[d.paras[h2_3]["end"]:d.paras[h1_ii]["start"]].count("<w:tbl>")
        if zone_h3 == want and zone_raw_tables == r3_tables:
            # the ERD image sits straight under H2 (images_after stops at H3 3.1)
            imgs = self.images_after(h2_3, h1_ii)
            if len(imgs) == 1:
                self._swap_image(imgs[0], Path(I3_ERD_PNG), "sec-I ERD")
            else:
                self.warn.append(f"I.3 ERD: expected 1 image para under H2, found {len(imgs)} - manual check")
            self.plan.append("SKIP I.3: structure + tables already match Report 3 (image synced)")
            return
        parts: list[str] = []
        new_toc: list[tuple[str, str | None]] = []  # (label, heading anchor)
        png = Path(I3_ERD_PNG)
        if png.is_file():
            img_xml, _, _ = self._image_para(png, alt="Database diagram (target ERD)")
            parts += [img_xml, caption_para(I3_ERD_CAPTION)]
            self.plan.append(f"ADD I.3 ERD: {png.name}")
        else:
            self.plan.append(f"MISSING-FILE {png}")
        for k, (name, chunk) in enumerate(entities, start=1):
            label = f"3.{k} {name}"
            h3_xml, h3_anchor = self._new_heading(3, label)
            parts += [h3_xml, chunk]
            new_toc.append((label, h3_anchor))
        self.plan.append(f"ADD I.3 tables: {len(entities)} H3s, {r3_tables} tables from Report 3 §3.2")
        self.replacements[(d.paras[h2_3]["end"], d.paras[h1_ii]["start"])] = "".join(parts)
        self._doomed.append((d.paras[h2_3]["end"], d.paras[h1_ii]["start"]))
        # stale template TOC3 lines ("3.1 table_name1", ...) live between TOC2 "3." and the next TOC2
        toc2_3 = next((i for i, p in enumerate(d.paras)
                       if p["style"] == "TOC2" and self.pt(i).split("\t")[0].startswith("3. ")), None)
        if toc2_3 is None:
            self.warn.append("TOC2 '3. Database Design' missing - new I.3 TOC lines skipped")
            return
        nxt_toc2 = next((i for i in range(toc2_3 + 1, len(d.paras))
                         if d.paras[i]["style"] == "TOC2"), len(d.paras))
        import html as _h
        for i in range(toc2_3 + 1, nxt_toc2):
            p = d.paras[i]
            if p["style"] != "TOC3":
                continue
            m = re.search(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", p["xml"], flags=re.S)
            lbl = _h.unescape(m.group(2)) if m else ""
            # delete every 3.x line here (kept labels included: fresh lines are
            # emitted below, otherwise the kept label duplicates)
            if re.match(r"^3\.\d+\s", lbl):
                self._replace_para(i, "")
                self.plan.append(f"DELETE-TOC '{lbl[:60]}'")
        page_hint = self._toc_page_hint(toc2_3)
        self._emit(d.paras[toc2_3]["end"], "".join(
            self._toc_line(3, label, anchor, page_hint) for label, anchor in new_toc))
        self.warn.append("I.3 ERD inserted portrait-fit - move to a landscape page in Word if needed; "
                          "entity tables copy Report 3 §3.2 - re-check column widths after sync")

    def _renumber_and_extend(self) -> None:
        self._rebuild_i3()
        s2, e2 = self.section2()
        # One-way gate: the 8/9/10 -> 11/12/13 renumber applies ONLY to the
        # pre-fill sections FE-11/FE-12/FE-13. On an already-filled document
        # 8/9/10 belong to FE-08/09/10 and must be left alone (rerunning the
        # script once renumbered FE-08 to 11.x, colliding with FE-11).
        old_to_fe = {8: "FE-11", 9: "FE-12", 10: "FE-13"}
        new_pos = {"FE-11": 11, "FE-12": 12, "FE-13": 13}
        cur_fe = ""
        for i in range(s2, e2):
            p = self.doc.paras[i]
            st = p["style"]
            if st == "Heading2":
                m = re.match(r"^(\d+)\.\s+(.*)$", self.pt(i))
                if m and int(m.group(1)) in old_to_fe:
                    want_fe = old_to_fe[int(m.group(1))]
                    if want_fe in m.group(2):
                        self._relabel(i, f"{new_pos[want_fe]}. {m.group(2)}")
                fm = re.search(r"(FE-\d+)", self.pt(i))
                cur_fe = fm.group(1) if fm else ""
                continue
            if st not in ("Heading3", "Heading4"):
                continue
            m = HEADING_NUM_RE.match(self.pt(i))
            if not m:
                continue
            nums = [int(x) for x in m.group(1).split(".")]
            if nums[0] in old_to_fe and cur_fe == old_to_fe[nums[0]]:
                new_sec = new_pos[cur_fe]
                if len(nums) == 2 and nums == [8, 1]:
                    new = [11, 2]  # FE-11 sequence H3 (11.1 class inserted before)
                elif len(nums) == 3 and nums[0] == 8:
                    new = [11, 2, nums[2] + 7]  # 76a/76b shift after 7 new UCs
                else:
                    new = [new_sec] + nums[1:]
                self._relabel(i, ".".join(map(str, new)) + " " + m.group(2))
        # TOC labels sync by FE/UC identity (the old TOC is already stale:
        # it still says 9.x where the headings say 10.x, so number arithmetic
        # alone would map it wrong). New labels = the relabeled heading texts.
        new_h2: dict[str, str] = {}
        new_h3: dict[tuple[str, str], str] = {}
        new_h4_by_label: dict[str, str] = {}  # old H4 text -> new H4 text (exact; avoids 33a cross-talk)
        cur_fe = ""
        for i in range(s2, e2):
            p = self.doc.paras[i]
            st = p["style"]
            if st == "Heading2":
                fm = re.search(r"(FE-\d+)", self.pt(i))
                cur_fe = fm.group(1) if fm else ""
                if cur_fe:
                    new_h2[cur_fe] = self.pt(i)
            elif st == "Heading3":
                m = HEADING_NUM_RE.match(self.pt(i))
                if m and cur_fe:
                    new_h3[(cur_fe, m.group(2))] = self.pt(i)
            elif st == "Heading4":
                if p["text"] != self.pt(i):  # this heading is being renumbered
                    new_h4_by_label[p["text"]] = self.pt(i)
        cur_toc_fe = ""
        for i, p in enumerate(self.doc.paras):
            st = p["style"]
            if st not in ("TOC2", "TOC3", "TOC4"):
                continue
            cur = re.search(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", p["xml"], flags=re.S)
            if not cur:
                continue
            label = cur.group(2)
            if st == "TOC2":
                m = re.search(r"(FE-\d+)", label)
                cur_toc_fe = m.group(1) if m else ""
                if cur_toc_fe and cur_toc_fe in new_h2:
                    import html as _h
                    if _h.unescape(label) != new_h2[cur_toc_fe]:
                        self._relabel(i, new_h2[cur_toc_fe], first_run_only=True)
                continue
            if st == "TOC3":
                m = HEADING_NUM_RE.match(label)
                if m and (cur_toc_fe, m.group(2)) in new_h3:
                    import html as _h
                    if _h.unescape(label) != new_h3[(cur_toc_fe, m.group(2))]:
                        self._relabel(i, new_h3[(cur_toc_fe, m.group(2))], first_run_only=True)
                continue
            import html as _h
            if _h.unescape(label) in new_h4_by_label:
                self._relabel(i, new_h4_by_label[_h.unescape(label)], first_run_only=True)
        self._ensure_toc_coverage()
        self._ensure_captions()
        self._insert_new_sections()

    def _ensure_toc_coverage(self) -> None:
        """Every section-II heading needs a TOC line. FE-12's whole block is
        missing (TOC predates its insertion), so insert absent lines in order."""
        import html as _h
        d = self.doc
        s2, e2 = self.section2()
        toc_labels: dict[str, int] = {}
        for i, p in enumerate(d.paras):
            if p["style"] in ("TOC2", "TOC3", "TOC4"):
                m = re.search(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", p["xml"], flags=re.S)
                if m:
                    toc_labels.setdefault(_h.unescape(m.group(2)), i)
        anchor: int | None = None
        pending: list[str] = []
        page_hint = "?"
        for i in range(s2, e2):
            p = d.paras[i]
            lvl = {"Heading2": 2, "Heading3": 3, "Heading4": 4}.get(p["style"])
            if not lvl:
                continue
            want = self.pt(i)
            if want in toc_labels:
                if pending and anchor is not None:
                    self._emit(anchor, "".join(pending))
                    pending = []
                anchor = d.paras[toc_labels[want]]["end"]
                page_hint = self._toc_page_hint(toc_labels[want])
                continue
            if anchor is None:
                self.warn.append(f"no TOC anchor for '{want[:60]}' - add manually in Word")
                continue
            pending.append(self._toc_line(lvl, want, self._heading_anchor(i), page_hint))
            self.plan.append(f"ADD-TOC '{want[:70]}'")
        if pending and anchor is not None:
            self._emit(anchor, "".join(pending))

    def _heading_anchor(self, idx: int) -> str:
        """Bookmark name of a heading; mint + inject one if it has none."""
        p = self.doc.paras[idx]
        key = (p["start"], p["end"])
        hit = self.replacements.get(key)
        if hit is not None:
            xml = hit
        else:
            v = p["xml"]
            assert isinstance(v, str)
            xml = v
        name = self.last_toc_name(xml)
        if name:
            return name
        name = f"_Toc{self.next_toc}"
        self.next_toc += 1
        bid = self.next_bmid
        self.next_bmid += 1
        xml = re.sub(r"(<w:p\b[^>]*>)", rf'\1<w:bookmarkStart w:id="{bid}" w:name="{name}"/>',
                     xml, count=1)
        xml = re.sub(r"</w:p>$", f'<w:bookmarkEnd w:id="{bid}"/></w:p>', xml)
        self.replacements[key] = xml
        return name

    def _replace_para(self, idx: int, xml: str) -> None:
        p = self.doc.paras[idx]
        self.replacements[(p["start"], p["end"])] = xml

    def _relabel(self, idx: int, new_text: str, first_run_only: bool = False) -> None:
        p = self.doc.paras[idx]
        if first_run_only:
            # TOC line: label is the first <w:t>; page number field stays intact
            cur = re.search(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", p["xml"], flags=re.S)
            if not cur or cur.group(2) == new_text:
                return
            self._replace_para(idx, set_first_run_text(p["xml"], new_text))
            self.relabel[idx] = new_text + self.pt(idx)[len(cur.group(2)):]
            self.plan.append(f"RENUMBER-TOC '{cur.group(2)[:60]}' -> '{new_text[:60]}'")
            return
        if self.pt(idx) != new_text:
            self._replace_para(idx, set_para_text(p["xml"], new_text))
            self.relabel[idx] = new_text
            self.plan.append(f"RENUMBER '{p['text'][:60]}' -> '{new_text[:60]}'")

    def _ensure_captions(self) -> None:
        """Figure caption under every diagram in sections I and II.

        Runs on original positions (single-pass apply keeps them valid) but
        uses final heading labels, so captions stay in sync after renumbering.
        Groups of consecutive image paras share one caption (frozen UC-33b
        halves); an existing caption right after the group is updated in
        place, otherwise a new centered italic line is inserted.
        """
        import html as _h
        d = self.doc
        h1 = d.headings("Heading1")
        start = next(i for i in h1 if d.paras[i]["text"] == "I. High Level Design")
        end = next(i for i in h1 if d.paras[i]["text"].startswith("III."))
        cur: tuple[str, str, str, str, str] | None = None  # h1, level, num, title, fe
        fe_tag, fe_title = "", ""
        group: list[int] = []
        added = updated = kept = 0

        def want_caption() -> str | None:
            if cur is None:
                return None
            _, level, num, title, fe = cur
            if level == "Heading4":
                return caption_for_seq(num, title)
            if level == "Heading3" and title == "Class Diagram" and fe:
                return caption_for_class(num, fe, fe_title)
            return f"Figure {num}: {title}"

        def flush() -> None:
            nonlocal added, updated, kept
            if not group or cur is None:
                return
            want = want_caption()
            if not want:
                return
            nxt = group[-1] + 1
            while nxt < len(d.paras) and d.paras[nxt]["text"] == "" \
                    and "<w:drawing>" not in d.paras[nxt]["xml"]:
                nxt += 1
            if nxt < len(d.paras) and CAPTION_RE.match(_h.unescape(d.paras[nxt]["text"])):
                if d.paras[nxt]["text"] != want:
                    self._replace_para(nxt, caption_para(want))
                    self.plan.append(f"UPDATE-CAPTION '{want[:70]}'")
                    updated += 1
                else:
                    kept += 1
            else:
                self._emit(d.paras[group[-1]]["end"], caption_para(want))
                self.plan.append(f"ADD-CAPTION '{want[:70]}'")
                added += 1

        for i in range(start, end):
            if any(a <= d.paras[i]["start"] < b for a, b in self._doomed):
                continue  # zone replaced wholesale by _rebuild_i3
            p = d.paras[i]
            st = p["style"]
            if st == "Heading1":
                flush()
                group = []
                cur = None
            elif st in ("Heading2", "Heading3", "Heading4"):
                flush()
                group = []
                label = self.pt(i)
                m2 = re.match(r"^(\d+)\.\s+(.*)$", label)
                m34 = HEADING_NUM_RE.match(label)
                m = m2 if st == "Heading2" else m34
                if st == "Heading2":
                    fm = re.search(r"(FE-\d+)", label)
                    fe_tag = fm.group(1) if fm else ""
                    # H2 label already carries the tag ("FE-03 Role Management") — strip it
                    fe_title = re.sub(r"^FE-\d+\s+", "", m.group(2)) if m else ""
                if m:
                    cur = (st, st, m.group(1), m.group(2), fe_tag)
                else:
                    cur = None
            elif "<w:drawing>" in p["xml"]:
                group.append(i)
            elif p["text"] != "":
                flush()
                group = []
        flush()
        self.plan.append(f"CAPTIONS: {added} added, {updated} updated, {kept} up to date")

    def _insert_new_sections(self) -> None:
        d = self.doc
        s2, e2 = self.section2()
        # anchor: Heading2 FE-11 (now 11.) and Heading1 III.
        h2_11 = next(i for i in range(s2, e2)
                     if d.paras[i]["style"] == "Heading2" and "FE-11" in d.paras[i]["text"])
        h1_3 = next(i for i in d.headings("Heading1") if d.paras[i]["text"].startswith("III."))
        # FE-11: replace stale note with class diagram; insert 7 UC headings before 76a
        self._extend_fe11(h2_11)
        # new FE sections in document order (same anchor => emission order kept)
        for fe_name in ("FE-08", "FE-09", "FE-10"):
            fe = next(f for f in FE_SECTIONS if f["fe"] == fe_name)
            self._insert_fe_section(fe, h2_11)
        # append FE-15, FE-16 before Heading1 III.
        for fe_name in ("FE-15", "FE-16"):
            fe = next(f for f in FE_SECTIONS if f["fe"] == fe_name)
            self._insert_fe_section(fe, h1_3)

    def _new_heading(self, level: int, text: str) -> tuple[str, str | None]:
        tpl = self.tpl[{2: "h2", 3: "h3", 4: "h4"}[level]]
        xml = set_para_text(self._fresh_heading(tpl), text)
        return xml, self.last_toc_name(xml)

    def _toc_line(self, level: int, label: str, anchor: str | None, page_hint: str) -> str:
        import html as _html
        tpl = self.tpl[{2: "toc2", 3: "toc3", 4: "toc4"}[level]]
        if tpl is None:
            return ""
        xml = self._fresh_heading(tpl)
        if anchor:
            xml = re.sub(r'w:anchor="_Toc\d+"', f'w:anchor="{anchor}"', xml)
            xml = re.sub(r"PAGEREF _Toc\d+", f"PAGEREF {anchor}", xml)
        # first <w:t> = label, last <w:t> = cached page number; empty any in between
        slots = [m for m in re.finditer(r"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", xml, flags=re.S)]
        if not slots:
            self.warn.append(f"TOC template has no text slot for '{label[:50]}' - line skipped")
            return ""
        esc_label = _html.escape(label)
        out = xml[:slots[0].start()]
        out += slots[0].group(1) + esc_label + slots[0].group(3)
        # rebuild middle: keep separators between first and last slots, empty their texts
        pos = slots[0].end()
        for m in slots[1:-1]:
            out += xml[pos:m.start()] + m.group(1) + m.group(3)
            pos = m.end()
        if len(slots) > 1:
            out += xml[pos:slots[-1].start()]
            out += slots[-1].group(1) + _html.escape(page_hint) + slots[-1].group(3)
            out += xml[slots[-1].end():]
        else:
            out += xml[slots[0].end():]
        return out

    def _emit(self, pos: int, xml: str) -> None:
        if xml:
            self._emit_seq += 1
            self.inserts.append((pos, self._emit_seq, xml))

    def _insert_fe_section(self, fe: dict, anchor_idx: int) -> None:
        d = self.doc
        if any(p["style"] == "Heading2" and fe["fe"] in p["text"] for p in d.paras):
            self.plan.append(f"SKIP {fe['fe']}: section already present")
            return
        pos = d.paras[anchor_idx]["start"]
        n = NEW_POS[fe["fe"]]
        has_class = bool(fe.get("class_png"))
        seq_no = f"{n}.2" if has_class else f"{n}.1"
        parts: list[str] = []
        toc_parts: list[str] = []
        h2_label = f"{n}. {fe['fe']} {fe['title']}"
        h2_xml, h2_anchor = self._new_heading(2, h2_label)
        parts.append(h2_xml)
        # TOC anchor: insert after the last TOC line of the preceding group
        toc_anchor_idx = self._toc_insert_after(fe)
        page_hint = self._toc_page_hint(toc_anchor_idx)
        toc_parts.append(self._toc_line(2, h2_label, h2_anchor, page_hint))
        if has_class:
            png = Path(fe["class_png"])
            if png.is_file():
                h3 = f"{n}.1 Class Diagram"
                h3_xml, h3_anchor = self._new_heading(3, h3)
                img_xml, _, _ = self._image_para(png, alt=f"Class diagram {fe['fe']}")
                parts += [h3_xml, img_xml, caption_para(caption_for_class(f"{n}.1", fe["fe"], fe["title"]))]
                toc_parts.append(self._toc_line(3, h3, h3_anchor, page_hint))
                self.plan.append(f"ADD class {fe['fe']}: {png.name}")
            else:
                self.plan.append(f"MISSING-FILE {png}")
        elif fe.get("class_note"):
            parts.append(set_para_text(self._fresh_heading(self.tpl["note"]), fe["class_note"]))
        h3s = f"{seq_no} Sequence Diagram"
        h3s_xml, h3s_anchor = self._new_heading(3, h3s)
        parts += [h3s_xml]
        toc_parts.append(self._toc_line(3, h3s, h3s_anchor, page_hint))
        for k, (png_s, txt) in enumerate(fe["seq"], start=1):
            png = Path(png_s)
            if not png.is_file():
                self.plan.append(f"MISSING-FILE {png}")
                continue
            title, problem = checked_title(png_s, txt)
            if problem and problem not in self.warn:
                self.warn.append(problem)
            label = f"{seq_no}.{k} {title}"
            h4_xml, h4_anchor = self._new_heading(4, label)
            import html as _h2
            img_xml, _, _ = self._image_para(png, alt=_h2.escape(title))
            parts += [h4_xml, img_xml, caption_para(caption_for_seq(f"{seq_no}.{k}", title))]
            toc_parts.append(self._toc_line(4, label, h4_anchor, page_hint))
            self.plan.append(f"ADD {label[:70]}: {png.name}")
        self._emit(pos, "".join(parts))
        tpos = d.paras[toc_anchor_idx]["end"]
        self._emit(tpos, "".join(toc_parts))

    def _extend_fe11(self, h2_11: int) -> None:
        d = self.doc
        fe11 = next(f for f in FE_SECTIONS if f["fe"] == "FE-11")
        nxt_h2 = next((i for i in range(h2_11 + 1, len(d.paras))
                       if d.paras[i]["style"] == "Heading2"), len(d.paras))
        in_range = range(h2_11, nxt_h2)
        has_class = any(d.paras[i]["style"] == "Heading3" and "Class Diagram" in d.paras[i]["text"]
                        for i in in_range)
        have_ucs = {m.group(0) for i in in_range
                    if d.paras[i]["style"] == "Heading4"
                    for m in [UC_ID_RE.search(d.paras[i]["text"])] if m}
        want = [uc_key_from_png(png_s) for png_s, _ in fe11["seq"]]
        if has_class and all(u in have_ucs for u in want):
            self.plan.append("SKIP FE-11: already filled")
            return
        # stale note -> delete (replaced by 11.1 Class Diagram); absent on reruns
        note_idx = next((i for i in in_range
                         if d.paras[i]["style"] == "" and "has no class diagram yet" in d.paras[i]["text"]
                         and "FE-11" in d.paras[i]["text"]), None)
        if not has_class:
            png = Path(fe11["class_png"])
            h3_xml, h3_anchor = self._new_heading(3, "11.1 Class Diagram")
            img_xml, _, _ = self._image_para(png, alt="Class diagram FE-11")
            at = d.paras[note_idx]["start"] if note_idx is not None else d.paras[h2_11 + 1]["start"]
            self._emit(at,
                       h3_xml + img_xml + caption_para("Figure 11.1: Class Diagram of FE-11 Voting Management"))
            self.plan.append(f"ADD class FE-11: {png.name}")
            toc_seq = next(i for i, p in enumerate(d.paras)
                           if p["style"] == "TOC3" and self.pt(i).startswith("11.2 Sequence Diagram"))
            page_hint = self._toc_page_hint(toc_seq)
            self._emit(d.paras[toc_seq]["start"], self._toc_line(3, "11.1 Class Diagram", h3_anchor, page_hint))
        if note_idx is not None:
            self._replace_para(note_idx, "")
            self.plan.append("DELETE stale FE-11 'no class diagram' note (diagram added as 11.1)")
        # insert missing UC headings (11.2.1..11.2.7) before H4 11.2.8 (ex-76a)
        h4_8 = next((i for i in in_range
                     if d.paras[i]["style"] == "Heading4" and self.pt(i).startswith("11.2.8")), None)
        toc_8 = next((i for i, p in enumerate(d.paras)
                      if p["style"] == "TOC4" and self.pt(i).startswith("11.2.8")), None)
        if h4_8 is None or toc_8 is None:
            self.warn.append("FE-11: cannot locate 11.2.8 anchor - add missing UCs manually")
            return
        page_hint = self._toc_page_hint(toc_8)
        parts, toc_parts = [], []
        for k, (png_s, txt) in enumerate(fe11["seq"][:7], start=1):
            if uc_key_from_png(png_s) in have_ucs:
                continue
            title, problem = checked_title(png_s, txt)
            if problem and problem not in self.warn:
                self.warn.append(problem)
            label = f"11.2.{k} {title}"
            h4_xml, h4_anchor = self._new_heading(4, label)
            import html as _h3
            img_xml, _, _ = self._image_para(Path(png_s), alt=_h3.escape(title))
            parts += [h4_xml, img_xml, caption_para(caption_for_seq(f"11.2.{k}", title))]
            toc_parts.append(self._toc_line(4, label, h4_anchor, page_hint))
            self.plan.append(f"ADD {label[:70]}: {Path(png_s).name}")
        self._emit(d.paras[h4_8]["start"], "".join(parts))
        self._emit(d.paras[toc_8]["start"], "".join(toc_parts))

    IN_DOC_FES = {"FE-01", "FE-02", "FE-03", "FE-04", "FE-05", "FE-06",
                  "FE-07", "FE-11", "FE-12", "FE-13"}

    def _toc_insert_after(self, fe: dict) -> int:
        """Paragraph index after which the new TOC block goes (end of previous group)."""
        order = [f["fe"] for f in FE_SECTIONS]
        prev = order[order.index(fe["fe"]) - 1]
        while prev not in self.IN_DOC_FES:  # chained new sections share one anchor
            prev = order[order.index(prev) - 1]
        # last TOC line belonging to prev FE = last TOC2/3/4 whose label starts with its number
        prev_num = NEW_POS[prev]
        old_num = {"FE-11": 8, "FE-12": 9, "FE-13": 10}.get(prev, prev_num)
        idx = max(i for i, p in enumerate(self.doc.paras)
                  if p["style"] in ("TOC2", "TOC3", "TOC4")
                  and re.match(rf"^{prev_num}\.|^{old_num}\.", self.pt(i).split('\t')[0]))
        return idx

    def _toc_page_hint(self, idx: int) -> str:
        m = re.search(r"<w:t>(\d+)</w:t>\s*</w:r>\s*</w:hyperlink>\s*</w:p>",
                      self.doc.paras[idx]["xml"])
        if m:
            return m.group(1)
        m = re.search(r"<w:t>(\d+)</w:t>", self.doc.paras[idx]["xml"])
        return m.group(1) if m else "?"

    # -- apply: single pass over the ORIGINAL string, so positions stay valid
    def apply(self) -> dict[str, bytes]:
        out = dict(self.files)
        out.update(self.new_files)
        orig = out["word/document.xml"].decode("utf8")
        # (pos, priority, sub-order, kind, payload):
        # inserts (pri 0) before a replacement starting at the same pos;
        # same-pos inserts keep emission order, so emit in document order.
        events: list[tuple[int, int, int, str, object]] = []
        for (s, e), rep in self.replacements.items():
            events.append((s, 1, 0, "rep", (e, rep)))
        for pos, seq, blk in self.inserts:
            events.append((pos, 0, seq, "ins", blk))
        events.sort(key=lambda ev: (ev[0], ev[1], ev[2]))
        chunks: list[str] = []
        cursor = 0
        for pos, _, _, kind, payload in events:
            chunks.append(orig[cursor:pos])
            cursor = pos
            if kind == "ins":
                chunks.append(payload)  # type: ignore[arg-type]
            else:
                end, rep = payload  # type: ignore[misc]
                chunks.append(rep)
                cursor = max(cursor, end)
        chunks.append(orig[cursor:])
        out["word/document.xml"] = "".join(chunks).encode("utf8")
        rels = out["word/_rels/document.xml.rels"].decode("utf8")
        rels = rels.replace("</Relationships>", "".join(self.rels_add) + "</Relationships>")
        out["word/_rels/document.xml.rels"] = rels.encode("utf8")
        return out


def write_zip(files: dict[str, bytes], target: Path) -> None:
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for name, data in files.items():
            z.writestr(name, data)


def main() -> int:
    ap = argparse.ArgumentParser(description="Fill Report 4 section II from repo diagrams.")
    ap.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    ap.add_argument("--output", type=Path, help="write modified copy here (test without touching source)")
    ap.add_argument("--write", action="store_true", help="modify source in place (keeps .bak)")
    args = ap.parse_args()

    filler = Filler(args.source)
    filler.run()

    print("== PLAN ==")
    for line in filler.plan:
        print(" ", line)
    if filler.warn:
        print("== WARNINGS (Word finishing pass) ==")
        for line in dict.fromkeys(filler.warn):
            print("  !", line)

    if not (args.output or args.write):
        print("\nDry run only. Re-run with --output <file> or --write to apply.")
        return 0

    out_files = filler.apply()
    target = args.output if args.output else args.source
    if args.write and not args.output:
        bak = args.source.with_suffix(args.source.suffix + ".bak")
        bak.write_bytes(args.source.read_bytes())
        print(f"Backup: {bak}")
    write_zip(out_files, target)
    print(f"Wrote {target}")

    errors, warnings = check_docx.check(target)
    print(("FAIL " if errors else "OK   ") + target.name)
    for e in errors:
        print("     -", e)
    for w in warnings:
        print("     !", w)
    if errors:
        return 1
    # self-check: every manifest UC reachable
    doc = Doc(out_files["word/document.xml"].decode("utf8"))
    texts = [p["text"] for p in doc.paras]
    missing = []
    for fe in FE_SECTIONS:
        for png_s, txt in fe["seq"]:
            uc = uc_key_from_png(png_s)
            if uc in fe.get("frozen", set()):
                continue
            if not any(uc in t for t in texts):
                missing.append(uc)
    if missing:
        print("SELF-CHECK FAIL: headings missing for", missing)
        return 1
    # self-check: every image group in sections I-II is followed by a Figure caption
    caps_missing = []
    op = out_files["word/document.xml"].decode("utf8")
    oparas = [m.group(0) for m in P_RE.finditer(op)]
    in_range = False
    group_open = False
    for x in oparas:
        st = para_style(x)
        tx = para_text(x)
        if st == "Heading1":
            in_range = tx in ("I. High Level Design", "II. Detailed Code Design")
            if group_open:
                group_open = False
        if not in_range:
            continue
        if st in ("Heading2", "Heading3", "Heading4"):
            if group_open:
                caps_missing.append("caption missing before " + tx[:50])
                group_open = False
            continue
        if "<w:drawing>" in x:
            group_open = True
        elif tx != "":
            if group_open:
                if not CAPTION_RE.match(tx):
                    caps_missing.append("caption missing: " + tx[:50])
                group_open = False
    if group_open:
        caps_missing.append("caption missing at end of section")
    if caps_missing:
        print("SELF-CHECK FAIL:", caps_missing[:5])
        return 1
    print(f"SELF-CHECK OK: {len(texts)} paragraphs, "
          f"{sum('w:drawing' in p['xml'] for p in doc.paras)} images, "
          f"{len(out_files)} package files.")
    print("Next (Word pass): Update Field on the TOC, add a Change Log row, "
          "move wide diagrams to landscape if needed, split tall images.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
