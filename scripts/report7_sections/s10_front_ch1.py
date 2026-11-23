"""Cover members, Acknowledgements, Definitions and Acronyms, Chapter 1 (sections 1-6)."""

from __future__ import annotations

from report7_lib import Report, tnr
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

MEMBERS = [
    ("Tran Viet Hai", "Team Leader, Frontend Lead"),
    ("Pham Gia Khanh", "Business Analyst, UI/UX"),
    ("Hoang Duc Loc", "Backend Lead, Architecture"),
    ("Nguyen Quang Huy", "Backend, Frontend"),
    ("Nguyen Truong Huy", "Test Lead, Quality Assurance"),
]

ACRONYMS = [
    ("IMDMS", "Internal Meeting and Document Management System - the project code of TrustCabinet"),
    ("AI", "Artificial Intelligence"),
    ("API", "Application Programming Interface"),
    ("ASR", "Automatic Speech Recognition - converts speech to text"),
    ("BA", "Business Analyst"),
    ("BF", "Business Flow (BF-01 to BF-04, Report 3)"),
    ("BR", "Business Rule"),
    ("Chair", "The person who presides over a meeting, starts and ends it, and approves its minutes"),
    ("Diarization", "Splitting a recording into segments by speaker (\"Speaker 1, 2, 3...\")"),
    ("ERD", "Entity Relationship Diagram"),
    ("FE", "Feature (FE-01 to FE-15 in Report 1); also Front-End in technical text"),
    ("JWT", "JSON Web Token - the signed token used for sign-in sessions"),
    ("LI / EX", "Limitation / Exclusion - an item deliberately out of scope (Report 1, Section V.2)"),
    ("LiveKit", "Open-source SFU used for the online meeting room"),
    ("MSG", "Message code, e.g. MSG07 - one global catalogue of messages (Report 3)"),
    ("NFR", "Non-Functional Requirement"),
    ("PO", "Product Owner (Dinh Van Toi, TrustSoft)"),
    ("RBAC", "Role-Based Access Control"),
    ("SFU", "Selective Forwarding Unit - a media server that relays each participant's stream to the others"),
    ("SRS", "Software Requirement Specification (Report 3)"),
    ("UC", "Use Case"),
    ("VAD", "Voice Activity Detection - finds the parts of a recording that contain speech"),
    ("Voice ID", "Optional recognition of a speaker from a voice sample given with consent; the sample is stored only as an encrypted vector"),
]


def _cell_lines(cell, lines):
    cell.text = ""
    first = True
    for line in lines:
        p = cell.paragraphs[0] if first else cell.add_paragraph()
        first = False
        tnr(p.add_run(line))


def _page_break():
    """Detached empty paragraph carrying a page break.

    Restores the template convention (each chapter starts on a new page): the
    template had an explicit page break before Chapter 2 that replace_until()
    removes together with the template boxes, so the filler re-adds it as its
    last element. This keeps the Chapter 2 heading off the table's page and
    avoids a half-filled page when the Foundations table splits.
    """
    p = OxmlElement("w:p")
    run = OxmlElement("w:r")
    br = OxmlElement("w:br")
    br.set(qn("w:type"), "page")
    run.append(br)
    p.append(run)
    return p


def fill_cover(r: Report):
    for t in r.doc.tables[:4]:
        for row in t.rows:
            label = row.cells[0].text.strip()
            if label == "Group Members":
                _cell_lines(row.cells[1], [f"{n} - {role}" for n, role in MEMBERS]
                            + ["TODO(team): student code (RollNo) of each member is not in the repo."])
            elif label == "Supervisor":
                _cell_lines(row.cells[1], ["Nguyen Quang Hung"])
            elif label == "Ext Supervisor":
                _cell_lines(row.cells[1], ["Dinh Van Toi - Product Owner, TrustSoft Company"])
            elif label == "Capstone Project code":
                _cell_lines(row.cells[1], ["IMDMS"])


