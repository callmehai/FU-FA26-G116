# Report 6 – Software User Guides

> **GENERATED — DO NOT EDIT.** Snapshot từ DOCX local. Sửa [`source/Report-6_Software%20User%20Guides.docx`](source/Report-6_Software%20User%20Guides.docx) rồi chạy `python scripts/sync_reports.py --source docs/08-reports/source/<tên>.docx --report 6`. Sửa tay file này sẽ mất ở lần sync sau. Chi tiết: [08-reports/README.md](README.md).

Capstone Project Report

Report 6 – Software User Guides

– Hanoi, Nov 2026 –

## Change Log

| Date | A*<br>M, D | In charge | Change Description |
| --- | --- | --- | --- |
| 21/11/2026 | A | HuyNQ | Initial release: deliverable package, installation guides (development machine and test server) and the user manual for the Administrator, Secretary, Chair and Member roles, with screenshots from the test server. |

*A - Added M - Modified D - Deleted

## I. Installation Guides

This part lists what is handed over with TrustCabinet and how to install it, first on a developer's machine and then on a server. TrustCabinet is one web application: an ASP.NET Core 10 back end that also serves the React front end, a PostgreSQL database, and a few supporting services that run in Docker containers.

### 1. Deliverable Package

| No. | Deliverable Item | Sub-items | Type | Version |
| --- | --- | --- | --- | --- |
|  | Code Package |  |  |  |
| 1 | Source code | TrustCabinet.zip — backend/, frontend/, workers/speech/, deploy/, tests/, Dockerfile, docker-compose.yml, TrustCabinet.sln | zip | 1.0 |
| 2 | Application image | trustcabinet-app (back end + built front end), built from Dockerfile | Docker image | 1.0 |
| 3 | Speech worker image | trustcabinet-speech (Vietnamese speech-to-text and Voice ID), built from workers/speech | Docker image | 1.0 |
|  | Database |  |  |  |
| 4 | Database migrations | backend/TrustCabinet.Infrastructure/Migrations (EF Core, code-first) | C# source | 1.0 |
| 5 | Database script | trustcabinet_schema.sql, generated with dotnet ef migrations script | sql | 1.0 |
|  | Documents |  |  |  |
| 6 | Project Introduction | Report_1_Project_Introduction.docx | docx | 1.0 |
| 7 | Project Management Plan | Report_2_Project_Management_Plan.docx | docx | 1.0 |
| 8 | Software Requirement Specification | Report_3_Software_Requirement_Specification.docx | docx | 1.0 |
| 9 | Software Design Specification | Report-4_Software Design Specification.docx | docx | 1.0 |
| 10 | Test Documentation | Report-5.0_Test Documentation.docx | docx | 1.0 |
| 11 | Unit Test | Report-5.1_Unit Test.xlsx | xlsx | 1.0 |
| 12 | Integration Test | Report-5.2_Integration Test.xlsx, Report-5.2.2_Integration Test_API Test.xlsx | xlsx | 1.0 |
| 13 | System Test | Report-5.3_System Test_FRs.xlsx, Report-5.4_System Test_NFRs.xlsx | xlsx | 1.0 |
| 14 | Acceptance Test | Report-5.5_Acceptance Test Scripts.xlsx | xlsx | 1.0 |
| 15 | Software User Guides | Report-6_Software User Guides.docx (this document) | docx | 1.0 |
| 16 | Final Project Document | Report-7_Final Project Document.docx | docx | 1.0 |
| 17 | Presentation slides | TrustCabinet_Report.pptx | pptx | 1.0 |

### 2. System Requirements

#### 2.1 Server

The test server is an AWS Lightsail instance in Singapore. Any Linux host with Docker gives the same result.

| Item | Requirement |
| --- | --- |
| Operating system | Ubuntu 24.04 LTS (64-bit) |
| CPU / RAM | 2 vCPU and 4 GB RAM to run the speech worker on the same host; 1 GB RAM is enough for the web application and the online room without speech-to-text |
| Storage | At least 40 GB SSD, plus 4 GB swap |
| Network | A static public IP address and a domain name pointing to it (an A record) |
| Open ports | 22/TCP (SSH), 80/TCP and 443/TCP (web, certificates), 7881/TCP and 7882/UDP (audio and video of the online room) |
| Software | Docker Engine with the Compose plugin, Git |

The containers that deploy/docker-compose.yml starts:

| Container | Image | Purpose |
| --- | --- | --- |
| app | trustcabinet-app | ASP.NET Core 10 back end; serves the API, the SignalR hubs and the React front end |
| postgres | postgres:17-alpine | Database |
| redis | redis:8-alpine | Shared state for the online room and notifications |
| livekit | livekit/livekit-server:v1.13.7 | Media server (SFU) for the online meeting room |
| caddy | caddy:2-alpine | Reverse proxy, automatic HTTPS certificates, Content-Security-Policy |
| speech (optional) | trustcabinet-speech | Vietnamese speech-to-text, speaker attribution and Voice ID |
| files (optional) | chrislusf/seaweedfs:3.97 | Self-hosted S3 storage for documents, instead of Cloudflare R2 |

#### 2.2 Development Machine

