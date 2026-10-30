"""Manual test designs for the implemented TrustCabinet surface, in English."""
import json
from pathlib import Path
AUTH='frontend/src/features/auth/LoginPage.tsx'
USERS='frontend/src/features/access/UsersPage.tsx'
ROLES='frontend/src/features/access/RolesPage.tsx'
FORM='frontend/src/features/meetings/MeetingForm.tsx'
FLOW='frontend/src/features/meetings/workflow/MeetingWorkflowBar.tsx'
DETAIL='frontend/src/features/meetings/detail/MeetingDetailPage.tsx'
LIST='frontend/src/features/meetings/MeetingsPage.tsx'
CALENDAR='frontend/src/features/calendar/CalendarPage.tsx'
NOTICE='backend/TrustCabinet.Web/Endpoints/NotificationEndpoints.cs'


def scenarios():
    rows=[]
    def add(level, code, module, title, pre, steps, expected, source, status='NotRun'):
        rows.append(dict(id=f'{level}-{code}', level=level, module=module, title=title,
            precondition=pre, steps='\n'.join(f'{i}. {s}' for i,s in enumerate(steps.split('|'),1)),
            expected=expected, source=source, line=1, status=status, tester='', executedAt='', actual='', evidence='', defect='',
            automation='Manual', className=module, fullName='', method='', data=[]))
    add('SYS','01','WorkFlow1','Provision access, create a meeting and revoke permission','AdminUsers; MeetingCreator role; new user; staging PostgreSQL/Redis.','Create user and assign role.|Sign in as user.|Create Draft.|Remove creation capability.|Reload and retry creation.','Creation succeeds while authorized and fails after revocation; original Draft remains intact.',USERS)
    add('SYS','02','WorkFlow2','Draft, submission, rejection, revision and approval','Separate active organizer/approver; ready meeting; notification worker.','Create and submit Draft.|Read notification and reject with reason.|Revise and resubmit.|Approve.|Inspect history.','Transitions/recipients are correct; reasons and history persist; unauthorized actions fail.',FLOW)
    add('SYS','03','WorkFlow3','Publication, notifications, email and Calendar','Approved fixture with active Chair/Secretary and participants; seeded roster; SMTP sandbox; workers enabled.','Publish.|Receive notification and ICS email.|Follow notification.|Compare Calendar and details.','Published status and schedule agree; intended recipients receive invitation; retries do not duplicate; outsiders receive no data.',FLOW)
    add('SYS','04','WorkFlow4','Synchronize published meeting reschedule','Published meeting; participants; original invitation recorded; SMTP sandbox.','Reschedule.|Inspect history.|Check bell, update email and Calendar.|Reload both browsers.','All screens show latest schedule; history records change; updates contain new time.',FORM)
    add('SYS','05','WorkFlow5','Deactivate, revoke sessions and restore access','Independent admin; target signed in on two tabs; not final admin.','Deactivate target.|Try reading/editing from both tabs.|Reactivate.|Sign in again.','Operations fail while inactive; new sign-in succeeds after reactivation; old sessions cannot bypass active-user checks.',USERS)
    add('SYS','06','WorkFlow6','Reset password across email and authentication','Administrator/test user; SMTP sandbox; issued reset link.','Request reset.|Complete through email.|Try old/new passwords.|Reuse token.|Check old sessions against auth contract.','Old password fails; new one works; token is single-use; session revocation follows backend contract.',AUTH)
    add('SYS','07','WorkFlow7','Isolate meeting data across roles','Organizer, assigned approver, participant, outsider; Draft/WaitingApproval/Published fixtures.','Open list/details as each role.|Try history, edit and transitions.|Replace approver and retry as former approver.','Access follows relationship/state; unrelated or revoked users cannot read restricted data; inaccessible records return 404.',DETAIL)
    add('SYS','08','WorkFlow8','Recover queued email after SMTP outage','Isolated staging; controllable SMTP sandbox; valid Approved fixture.','Stop SMTP.|Publish.|Inspect retry state.|Restart SMTP and allow retry.|Compare mail and queue.','Publication persists; email retries after recovery without duplicate deduplication keys.','backend/TrustCabinet.Infrastructure/Email/EmailDeliveryWorker.cs')
    add('SYS','09','WorkFlow9','Operate agenda through complete meeting UI','Blocked: agenda UI and meeting-start workflow unavailable in this checkout.','Create/reorder agenda in UI.|Start meeting.|Start/finish item as Chair.|Observe participant browser.','After integration, order/state/duration synchronize according to API; re-execute workflow.',DETAIL,'Blocked')
    for i,(module,title,pre,steps,expected,src) in enumerate([
        ('Administration','Administer user access','Administrator representative; disposable accounts.','Create user and role.|Assign capabilities.|Deactivate/reactivate.|Confirm permitted work.','Tasks are usable; permissions correct; final administrator protected.',USERS),
        ('Preparation','Prepare a meeting','Organizer with MeetingCreator.','Create Draft with schedule/location/content.|Edit.|Find and reopen.','Information persists; organizer confirms form supports preparation.',FORM),
        ('Approval','Review meeting requests','Organizer/approver representatives; ready meetings.','Submit.|Approve one meeting.|Reject another with reason.|Check feedback.','Parties understand outcomes/reasons; history identifies reviewer.',FLOW),
        ('Publication','Receive correct invitation','Approved fixture with roster; participant; SMTP sandbox.','Publish.|Read notification and ICS email.|Open details and Calendar.','Meeting/time/location/content agree across channels.',CALENDAR),
        ('Rescheduling','Understand schedule change','Published meeting; organizer and participant.','Reschedule.|Find new time through notification and Calendar.|Confirm applicable time.','Participant recognizes latest time without contradictory information.',FORM),
        ('Confidentiality','Protect meeting information','Participant and unrelated-user representatives.','Open invited meeting.|Search/enter URL as outsider.|Review with administrator.','Participant sees permitted data; outsider cannot view restricted content.',DETAIL),
        ('Recovery','Reset forgotten password','Administrator/user representatives; SMTP sandbox.','Request reset.|Read email and change password.|Sign in again.','User can follow instructions; consumed link cannot be reused.',AUTH),
        ('Usability','Use supported browsers','Chrome/Edge; record versions and screen sizes in evidence.','Sign in.|Find meeting.|Open Calendar.|Read notifications.|Record suggestions.','No UI defect blocks tasks; feedback is recorded separately from acceptance.',LIST),
    ],1): add('UAT',f'{i:02}',module,title,pre,steps,expected,src)
    return ui_scenarios()+rows


def ui_scenarios():
    """Functional UI tests written by hand in cases/ui-*.json, the way the Report 5.2 template does:
    steps a tester follows in the browser, what the screen must show, and the state to start from."""
    rows=[]
    number=lambda items:'\n'.join(f'{i}. {x}' for i,x in enumerate(items,1))
    for path in sorted((Path(__file__).resolve().parent/'cases').glob('ui-*.json')):
        data=json.loads(path.read_text(encoding='utf-8'))
        for n,c in enumerate(data['cases'],1):
            rows.append(dict(id=f"{data['prefix']}{n:02}", level='UI', module=data['module'], group=c['group'], title=c['d'],
                precondition=number(c['pre']), steps=number(c['steps']), expected=number(c['exp']), source=data['source'], line=1,
                status='NotRun', tester='', executedAt='', actual='', evidence='', defect='', automation='Manual',
                className=data['module'], fullName='', method='', data=[], authoredText=True))
    return rows