def fill_front(r: Report):
    h = r.find_heading("Acknowledgements")
    r.replace_section(h, [
        r.text("We would like to thank our supervisor, Mr. Nguyen Quang Hung, for the guidance and the timely "
               "feedback he gave on every report version during the fifteen weeks of the capstone."),
        r.text("We thank Mr. Dinh Van Toi, the Product Owner from TrustSoft Company, who defined the business "
               "problem, answered our questions about meeting practice and confirmed the scope and the "
               "acceptance of each iteration. We also thank the review council, whose comments in October 2026 "
               "led us to bring Voice ID into the project and to run speech recognition on our own server."),
        r.text("Finally, we thank our lecturers at FPT University for the knowledge that made this work "
               "possible, and our families and friends for their support."),
    ])
    h = r.find_heading("Definition and Acronyms")
    r.replace_section(h, [r.table(["Acronym", "Definition"], ACRONYMS, [1.3, 5.0], size=9)])


ASIS = [
    ("Scheduling is done in a calendar; links and changes go through chat",
     "Participants miss changes; there is no approval step and no confidentiality level on a meeting"),
    ("Agendas, specifications and reports are shared as chat messages, links or folders",
     "Time spent searching for the latest file and checking what was reviewed"),
    ("A note-taker writes notes during the meeting",
     "The note-taker takes part less; details of decisions and owners are lost"),
    ("Votes are verbal or \"agree\" in chat",
     "No structured record linked to the meeting; a decision cannot be proved when questioned"),
    ("Minutes are written afterwards from notes and shared by link",
     "Delay of one or two days; the format differs from one meeting to the next"),
    ("Access to documents is per link or per folder",
     "No consistent rule on who may see a confidential meeting, its audio and its minutes"),
]

TOBE = [
    ("Organizer creates a meeting and submits it to an assigned approver; publication sends invitations",
     "Changed - one controlled flow with a recorded status history (BF-02)"),
    ("Documents come from the personal library or are uploaded into the meeting, visible by meeting permission",
     "Automated - one place per meeting, no search through chats (BF-02)"),
    ("The meeting is held in the built-in online room; the Chair controls the agenda and Register to Speak",
     "Changed - no external conferencing service (BF-03)"),
    ("The Secretary records attendance; the audio is recorded and transcribed in Vietnamese with speaker labels",
     "Automated - nobody has to type everything during the meeting (BF-03, BF-04)"),
    ("The Chair opens a vote in the meeting and records a conclusion",
     "Changed - structured result bound to the meeting (BF-03)"),
    ("An AI draft of the minutes from the fixed template; the Secretary edits and sends it; the Chair approves or returns it",
     "Automated and controlled - issued minutes are locked and searchable by permission (BF-04)"),
    ("Roles and the capability matrix decide what each user sees; the server checks every request",
     "Eliminated - ad-hoc sharing by link (BF-01)"),
]