| Item | Requirement |
| --- | --- |
| Operating system | Windows 10/11, macOS or Linux; 8 GB RAM (16 GB to run the speech worker locally) |
| .NET SDK | 10.0.401 (pinned by global.json; newer 10.0.x patch versions are accepted) |
| Node.js | 22 LTS with npm (the version the Dockerfile builds with) |
| Docker | Docker Desktop (Windows, macOS) or Docker Engine (Linux) |
| Git | Any recent version |
| Editor | Visual Studio Code, JetBrains Rider or Visual Studio 2022+ |
| Python (optional) | 3.11+ for the speech worker tests and the report scripts |

#### 2.3 External Services

| Service | Used for | Required? |
| --- | --- | --- |
| Google Cloud Console (OAuth client) | The "Tiếp tục sử dụng dịch vụ bằng Google" (Continue with Google) sign-in | Optional — email and password sign-in works without it |
| Resend, or an SMTP server | Invitation, notification and password-reset emails | Optional — without it no email is sent |
| Cloudflare R2, or the self-hosted SeaweedFS | Storing the bytes of uploaded documents and minutes | Needed for documents; without it uploads answer MSG166 |
| Ollama (qwen2.5:7b-instruct) or an OpenAI-compatible API | Writing the AI draft of the minutes | Optional — the rest of the minutes work without it |
| Tailscale | Reaching a speech or AI machine of the team from the server without opening ports | Optional |
| GitHub Actions | Continuous integration and automatic deployment of the main and app branches | Optional |

#### 2.4 End-user Device

- A recent version of Google Chrome, Microsoft Edge, Mozilla Firefox or Safari.

- A microphone and a camera to speak and be seen in the online room (optional — a participant can join to listen only).

- A network that allows UDP port 7882 or TCP port 7881 to the server; some company or school Wi-Fi networks block both, in which case the online room does not connect but every other screen works.

### 3. Installation Instruction

#### 3.1 Setting up the Development Environment

The steps below run TrustCabinet on one machine with sample data. Commands are written for a terminal opened at the root of the repository.

Step 1: Get the source code

| git clone https://github.com/callmehai/TrustCabinet.git<br>cd TrustCabinet |
| --- |

Step 2: Start PostgreSQL, Redis and the document store

Copy the example environment file, open .env and set your own POSTGRES_PASSWORD, then start the containers:

| cp .env.example .env<br>docker compose up -d<br>docker compose ps        # wait until postgres shows "healthy" |
| --- |

Step 3: Give the back end its connection string

The connection string is never stored in the repository. Set it once with user-secrets; without it the back end refuses to start, on purpose.

| dotnet user-secrets set "ConnectionStrings:TrustCabinet" \<br>"Host=localhost;Port=5432;Database=trustcabinet;Username=trustcabinet;Password=<POSTGRES_PASSWORD>" \<br>--project backend/TrustCabinet.Web |
| --- |

Step 4: Configure the document store (needed for documents)

| curl -X PUT http://localhost:8333/trustcabinet-dev<br>dotnet user-secrets set "FileStorage:ServiceUrl" "http://localhost:8333" --project backend/TrustCabinet.Web<br>dotnet user-secrets set "FileStorage:BucketName" "trustcabinet-dev" --project backend/TrustCabinet.Web<br>dotnet user-secrets set "FileStorage:AccessKeyId" "dev" --project backend/TrustCabinet.Web<br>dotnet user-secrets set "FileStorage:SecretAccessKey" "dev" --project backend/TrustCabinet.Web |
| --- |

Step 5: Enable the online room (optional)

| echo "LIVEKIT_API_SECRET=$(openssl rand -hex 24)" >> .env<br>dotnet user-secrets set "LiveKit:ApiSecret" "<the same value>" --project backend/TrustCabinet.Web<br>docker compose --profile calls up -d livekit |
| --- |

Speech-to-text and Voice ID need the speech worker (workers/speech/README.md) and the AI draft of the minutes needs a language model (Ollama with qwen2.5:7b-instruct). Both are optional: without them the related buttons say the service is not ready.

Step 6: Run the back end and the front end

Use two terminals. In Development the back end applies the migrations and seeds the sample accounts and meetings by itself on first start.

| dotnet run --project backend/TrustCabinet.Web      # API on http://localhost:5225 |
| --- |

| cd frontend<br>npm install<br>npm run dev                                       # UI on http://localhost:5173 |
| --- |

Step 7: Verify the installation

- Open http://localhost:5225/api/health — it answers that the service and the database are healthy.

- Open http://localhost:5173 (not 5225). The sign-in page appears; Vite forwards /api and /hubs to the back end.

- Sign in with one of the sample accounts in section II.1.3, password TrustCabinet@2026.

The checks the team runs before every pull request are dotnet build TrustCabinet.sln, dotnet test TrustCabinet.sln, npm run build and npm run lint in frontend/.

#### 3.2 Deploying to a Server

The test server runs the whole application with Docker Compose behind Caddy, which obtains the HTTPS certificate by itself. A first deployment takes about an hour.

Step 1: Create the server and open the network

- Create an Ubuntu 24.04 LTS instance (section 2.1) and attach a static IP address.

- Allow inbound HTTPS (443) besides SSH (22) and HTTP (80), and add Custom TCP 7881 and Custom UDP 7882 for the online room.

- Point an A record of your domain to the static IP.

Step 2: Install Docker

| sudo fallocate -l 4G /swapfile && sudo chmod 600 /swapfile<br>sudo mkswap /swapfile && sudo swapon /swapfile<br>echo '/swapfile none swap sw 0 0' \| sudo tee -a /etc/fstab<br>curl -fsSL https://get.docker.com \| sudo sh<br>sudo usermod -aG docker ubuntu        # then close and reopen the terminal |
| --- |

Step 3: Get the code

Give the server a read-only deploy key on the GitHub repository, then clone it:

| git clone git@github.com:callmehai/TrustCabinet.git<br>cd TrustCabinet |
| --- |

Step 4: Configure and start

| cat > deploy/.env <<EOF<br>SITE_ADDRESS=<your domain><br>POSTGRES_PASSWORD=$(openssl rand -hex 24)<br>SIGNING_KEY=$(openssl rand -hex 32)<br>LIVEKIT_API_SECRET=$(openssl rand -hex 32)<br>EOF<br>chmod 600 deploy/.env<br>docker compose -f deploy/docker-compose.yml build app<br>docker compose -f deploy/docker-compose.yml run --rm -T app --migrate --seed-demo<br>docker compose -f deploy/docker-compose.yml up -d |
| --- |

Generate POSTGRES_PASSWORD and SIGNING_KEY once and never change them: a new database password locks the application out of its own database, and a new signing key signs everybody out and breaks every guest invitation link already sent. --seed-demo creates the sample accounts; leave it out on a production server and create the first administrator account instead.

Step 5: Optional services

Add the lines below to deploy/.env as needed, then run docker compose -f deploy/docker-compose.yml up -d again.

| Service | Lines in deploy/.env |
| --- | --- |
| Documents on Cloudflare R2 | R2_ENDPOINT, R2_BUCKET, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY (one bucket per server) |
| Documents on the server itself | COMPOSE_PROFILES=files, R2_ENDPOINT=http://files:8333 and a bucket name |
| Email through Resend | RESEND_API_KEY, RESEND_FROM, RESEND_WEBHOOK_SECRET, RESEND_ENABLED=true |
| Speech-to-text and Voice ID | COMPOSE_PROFILES=speech, SPEECH_URL=http://speech:8090, and once VOICE_ID_KEY=$(openssl rand -base64 32) |
| AI draft of the minutes | AI_URL=http://<AI machine>:11434/v1, AI_MODEL=qwen2.5:7b-instruct (optional AI_BACKUP_URL) |
| Google sign-in | Add https://<SITE_ADDRESS> to the Authorized JavaScript origins of the OAuth client |

Step 6: Automatic deployment (optional)

.github/workflows/deploy.yml builds the images on GitHub, copies them to the server over SSH, backs up the database with pg_dump, migrates, switches containers and checks /api/health; it rolls back if the check fails. It needs three secrets in the GitHub environment of the server: DEPLOY_SSH_KEY, DEPLOY_HOST and DEPLOY_HOST_KEY.

#### 3.3 Verifying the Server

- docker compose -f deploy/docker-compose.yml ps shows every container running.

- docker compose -f deploy/docker-compose.yml logs caddy contains "certificate obtained successfully".

- https://<your domain>/api/health answers healthy, and the sign-in page opens over HTTPS.

- Open a meeting that is in progress and choose Vào phòng họp (Join room); two browsers signed in as different people see and hear each other.

| Symptom | What to check |
| --- | --- |
| The page times out | The firewall allows HTTPS (443) |
| The browser reports an invalid certificate | The A record points to the static IP and SITE_ADDRESS matches the domain |
| 502 Bad Gateway | The app is starting or stopped — docker compose -f deploy/docker-compose.yml logs app |
| Signed out right after signing in | The site must be opened with https:// |
| The online room says the connection was lost | The firewall allows 7881/TCP and 7882/UDP |

## II. User Manual

TrustCabinet is a paperless meeting system for one organisation. A Secretary prepares a meeting, a Chair approves it, everybody invited reads the documents and joins in person or in the built-in online room, votes are cast on screen, the meeting is recorded and transcribed, and the minutes are drafted with AI, approved by the Chair and issued.

The screens are in Vietnamese. In this manual each button or menu is written the way it appears on screen, in bold, with its meaning in brackets the first time — for example Tạo cuộc họp (Create meeting). Find your role below and go to your chapter.

| Your role | Go to |
| --- | --- |
| Everybody | Chapter 1 — Getting Started, and Chapter 6 — Features for Every User |
| Quản trị viên (Administrator) | Chapter 2 — Administrator Guide |
| Thư ký (Secretary) | Chapter 3 — Secretary Guide |
| Chủ toạ (Chair) | Chapter 4 — Chair Guide |
| Thành viên (Member) | Chapter 5 — Member Guide |
| Khách (Guest, invited by email) | Chapter 5.6 — Joining as a Guest |

Things to know before you start

- A role is a set of permissions. The administrator gives each account one role. Inside a meeting, a person also has a meeting role — Chủ toạ, Thư ký or Thành viên — chosen by whoever creates the meeting.

- A meeting only moves forward: Nháp (Draft) → Chờ duyệt (Pending approval) → Đã duyệt (Approved) → Đã công bố (Published) → Đang diễn ra (In progress) → Đã kết thúc (Ended). After publishing, its details can no longer be edited.

- You see only your meetings. A meeting appears to the people in it, to its approver and to nobody else.

- Mật (Secret) meetings are stricter: no documents downloaded, no recording, no minutes, and every viewing is written to the audit log.

### 1. Getting Started

#### 1.1 Supported Browsers

TrustCabinet works best in a recent version of:

- Google Chrome

- Microsoft Edge

- Mozilla Firefox

- Safari (Mac, iPhone, iPad)