def fill_ch1(r: Report):
    h1 = r.find_heading("1. Project Overview")
    h2 = r.find_heading("2. Rationale/Background")
    h3 = r.find_heading("3. Business Process Improvement")
    h4 = r.find_heading("4. Related / Comparable Products")
    h5 = r.find_heading("5. Technologies and Tools")
    h6 = r.find_heading("6. Theoretical Foundations")
    ch2 = r.find_heading("Chapter 2 - Project Management Plan")

    # 1
    r.replace_until(h1, h2, [
        r.table(["Item", "Content"], [
            ("English name", "TrustCabinet - AI-assisted Paperless Meeting System (Internal Meeting and Document Management System for TrustSoft Company)"),
            ("Vietnamese name", "TrustCabinet - Hệ thống quản lý cuộc họp và tài liệu nội bộ cho Công ty TrustSoft"),
            ("Project code / Group", "IMDMS / SEP490-G116"),
            ("Software type", "Web application, responsive (no native mobile app)"),
            ("Customer", "TrustSoft Company, Hanoi - Product Owner: Dinh Van Toi"),
            ("Supervisor", "Nguyen Quang Hung"),
            ("Duration", "15 weeks, 14/09/2026 - 27/12/2026 (Report 2)"),
            ("Team", "; ".join(f"{n} ({role})" for n, role in MEMBERS)),
        ], [1.5, 4.8]),
        r.text("TrustCabinet is a centralised, internally hosted system that takes a meeting from preparation to "
               "issued minutes in one place. Its main capabilities are:"),
        *r.bullets([
            "Identity and access: email/password or Google sign-in, user and role administration, and a role matrix that can be edited at run time (FE-01 to FE-03).",
            "Meeting operations: scheduling with approval or cancellation, agenda, participants and attendance, a personal document library and meeting documents with comments (FE-04 to FE-07).",
            "An online meeting room hosted by the system itself, with audio, video, screen sharing, chat and Register to Speak (FE-08).",
            "Meeting intelligence: audio recording, Vietnamese transcription with speaker attribution, optional Voice ID, and an AI draft of the minutes from one fixed template (FE-09, FE-10).",
            "Governance: in-meeting voting with recorded Chair conclusions, and minutes that are sent for approval, returned for correction or issued (FE-11, FE-12).",
            "Communication and visibility: in-app and email notifications, and reports and statistics (FE-13, FE-15).",
        ]),
        r.text("Out of scope by decision of the PO: signing the minutes (even a simulated signature), the "
               "Secretary's list of document comments, the personal dashboard and meeting workspace, locking an "
               "account after wrong passwords, task and action-item tracking, and integrations with external "
               "systems (Report 1, Section V.2). The business domain is modelled on AI-eCabinet (SmartMeet), but "
               "TrustCabinet is an independent .NET implementation sized for a capstone."),
    ])

    # 2
    r.replace_until(h2, h3, [
        r.text("TrustSoft is a technology company in Hanoi whose project teams hold three to five internal "
               "meetings per week: sprint planning, client syncs and internal reviews. Today these meetings are "
               "run through a patchwork of tools - Google Calendar for scheduling, Zalo or Teams for links and "
               "updates, and Google Docs, paper or personal notes for the record (Report 1, Section II)."),
        r.text("The problem. Agendas and supporting files travel through chat messages and shared folders, so "
               "participants search several places for the latest version. The note-taker takes part less in the "
               "discussion. Votes are given by chat replies such as \"agree\" and leave no structured record. "
               "Minutes are written by hand, reach the team a day or two late as long unstructured text, and are "
               "not linked to the documents, decisions and votes of the meeting."),
        r.text("Why existing solutions are not enough. The AI meeting assistants we reviewed (Fellow, "
               "Fireflies.ai, ClickUp) attach to an outside conferencing service and process confidential "
               "audio on third-party clouds. Combining separate tools (Asana + Zoom + an AI service) leaves data "
               "silos and manual synchronisation. Document-governance platforms handle approval and access "
               "control but treat a meeting only as a calendar entry. Report 1 consolidates these as six gaps, "
               "GAP-01 to GAP-06, and every feature of the project closes at least one of them (see Section 4)."),
        r.text("Trigger. The project is a capstone (SEP490) done for TrustSoft as the customer. Speech "
               "recognition and summarisation have become practical enough to connect what is prepared, said, "
               "decided and written in one workflow. The review council later asked for Voice ID (23/10/2026), "
               "and the PO chose to run the speech and AI models on the team's own infrastructure."),
    ])

    # 3
    r.replace_until(h3, h4, [
        r.heading("3.1 Current (As-Is) Process Analysis", 3),
        r.text("The as-is process is informal and spread over several tools, so there is no single model to draw. "
               "The table lists each step as it happens at TrustSoft today and the impact observed "
               "(Report 1, Section II)."),
        r.table(["As-is step", "Pain point and impact"], ASIS, [3.1, 3.2]),
        r.heading("3.2 Feasibility Study", 3),
        r.table(["Dimension", "Finding"], [
            ("Technical", "Feasible with open-source parts. A proof of concept for the online room and Vietnamese transcription was done in the planning week (Report 2, WP 0.2). The room runs on a self-hosted LiveKit SFU (ADR-0010); transcription and speaker separation run on CPU in a small in-house worker (ADR-0026); the AI draft of the minutes runs on a local language model behind an OpenAI-compatible API (ADR-0054). The main technical risks were the first real-time media work (R01) and Vietnamese accuracy (R02)."),
            ("Economic", "No budget; only free, open-source or student-licensed tools are used (Report 2, constraint 2). The shared test server is one small AWS Lightsail instance paid from the team's student credit (ADR-0003, ADR-0015); file storage is on Cloudflare R2 (ADR-0024). The cost to the customer is hosting only, with no per-user or per-meeting licence, unlike the subscription tools in Section 4."),
            ("Operational", "TrustSoft already holds the meetings this system supports, and its Product Owner confirmed the scope and accepted every iteration. Staff use a web browser and sign in with email or Google, so nothing is installed. Risks: speech recognition is less accurate than large cloud models, so the transcript is always marked as machine-made and reviewed by the Secretary; recording and voice data are personal data, handled under explicit consent and deletion rules (Law 91/2025/QH15, ADR-0027)."),
        ], [1.1, 5.2]),
        r.heading("3.3 Proposed (To-Be) Process Design", 3),
        r.text("The to-be process is modelled as four business flows, BF-01 to BF-04, as swimlane diagrams in "
               "Chapter 3, Section 1.2 (Figures 3.2 to 3.5): access and administration; meeting preparation and "
               "approval; execution and decision making; post-meeting processing and formal records. The table "
               "states what changes relative to Section 3.1."),
        r.table(["To-be step", "Change relative to as-is"], TOBE, [3.6, 2.7]),
    ])

    # 4
    r.replace_until(h4, h5, [
        r.text("Report 1 reviewed four products; we add AI-eCabinet (SmartMeet), the reference system that the "
               "business domain is modelled on. Fellow, Fireflies.ai and ClickUp are cloud SaaS; the "
               "Asana + Zoom + AI Studio combination is a do-it-yourself chain."),
        r.text("Fellow (https://fellow.ai) is an AI meeting assistant for teams that need AI-assisted capture, "
               "documentation and follow-up. Its main features are meeting recording (live or uploaded audio and "
               "video), transcription, editable AI-generated summaries with key-decision extraction, "
               "collaborative meeting notes and a searchable meeting history (Report 1, Section III.1). Its "
               "strength for our problem is a mature capture-and-recap workflow whose summaries can be reviewed "
               "after the meeting. Its weakness is governance: voting is documented through reactions and "
               "informal pulse checks rather than a structured voting session with a recorded outcome, so it "
               "does not close GAP-04 (structured record) or GAP-05 (formal governance)."),
        r.text("Fireflies.ai (https://fireflies.ai) targets remote teams, sales staff and project managers with "
               "automatic recording and transcription across Zoom, Teams and Google Meet, AI summaries, sentiment "
               "analysis, conversation intelligence and CRM/task integrations (Report 1, Section III.2). Its "
               "strengths are multi-platform integration, transcription accuracy and conversational search. "
               "Against our problem it has two drawbacks: confidential audio is processed on a third-party cloud "
               "with a recurring subscription (GAP-01), and it offers transcription and analytics rather than a "
               "complete self-hosted document-governance and voting workflow with approval, access control and "
               "formal minutes (GAP-05, GAP-06)."),
        r.text("ClickUp (https://clickup.com) is an all-in-one productivity platform for cross-functional and "
               "agile teams, combining task management, Docs and AI meeting tools: ClickUp Brain transcript "
               "analysis, automatic follow-up tasks with assignees and deadlines, an AI Notetaker and an Action "
               "Item Extractor agent (Report 1, Section III.4). Its strength is a unified task-and-document "
               "environment with strong AI automation. Its drawbacks for TrustSoft are platform complexity, a "
               "subscription cost model, no self-hosted deployment (GAP-01), no native signing of minutes and no "
               "meeting governance tailored to a formal internal meeting record (GAP-05, GAP-06); moreover its "
               "task tracking is exactly what TrustCabinet excludes by LI-12."),
        r.text("Asana + Zoom + AI Studio (https://asana.com / https://zoom.us) is not one product but a chain "
               "assembled by tech-savvy teams: video conferencing in Zoom, custom script processing in AI "
               "Studio and task tracking in Asana (Report 1, Section III.3). Each tool is powerful on its own, "
               "which is its strength. Its drawbacks are structural: data must be bridged by hand across "
               "platforms, the user experience is fragmented, several subscriptions add up, and there is no "
               "integrated security mechanism such as signing (GAP-03, GAP-01, GAP-05). It is the clearest "
               "example of the silo problem TrustCabinet removes by holding the meeting, its documents, its "
               "votes and its minutes in one record."),
        r.text("AI-eCabinet (SmartMeet) is the internal reference system the business domain is modelled on, "
               "surveyed from its internal demo and reference source on 18/09/2026 "
               "(docs/00-overview/04-so-sanh-ai-ecabinet.md). It is a Go modular monolith (React + TypeScript + "
               "Vite front end, OpenAPI as the single contract source) with OpenFGA as the single decision "
               "point, Postgres plus NATS and a Python AI worker (ASR, diarization, voiceprint, summarisation), "
               "headless Zitadel sign-in, PKCS#11/TPM-backed security with a hash-chained audit log, deployable "
               "in an isolated network. Its strength for our problem is precisely what the SaaS tools lack: "
               "formal document governance with approval, role-based access and signed minutes. Its limits as a "
               "model are that it provides no video meeting room of its own, and that OpenFGA, Zitadel and "
               "hardware-backed signing are too heavy for a one-semester capstone - which is why TrustCabinet "
               "keeps its vocabulary and meeting life cycle but implements its own LiveKit room, run-time role "
               "matrix and email/Google sign-in, and drops minutes signing from scope (PO decision 14/11/2026)."),
        r.table(["Capability", "Fellow", "Fireflies.ai", "ClickUp", "Asana + Zoom + AI", "AI-eCabinet", "TrustCabinet"], [
            ("Meeting room of its own", "No", "No", "No", "Zoom", "No", "Yes (LiveKit)"),
            ("Transcript and AI summary", "Yes", "Yes", "Yes", "Via AI Studio scripts", "Yes", "Yes, Vietnamese, local"),
            ("Self-hosted / data in-house", "No", "No", "No", "No", "Yes, air-gapped", "Yes, single server"),
            ("Meeting approval and roles", "No", "No", "Limited", "No",
             "TODO(compare): approval/roles in AI-eCabinet not confirmed in repo sources", "Yes"),
            ("Structured voting and Chair conclusion", "Reactions only", "No", "No", "No", "Yes", "Yes"),
            ("Formal minutes, approval and issue", "Recaps", "Summaries", "Docs", "Manual", "Yes, with signing", "Yes (no signing)"),
            ("Task / action-item tracking", "Notes only", "Via integrations", "Yes", "Asana",
             "TODO(compare): task tracking in AI-eCabinet not confirmed in repo sources", "No (out of scope)"),
            ("Cost model", "Subscription", "Subscription", "Subscription", "Several subscriptions",
             "TODO(compare): AI-eCabinet cost/licensing not documented in repo", "Hosting only"),
        ], [1.3, 0.75, 0.8, 0.75, 0.9, 0.9, 0.9], size=8),
        r.text("Source: Report 1, Section III (survey of September 2026; commercial cells may have changed), "
               "docs/00-overview/04-so-sanh-ai-ecabinet.md (AI-eCabinet column), and Report 1 Sections IV-V "
               "plus ADRs (TrustCabinet column). Cells marked TODO could not be verified from repo sources."),
        r.text("AI-eCabinet vs TrustCabinet. TrustCabinet keeps AI-eCabinet's vocabulary and meeting life "
               "cycle (a meeting is one sitting; voting happens inside the meeting; the Chair records Adopted / "
               "Not adopted / Postponed) and the layout of its screens. It differs on purpose:"),
        r.table(["Aspect", "AI-eCabinet", "TrustCabinet", "Reason"], [
            ("Backend", "Go modular monolith", "ASP.NET Core 10, four projects (Clean Architecture)", "Course requirement; team knows C#"),
            ("Authorisation", "OpenFGA", "Role matrix with capabilities, editable at run time", "OpenFGA is too heavy for one semester"),
            ("Calls", "No video room", "Own online room on LiveKit SFU", "Main difference (ADR-0010)"),
            ("Sign-in", "Zitadel", "Email + password or Google", "No separate identity provider (ADR-0004)"),
            ("Signing", "PKCS#11 / TPM", "Dropped from scope", "No HSM or USB token; PO decision 14/11/2026"),
            ("Deployment", "Air-gapped capable", "One server, Docker Compose", "Capstone scope"),
        ], [0.9, 1.3, 2.1, 2.0], size=8),
        r.text("The gap TrustCabinet fills: no reviewed product holds the meeting room, documents, voting, "
               "Vietnamese transcription and approved minutes together on infrastructure the customer controls "
               "(GAP-01 to GAP-06). AI-eCabinet's source code is private and unlicensed; it was read to "
               "understand the domain and no file was copied."),
    ])

    # 5
    r.replace_until(h5, h6, [
        r.text("Each choice is recorded as an Architecture Decision Record in docs/05-decisions."),
        r.heading("5.1 Back end and data", 3),
        r.table(["Technology", "Used for", "Why / alternative considered"], [
            ("ASP.NET Core 10 (Minimal API), C#", "REST API, SignalR hubs, background workers", "Course requirement and team skill. Alternative: Go, as in AI-eCabinet."),
            ("EF Core 10 + Npgsql; PostgreSQL 17", "Code-first model, migrations, all business data", "Free, fits a 4 GB server. Alternative: SQL Server, rejected for RAM and image size (ADR-0003)."),
            ("Redis 8", "Refresh tokens for sign-in, expiring by TTL", "Losing it only forces a new sign-in. Alternative: a database table."),
            ("JWT bearer + Google sign-in", "Sessions and optional Google sign-in", "No separate identity provider (ADR-0004, ADR-0009)."),
            ("S3-compatible storage: SeaweedFS (dev), Cloudflare R2 (test server)", "Document, audio and file bytes", "One S3 API for both; every byte passes through the backend (ADR-0024)."),
            ("MailKit and Resend", "Invitation and reset emails, delivery results", "Delivery status is visible, unlike personal SMTP (ADR-0031)."),
            ("QuestPDF, PdfPig, AngleSharp", "Minutes export, PDF page counting, HTML minutes handling", "In-process libraries; no external service."),
        ], [1.9, 2.0, 2.4], size=8),
        r.heading("5.2 Front end", 3),
        r.table(["Technology", "Used for", "Why / alternative considered"], [
            ("React 19, TypeScript, Vite", "Single-page app, split by feature", "Common stack, fast build. Alternative: Blazor."),
            ("React Router 7", "Route table with capability-gated routes", "A restricted route renders the same 404 as an unknown one."),
            ("@microsoft/signalr", "Live notifications and meeting state", "Same stack as the server hub."),
            ("livekit-client, @livekit/components-react", "Online room in the browser", "Matches the self-hosted SFU."),
            ("Lucide icons, Be Vietnam Pro", "Icons and typeface", "Light, consistent look (ADR-0012, ADR-0043)."),
        ], [2.2, 1.9, 2.2], size=8),
        r.heading("5.3 Real-time media, speech and AI", 3),
        r.table(["Technology", "Used for", "Why / alternative considered"], [
            ("LiveKit server v1.13 (SFU, self-hosted)", "Audio, video, screen sharing in the room", "Proven, Apache-2.0, includes TURN. Alternative: peer-to-peer mesh, which does not scale (ADR-0010)."),
            ("Python worker with sherpa-onnx (CPU)", "Vietnamese speech to text, speaker separation, voice vectors", "Audio never leaves the team's server. Alternative: Whisper / PhoWhisper, too heavy without a GPU (ADR-0026)."),
            ("Gipformer 1.5 (RNN-T), 3D-Speaker ERes2Net, Silero VAD, viBERT-capu", "Transcription, speaker embeddings, silence filter, punctuation", "Open licences (MIT, Apache-2.0, CC BY-SA); hashes checked at start (workers/speech)."),
            ("Local LLM through an OpenAI-compatible API (default Ollama, qwen2.5:7b-instruct)", "AI draft of the minutes", "Model and machine change by configuration only; a fallback machine exists (ADR-0054, ADR-0063)."),
        ], [2.1, 1.9, 2.3], size=8),
        r.heading("5.4 Delivery and quality tools", 3),
        r.table(["Tool", "Used for"], [
            ("Git, GitHub, GitHub Actions", "Version control, pull requests, CI that builds the backend with warnings as errors and runs tests"),
            ("Docker Compose, Caddy, AWS Lightsail", "Shared test server, deployed automatically with a database backup before migration (ADR-0015, ADR-0047)"),
            ("xUnit with a real PostgreSQL; node --test; browser runner in tests/qa", "Integration and use-case tests, front-end unit tests, browser acceptance runs"),
            ("Jira (project SCRUM), Google Workspace, draw.io", "Backlog and tracking, documents, diagrams"),
        ], [2.3, 4.0], size=8),
    ])

    # 6
    r.replace_until(h6, ch2, [
        r.text("Only what is needed to read Chapter 3 and Chapter 4."),
        r.table(["Foundation", "Meaning and use in TrustCabinet"], [
            ("Clean Architecture (four projects)", "Dependencies point one way: Web -> Application -> Domain, and Infrastructure -> Application -> Domain. Domain holds entities and rules and references nothing; Application holds use-case services and ports; Infrastructure implements the ports. Tests enforce the direction (ADR-0064)."),
            ("Role-based access control with capabilities", "A user has a role; a role is a set of capabilities that an administrator can edit while the system runs. The UI only hides what a user may not use; every endpoint re-checks on the server and answers 404 for a record the caller may not see, so its existence is not revealed (ADR-0007, ADR-0008)."),
            ("Meeting life cycle as a state machine", "A meeting moves through Draft, WaitingApproval, Approved or Rejected, Published, InProgress, Ended and Cancelled; every change is written to an append-only status history kept as evidence."),
            ("Selective Forwarding Unit (SFU)", "Each participant sends one stream to a server that forwards it to the others. Upload cost stays flat per participant, unlike a peer-to-peer mesh where every client sends to every other (ADR-0010)."),
            ("Speech pipeline", "A recording is cut by voice activity detection, transcribed by a transducer model (RNN-T), punctuated by a small BERT model, and each speech segment gets a 512-dimension speaker vector. Segments with similar vectors are grouped into \"Speaker 1, 2, 3\" (diarization)."),
            ("Voice ID by embedding similarity", "A person who consents records a voice sample; only its encrypted vector is kept (AES-256-GCM) and consent can be withdrawn. A segment is named when its vector matches the stored one closely enough; a close match shared by two people is not named automatically (ADR-0027, ADR-0045, ADR-0050)."),
            ("Drafting with a language model", "The model receives the meeting facts, the attendance, the agenda outcomes and the reviewed transcript with a fixed template; the output is only a draft that the Secretary edits and the Chair approves (ADR-0054)."),
            ("Personal data protection", "Recordings and voice vectors are personal data, and a voice used to recognise a person is sensitive biometric data under Law 91/2025/QH15. The system stores them only with consent, allows withdrawal and erasure, and keeps the models in-house."),
        ], [1.7, 4.6], size=8),
        _page_break(),
    ])


def fill(r: Report):
    fill_cover(r)
    fill_front(r)
    fill_ch1(r)