Internet Explorer is not supported. On a phone every screen works, with a bottom tab bar instead of the sidebar; preparing a meeting is easier on a larger screen.

#### 1.2 Accessing TrustCabinet

Open your browser and go to the address your organisation gives you. On the test server this is https://app.trustcabinet.workspace-hub.space. You will see the sign-in page.

Figure 1: Sign-in page

#### 1.3 Creating Your Account

There is no self-registration. The administrator creates every account (section 2.2) and gives you your email address and a first password through a private channel. The test server has these sample accounts, all with the password TrustCabinet@2026:

| Email | Role | Can do |
| --- | --- | --- |
| quantri@trustcabinet.local | Quản trị viên | Manage users, roles, AI settings, read the audit log |
| thuky@trustcabinet.local | Thư ký | Create meetings, collect voice samples for others, write minutes |
| chutoa@trustcabinet.local | Chủ toạ | Create and approve meetings |
| thanhvien@trustcabinet.local | Thành viên | Attend, read documents, vote |

| Note: the test server is public. Enter only made-up data there — never real documents, recordings or personal information. |
| --- |

#### 1.4 Signing In

With email and password

- Enter your Email and Mật khẩu (Password). The eye icon shows the password you typed.

- Click Đăng nhập bằng mật khẩu (Sign in with password).

With Google

- Click Tiếp tục sử dụng dịch vụ bằng Google (Continue with Google).

- Choose the Google account whose email the administrator registered for you.

After signing in you land on Lịch làm việc (Work calendar), the same for every role. The sidebar shows only the sections your role may use.

#### 1.5 If You Cannot Sign In

| What you see | What to do |
| --- | --- |
| Email or password is incorrect | Check for typos and that Caps Lock is off, then try again. |
| Too many attempts | Sign-in attempts are limited per minute. Wait one minute and try again. |
| Google: "Hãy chọn đúng tài khoản Google có email đã đăng ký…" | The Google account you chose is not registered. Choose the right one, or ask the administrator for an account. |
| Account deactivated | Your account was turned off by the administrator. Ask them to reactivate it. |

#### 1.6 Signing Out

- Click your name at the bottom of the sidebar to open the account menu.

- Click Đăng xuất (Sign out) and confirm.

Figure 2: Sign-out confirmation

| Tip: closing the tab does not sign you out. Always sign out on a shared computer. |
| --- |

#### 1.7 Resetting Your Password

Passwords are reset through a link sent by email:

- Ask the administrator to send you a password-reset link (section 2.2).

- Open the email and click the link. The Đặt mật khẩu mới (Set a new password) page opens.

- Type the new password twice and confirm. A password has at least 12 characters.

The link works once, for 60 minutes, and stops working when a newer link is sent. Your old password keeps working until you set the new one.

#### 1.8 The Main Screen

Every screen sits in one window with a blue sidebar on the left. The sidebar can be collapsed with the arrow at its edge.

Figure 3: Work calendar — a week with one approved meeting

| Part of the screen | What it does |
| --- | --- |
| Lịch làm việc (Work calendar) | Your meetings by week or month; the list icon shows them as a list. Click a meeting to see a summary and open it. |
| Cuộc họp (Meetings) | The list of meetings you take part in, with search and filters. |
| Tìm giờ họp (Find a time) | Finds a time when the people you need are free (only for roles that can create meetings). |
| Báo cáo (Reports) | Meeting statistics over a period, exportable to Excel. |
| Quản trị (Administration) | Users, roles, AI settings and the audit log (administrators only). |
| Your name (bottom) | Account menu: Kho tài liệu (Document library), Giọng nói của tôi (My voice), Chia sẻ lịch (Calendar sharing), Đăng xuất (Sign out). |
| Bell (bottom) | Notifications; the red badge counts the unread ones. |

Figure 4: Account menu

#### 1.9 Notifications

The bell shows what happened to your meetings: an invitation, an approval request, an approval or rejection, a meeting starting, minutes issued, a calendar-sharing request. New notifications arrive immediately, without reloading. Click one to open what it is about; Đánh dấu tất cả đã đọc (Mark all as read) clears the badge. Invitations and other important events are also sent by email.

Figure 5: Notification panel

#### 1.10 Meeting Status Reference

| Status | Meaning |
| --- | --- |
| Nháp (Draft) | Being prepared by the Secretary; only the people preparing it see it. |
| Chờ duyệt (Pending approval) | Sent to an approver; details are locked while waiting. |
| Bị từ chối (Rejected) | The approver rejected it with a reason; the Secretary can edit and send it again. |
| Đã duyệt (Approved) | Approved; the Secretary can now publish it. |
| Đã công bố (Published) | Invitations sent; participants confirm attendance and read documents. Details can no longer be edited. |
| Đang diễn ra (In progress) | Started; the online room is open and voting can happen. |
| Đã kết thúc (Ended) | Finished; recordings, transcript and minutes are worked on. Final. |
| Đã huỷ (Cancelled) | Cancelled with a reason; everybody invited is told. Final. |

Mức độ mật (Sensitivity) is shown next to the status: Thường (Normal), Nội bộ (Internal — like Normal, but the AI draft is never sent to an outside service) and Mật (Secret).

### 2. Administrator Guide

As an administrator you can:

- create, edit, deactivate and reactivate user accounts and send password-reset links;

- create roles and decide which permissions each role has;

- choose which AI model writes the draft minutes;

- read the audit log of business and security events.

You cannot see other people's meetings or documents because you are an administrator; you see a meeting only when you are invited to it.

#### 2.1 Managing User Accounts

Open Quản trị › Người dùng (Administration › Users). The list shows each person's name, email, role, how they sign in and whether the account is active. Search by name or email, or filter by role and status.

Figure 6: User list

| Action icon | What it does |
| --- | --- |
| Pencil — Sửa tài khoản | Edit the name, email and role. |
| Lock — Vô hiệu hoá / Kích hoạt lại | Deactivate the account (the person is signed out everywhere and cannot sign in) or reactivate it. History is kept; accounts are never deleted. |
| Key — Gửi liên kết đặt lại mật khẩu | Email a one-time password-reset link valid for 60 minutes. |
| Folder — Xem kho tài liệu | Read-only view of a deactivated person's document library, to recover files; every opening is logged. |

#### 2.2 Creating an Account

- Click Tạo tài khoản (Create account).

- Fill in the form (table below). Tạo mật khẩu (Generate password) creates a strong password; the copy icon copies it.

- Click Tạo tài khoản. The account works immediately.

- Give the password to the person through a private channel, not a group chat.

Figure 7: Create account dialog

| Field | Required? | Notes |
| --- | --- | --- |
| Họ tên (Full name) | Yes | As shown everywhere in the system |
| Email | Yes | Used to sign in. If it is a Google account, the person can also sign in with Google. |
| Mật khẩu (Password) | Yes | At least 12 characters |
| Vai trò (Role) | Yes | Thành viên by default |

Editing, deactivating and resetting

The pencil opens the same form for an existing account; changing a role takes effect immediately and the person is notified. The lock and key icons ask for confirmation first, as shown below.

Figure 8: Edit account dialog

Figure 9: Deactivating an account

Figure 10: Sending a password-reset link

#### 2.3 Roles and Permissions

Open Quản trị › Vai trò (Administration › Roles). The Danh sách vai trò (Role list) tab shows each role, its description and how many accounts have it. The four built-in roles (Mặc định) cannot be deleted; roles you create can be edited and deleted.

Figure 11: Role list

The Ma trận phân quyền (Permission matrix) tab shows which permission each role has. Tick or untick a box to change it; the change applies at once to everybody with that role.

Figure 12: Permission matrix

| Permission | Allows |
| --- | --- |
| Tạo cuộc họp (Create meetings) | Create and prepare meetings, find a time, see calendars shared with them |
| Phê duyệt cuộc họp (Approve meetings) | Be chosen as the approver of a meeting and approve or reject it |
| Thu giọng mẫu hộ (Collect voice samples) | Record voice samples of participants with the room microphone, for those who agreed to Voice ID |
| Soạn biên bản (Write minutes) | Create the minutes draft and send it for approval |
| Quản trị người dùng (Administer users) | Every screen under Quản trị |

To add a role, click Tạo vai trò (Create role), enter a name, an optional description and its first permissions, then Tạo vai trò.

Figure 13: Create role dialog

#### 2.4 AI Configuration

Quản trị › Cấu hình AI chooses what writes the draft minutes of Normal meetings: Máy local (the organisation's own model) or API (an OpenAI-compatible service: address, model and key). Thử kết nối (Test connection) checks the settings; Lưu (Save) applies them. Internal meetings always use the local model, whatever is chosen here, and Secret meetings have no AI draft at all.

Figure 14: AI configuration

#### 2.5 Audit Log

Quản trị › Nhật ký hệ thống (Audit log) lists business and security events — sign-ins, account and role changes, meeting workflow, document access, voice consent — with time, person, object and IP address. Filter by object, period, person or event type; the eye icon shows the details. Entries cannot be edited or deleted.

Figure 15: Audit log (IP addresses blurred in this manual)

### 3. Secretary Guide

As a Secretary you prepare meetings and their records:

- create a meeting, its agenda, participants and documents, and send it for approval;

- publish it once approved, start it, run attendance and votes, and record it;

- review the transcript and write the minutes, then send them to the Chair;

- find a time when everybody is free.

Your account is created by the administrator with the Thư ký role (or any role with the Create meetings permission).

#### 3.1 The Meeting List

Cuộc họp (Meetings) shows the meetings you take part in. The cards at the top count today's meetings, published meetings, meetings waiting for you (Cần xử lý) and your drafts; click one to filter the list. Search by name, filter by status, sensitivity or period, and click a column title to sort. The eye icon or the meeting name opens the meeting; Vào phòng họp appears on meetings in progress.

Figure 16: Meeting list

#### 3.2 Creating a Meeting

- On Cuộc họp or Lịch làm việc, click Tạo cuộc họp (Create meeting).

- Fill in the left column: name, description, date and time (the buttons 30 phút, 1 giờ, 2 giờ set the end time; Nhiều ngày allows several days).

- Under Người dự họp (Participants), type a name or email and pick the person. Choose each person's meeting role: exactly one Chủ toạ, a Thư ký to start the meeting, and Thành viên for the others.

- In the right column choose Hình thức họp (Format) — Trực tiếp (in person), Trực tuyến (online) or Kết hợp (hybrid) — the room and/or online link, and the Mức độ mật.

- Click Tạo cuộc họp. The meeting is saved as Nháp (Draft).

Figure 17: Create meeting — details, time and format

Figure 18: Create meeting — participants and their meeting roles

| Field | Required? | Notes |
| --- | --- | --- |
| Tên cuộc họp (Name) | Yes | Up to 300 characters |
| Mô tả (Description) | No | Purpose and main content, up to 4,000 characters |
| Thời gian (Time) | Yes | Date, start and end |
| Người dự họp (Participants) | Yes | At least one person; exactly one Chủ toạ |
| Hình thức họp (Format) | Yes | In person: the Secretary records the whole room. Online: each person's microphone is recorded. Hybrid: both. |
| Địa điểm / Link họp trực tuyến | No | Room name; leave the link empty to use the built-in TrustCabinet room |
| Mức độ mật (Sensitivity) | Yes | Thường, Nội bộ or Mật |

#### 3.3 The Meeting Page and the Agenda

The meeting page shows the title, status, time and format, a banner with the next step, the tabs, and Lịch sử trạng thái (Status history) on the right. While it is a draft, Chỉnh sửa (Edit) changes the details and Huỷ cuộc họp (Cancel meeting) cancels it with a reason.

Figure 19: A draft meeting — Thông tin chung (General) tab

In the Nội dung cuộc họp (Agenda) tab, click Thêm nội dung (Add item), enter the item, an optional presenter and notes, then Lưu. Drag the handle to reorder items; the paper-clip attaches documents to an item; the pencil and bin edit and remove it.

Figure 20: Adding an agenda item

Figure 21: The agenda of the sample meeting

#### 3.4 Participants and Guests

The Thành phần tham dự (Participants) tab lists everybody with their meeting role. The person-plus icon adds people; the pencil changes a role (including handing over the Chair); the bin removes someone. The envelope icon Mời khách ngoài hệ thống invites a guest without an account by email (section 5.6). During the meeting this tab also shows attendance.

Figure 22: Participants tab

#### 3.5 Meeting Documents

The Tài liệu (Documents) tab groups documents Theo nội dung họp (by agenda item), Biểu quyết (by vote) and Tài liệu chung (general). Click Thêm tài liệu chung, or the paper-clip of an agenda item, then either:

- Tải từ máy (Upload) — drag PDF or DOCX files in, up to 10 MB each, 10 files and 100 MB at a time. The files are also saved in your document library, in the meeting's folder; or

- Từ kho của tôi (From my library) — pick documents you already have.

Everybody who can see the meeting can read its documents. In a Secret meeting they can be read online only, never downloaded.

Figure 23: Documents tab, grouped by agenda item

Figure 24: Adding documents — upload from the computer

Figure 25: Adding documents — pick from my library

#### 3.6 Sending for Approval

- When the banner says the details are complete, click Gửi duyệt (Send for approval).

- Choose the Người duyệt (Approver) — anyone with the Approve meetings permission.

- Click Gửi duyệt. The status becomes Chờ duyệt and the approver is notified.

Figure 26: Choosing the approver

Figure 27: Waiting for approval

While it waits the details cannot be edited; Rút về nháp (Withdraw to draft) takes it back. If it is rejected, the banner shows the reason; edit and choose Gửi duyệt lại (Send again).

#### 3.7 Publishing, Rescheduling and Cancelling

Once approved, click Công bố (Publish) and confirm. Participants receive the invitation in the app and by email and can confirm attendance; the details can no longer be edited.

Figure 28: An approved meeting, ready to publish

Figure 29: Publish confirmation

A published meeting shows Bắt đầu họp (Start), Dời lịch (Reschedule — change the time, everybody is told) and Huỷ cuộc họp (Cancel — with a reason, cannot be undone). Like every participant you also answer the invitation with Tham dự or Không tham dự.

Figure 30: A published meeting

#### 3.8 Starting the Meeting

Click Bắt đầu họp. Starting earlier than scheduled asks for confirmation (Vẫn bắt đầu). The status becomes Đang diễn ra, the online room opens and the Chair gets Kết thúc họp (End meeting). A meeting needs a Secretary in its participants to start.

Figure 31: Starting earlier than scheduled

Figure 32: A meeting in progress

#### 3.9 The Online Meeting Room

Click Vào phòng họp (Join room). The lobby previews your camera and microphone, tells you the meeting is recorded for the minutes (recordings are deleted after 180 days) and offers to record your voice sample if you have none. Allow the browser to use the microphone and camera, then click Vào phòng (Enter).

Figure 33: Room lobby (the browser here had no microphone)

Figure 34: The online room

| Control | What it does |
| --- | --- |
| Microphone / Camera (with ^ for device settings) | Turn your microphone or camera on and off |
| Chia sẻ màn hình | Share your screen |
| Mở cửa sổ nổi | Keep the call in a floating window while you use other screens |
| Giơ tay | Raise your hand to ask to speak |
| Rời phòng (red) | Leave the room; the meeting continues |
| Người dự | People in the room |
| Phát biểu | The speaking queue |
| Nội dung họp | The agenda; the Chair and Secretary can edit it here |
| Ghi chú | Your private notes for this meeting |
| Điểm danh | Attendance (Secretary) |
| Tài liệu | The meeting documents, opened in a new tab |
| Biểu quyết | Votes |
| Chat | Messages to the people in the room; not kept after the meeting |

As Secretary you take attendance in Điểm danh: choose each person's state from the list.

Figure 35: Agenda panel in the room

Figure 36: Attendance panel

Figure 37: Chat panel

#### 3.10 Preparing a Vote

In Biểu quyết click Tạo biểu quyết (Create vote). Enter the question, the options (Đồng ý, Không đồng ý, Không ý kiến by default; Thêm phương án adds more), choose Công khai (open — after closing everybody sees who chose what) or Bỏ phiếu kín (secret ballot), tick who may vote, then Lưu nháp (Save draft). Votes can be prepared before the meeting and opened by the Chair during it (section 4.3).

Figure 38: Create vote

Figure 39: A vote saved as draft

#### 3.11 Recordings and Transcript

In an in-person or hybrid meeting the Secretary turns on the room microphone with Bật mic phòng; in the online room each person's microphone is recorded. Turning the microphone off saves that part and sends it to speech-to-text. The Bản ghi (Recordings) tab plays the whole meeting, lists the speakers, and shows the transcript line by line with the speaker and time. You can correct a speaker, listen to one line, download the transcript (.txt) or the MP3, and exclude a line from the minutes. Speakers are recognised with Voice ID for people who agreed to it (section 6.3).

Figure 40: Recordings tab with the live transcript

#### 3.12 Writing the Minutes

The Biên bản (Minutes) tab opens when the meeting ends. It has two cards:

- Dự thảo nội dung (Content draft): click Tạo dự thảo and the AI summarises the agenda, the votes and the transcript into sections I–III. Review and edit each section, then save. Tạo lại writes it again.

- Biên bản (Minutes): creates the minutes from the organisation's fixed template, filling in the meeting details, attendance, vote results and the draft. Edit the text directly; places still marked “……” are highlighted.

- Click Gửi duyệt to send the minutes to the Chair. You cannot edit while they wait.

- If the Chair returns them (Bị trả lại), read the reason, correct and send again. Once approved they are Đã ban hành (Issued).

- To correct issued minutes, click Tạo bản đính chính (Create correction); it goes through the same approval and replaces the previous version. Tải xuống downloads any version.

Figure 41: Minutes tab — approved AI content draft

Figure 42: Issued minutes in the organisation's template

| Note: the AI draft must be reviewed. Secret meetings have no recording and no minutes in TrustCabinet. |
| --- |

#### 3.13 Finding a Meeting Time

Tìm giờ họp (Find a time) looks for slots when the main participants are free, based on calendars they share with you.

- Set the length, the date range, the weekdays, the working hours and the lunch break, the gap between meetings and the rounding.

- Add up to 10 Người dự chính (main participants). A person who has not shared their calendar with you shows Chưa được chia sẻ lịch; click Yêu cầu chia sẻ lịch to ask them.

- Tick Tính cả lịch của tôi to include your own calendar, then click Tìm giờ.

- Free slots are listed by day. Click one to see who is free, then Tạo nháp to create a draft meeting at that time.

Figure 43: Find a time — search settings

Figure 44: Participants who have not shared their calendar

Figure 45: Asking to see a calendar

Figure 46: Free slots found

Figure 47: A chosen slot, ready to create a draft

The Lịch được chia sẻ tab shows the week of busy hours of the people who share with you; you see when they are busy, never the name or content of their meetings.

### 4. Chair Guide

As a Chair you approve meetings, run them and approve their minutes. The built-in Chủ toạ role can also create meetings exactly as a Secretary does (chapter 3).

#### 4.1 Approving or Rejecting a Meeting

When a meeting is sent to you, you get a notification and the meeting shows Cuộc họp đang chờ bạn duyệt (Waiting for your approval).

Figure 48: A meeting waiting for your approval

| Action | When available | What happens |
| --- | --- | --- |
| Duyệt (Approve) | Pending approval | Confirm the dialog. The meeting becomes Approved and the Secretary can publish it. |
| Từ chối (Reject) | Pending approval | Enter a reason (required). The Secretary sees it, edits the meeting and may send it again. |

Figure 49: Approve confirmation

Figure 50: Reject with a reason

Figure 51: Approved

#### 4.2 Starting and Running the Meeting

You can start a published meeting with Bắt đầu họp, join with Vào phòng họp, and end it with Kết thúc họp. In the room your name carries the Chủ toạ tag. Besides the controls in section 3.9 you can:

- open and close votes;

- see the speaking queue in Phát biểu and invite the next person with Mời;

- edit the agenda and hand the Chair over to another participant from the Participants tab.

Figure 52: A meeting in progress, seen by the Chair

Figure 53: Speaking queue

#### 4.3 Running a Vote

- In Biểu quyết, expand the draft vote and click Mở (Open). Confirm: the list of voters is fixed at this moment.

- The vote shows Đang mở (Open), the time and how many have voted (for example 1/3). Choose your option and click Gửi phiếu (Submit). A ballot cannot be changed afterwards.

- Click Đóng phiếu (Close) when everybody has voted. The result and the conclusion appear; for an open vote, Phiếu từng người shows who chose what.

Figure 54: Opening a vote

Figure 55: Casting a ballot

Figure 56: Ballot recorded

Figure 57: Result of a closed vote in the meeting's Votes tab

#### 4.4 Ending the Meeting

Click Kết thúc họp and confirm. The meeting ends and cannot be reopened; the online room closes and everybody is disconnected. Leaving the room with Rời phòng does not end the meeting.

Figure 58: End meeting confirmation

#### 4.5 Approving the Minutes

When the Secretary sends the minutes you are notified. Open the Biên bản tab and read them, then:

- Duyệt và ban hành (Approve and issue) — the minutes become Issued and everybody in the meeting can read and download them; or

- Trả lại (Return) — enter what to change (up to 2,000 characters); the Secretary corrects and sends them again.

### 5. Member Guide

As a member you take part in the meetings you are invited to:

- answer invitations;

- read the documents and comment on them;

- join the online room, raise your hand and vote;

- read the minutes once issued.

You cannot create meetings or see meetings you are not invited to.

#### 5.1 Answering an Invitation

When a meeting is published you get a notification and an email. Open the meeting and answer Bạn có tham dự cuộc họp này? with Tham dự (Attend) or Không tham dự (Decline). You can answer only once. Declining removes the meeting from your calendar.

Figure 59: Answering an invitation

Figure 60: Declining asks for confirmation

#### 5.2 Reading Documents and Commenting

Open the Tài liệu tab and click a document to read it full screen; zoom with the buttons at the top. Documents you have not opened are marked Chưa đọc. Click Góp ý (Comment) to write a comment, optionally tied to a page; others can reply. Comments close when the meeting ends or is cancelled. In a Secret meeting documents can only be read online.

#### 5.3 Joining the Room and Voting

When the meeting is in progress, click Vào phòng họp on the meeting or in the calendar (section 3.9). To speak, click Giơ tay: you see your place in the queue and the Chair invites you; Hạ tay lowers your hand. When the Chair opens a vote, choose your option in Biểu quyết and click Gửi phiếu.

Figure 61: An open vote, seen by a member

Figure 62: After voting

Figure 63: Raising a hand to speak

#### 5.4 Leaving the Room

Click the red button and choose Rời phòng. The meeting continues and you can join again while it is in progress.

Figure 64: Leaving the room

#### 5.5 Reading the Results

After the meeting the Biểu quyết tab shows each vote's result and conclusion, the Thành phần tham dự tab shows attendance, and the Biên bản tab shows the issued minutes, which you can download.

Figure 65: Votes of an ended meeting

Figure 66: Participants and attendance of an ended meeting

#### 5.6 Joining as a Guest

A person outside the organisation can be invited by email by the Secretary. A guest has no account:

- Open the link in the invitation email.

- Enter the code sent to the same email address and click Xác minh (Verify). A code works once; Gửi lại mã sends a new one.

- Enter your display name and title, then Vào cuộc họp (Enter the meeting).

- Read the agenda, participants and documents, answer the invitation, and join the room and vote when the meeting is in progress.

The link stops working when the meeting ends or the Secretary revokes the invitation.

### 6. Features for Every User

#### 6.1 My Document Library

Kho tài liệu (Document library), in the account menu, holds your own documents in folders (up to 5 levels). Thư mục mới creates a folder, Tải tài liệu lên uploads files; the icons on each row rename, move and delete. The Đang dùng column counts the meetings that use a document. Documents added to a meeting by upload are stored here in the meeting's folder. The bar at the top right shows the storage used by the whole system.

Figure 67: Document library

Figure 68: Creating a folder

#### 6.2 Calendar Sharing

Chia sẻ lịch (Calendar sharing), in the account menu, decides who may see when you are busy — never the names or content of your meetings.

- Người xem được lịch của tôi (Who can see my calendar): click Chia sẻ lịch, choose a person and the period. Requests from others appear here with Đồng ý (Accept, where you may shorten the period) and Từ chối (Decline).

- Lịch được chia sẻ với tôi (Calendars shared with me): click Xin xem lịch to ask someone, choosing the level — Normal meetings only, or Normal and Internal. Secret meetings never show.

- The ⋮ menu ends a sharing; ended ones are listed under Chia sẻ đã kết thúc.

Figure 69: Calendar sharing

Figure 70: Sharing my calendar

Figure 71: A request to see my calendar

Figure 72: Accepting, with the period to share

#### 6.3 My Voice (Voice ID)

Giọng nói của tôi (My voice) lets the system recognise you in the transcript of meetings you attend. Your voice is sensitive biometric data under the Personal Data Protection Law 2025, so it is used only with your consent:

- Read the terms: what is stored (voice samples and a voice signature, both encrypted), who records them (you, or the Secretary with the room microphone — you are notified each time), and where they are used.

- Tick the consent box and click Đồng ý và bật (Agree and enable).

- Record your sample by reading the paragraph shown.

You can listen to and delete each sample and withdraw consent at any time on the same page; withdrawing deletes every sample, suggestion and name filled in by the system. Without Voice ID every other feature still works.

Figure 73: Voice ID consent

#### 6.4 Reports

Báo cáo (Reports) summarises the meetings you can see over a period of up to 24 months: average attendance, average length, time from the end of a meeting to issued minutes, meetings per month, by status and by sensitivity. Xuất Excel (Export to Excel) downloads the figures.

Figure 74: Reports

#### 6.5 Frequently Asked Questions

| Question | Answer |
| --- | --- |
| I cannot find a meeting. | You see only meetings you are invited to. A draft is visible only to the people preparing it until it is published. |
| Why can I not edit the meeting? | Details are locked while waiting for approval and after publishing. Use Reschedule to change the time. |
| The room says it cannot use my microphone. | Click the lock icon next to the address bar, allow the microphone and reload. You can still join to listen. |
| I voted by mistake. | A ballot cannot be changed. Tell the Chair. |
| Why is there no recording? | Secret meetings are never recorded, and in-person meetings are recorded only when the Secretary turns on the room microphone. |
| The password-reset link does not work. | It works once, for 60 minutes, and only the newest link works. Ask the administrator for a new one. |
