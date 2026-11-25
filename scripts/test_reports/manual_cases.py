"""Manual test designs for the implemented TrustCabinet surface, in English."""
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
    add('UI','AUTH-01','Authentication','Sign in, restore session and sign out','Active internal user; clean browser; backend and Redis available.','Sign in with valid credentials.|Reload.|Sign out and reopen a protected URL.','The meeting list opens; reload restores the session; sign-out requires authentication again.',AUTH)
    add('UI','AUTH-02','Authentication','Reject invalid credentials','No session; active account.','Submit empty fields.|Enter an invalid email.|Use the wrong password.','Field errors appear and no authenticated session is created.',AUTH)
    add('UI','AUTH-03','Authentication','Google sign-in requires a provisioned user','Configured Google test client/origin; provisioned and unprovisioned emails.','Sign in with provisioned Google account.|Sign out.|Try an unprovisioned Google account.','Only the active internal account signs in; no user is automatically registered.',AUTH)
    add('UI','AUTH-04','Authentication','Use a single-use reset link','AdminUsers and active target user; SMTP sandbox.','Request reset as administrator.|Open link and set valid password.|Sign in with new password.|Reuse link.','New password works; a consumed link cannot change it again.','frontend/src/features/auth/ResetPasswordPage.tsx')
    add('UI','ACCESS-01','Access','Create and find a user','AdminUsers; unused email; suitable role.','Create a user with required fields.|Search by name/email.|Reload.','Exactly one account persists with correct fields and role.',USERS)
    add('UI','ACCESS-02','Access','Reject duplicate email and preserve input','AdminUsers; existing email.','Submit an existing email.|Inspect error.|Correct email and resubmit.','Duplicate is rejected; form input remains editable; correction creates one account.',USERS)
    add('UI','ACCESS-03','Access','Deactivate a signed-in user','Separate admin/user browsers; target is not final administrator.','Deactivate target.|Reload protected data as target.|Try signing in again.','Inactive user cannot read protected data or sign in; list reflects backend state.',USERS)
    add('UI','ACCESS-04','Access','Manage a custom role','AdminUsers; unassigned disposable role; another active admin remains.','Create and rename role.|Toggle MeetingCreator, save and reload.|Delete role.','Changes persist; role is removed; system-role protections remain effective.',ROLES)
    add('UI','ACCESS-05','Access','Protect the last active administrator','Isolated environment with exactly one active AdminUsers user.','Try deactivating user.|Try removing final administrative capability through roles.','Operations are rejected and an active administrator remains.',ROLES)
    add('UI','ACCESS-06','Access','Hide restricted administration screens','Active user without AdminUsers; known management URLs.','Inspect sidebar.|Open management URL directly.|Call a fixed management API.','Sidebar hides restricted entry; UI shows 404; fixed API returns 403 without protected data.','frontend/src/features/auth/RequireCapability.tsx')
    add('UI','MEETING-01','Meetings','Create and edit a draft meeting','MeetingCreator; future meeting time.','Create a valid Draft.|Edit title, schedule and location.|Reload details.','Draft persists with latest valid values.',FORM)
    add('UI','MEETING-02','Meetings','Validate meeting information','MeetingCreator account.','Submit missing required fields.|Set end before start.|Correct and submit.','Invalid input is rejected; a valid submission creates one meeting.',FORM)
    add('UI','MEETING-03','Meetings','Search, filter and protect meeting list','Visible meetings in several states; an unreadable meeting.','Search title.|Apply filters and paging.|Open visible details.|Try unreadable URL.','Results match filters; restricted records are absent and direct access returns 404.',LIST)
    add('UI','MEETING-04','Meetings','Submit and approve meeting','Ready Draft; organizer and assigned active approver in separate browsers.','Select approver and submit.|Approve as assigned approver.|Reload history.','Draft becomes WaitingApproval then Approved; history records correct actor/action.',FLOW)
    add('UI','MEETING-05','Meetings','Reject, revise and resubmit','WaitingApproval meeting; authorized organizer and approver.','Reject with reason.|Read reason and revise.|Resubmit.','Reason is visible; Rejected permits revision; previous history remains intact.',FLOW)
    add('UI','MEETING-06','Meetings','Require meeting roles before publication','Approved A has active Chair and Secretary; B lacks Secretary. Seed rosters because roster UI is unavailable.','Publish A.|Try publishing B.|Reload both.','A becomes Published; B remains Approved with validation error; no participant is silently added.',FLOW)
    add('UI','MEETING-07','Meetings','Reschedule a published meeting','Published fixture; organizer/participants; SMTP sandbox.','Change permitted time/location.|Save and reload.|Check participant notifications.','Permitted changes persist, locked fields stay unchanged, intended recipients receive updates.',FORM)
    add('UI','CALENDAR-01','Calendar','Switch calendar views and open details','Readable meetings across weeks/months, including near midnight.','Switch week/month/list.|Navigate dates.|Open preview and details.','Times agree in browser time zone; correct meeting opens; unreadable meetings remain hidden.',CALENDAR)
    add('UI','NOTICE-01','Notifications','Receive notification and persist read status','Two users; event for one recipient; active hub.','Observe bell after event.|Mark one read.|Mark all read.|Reload.','Only intended user receives event; unread count and persisted read state are correct.',NOTICE)
    add('UI','NOTICE-02','Notifications','Follow notification after access revocation','Old notification targets a now-unreadable meeting.','Open bell.|Follow old notification.|Inspect target response/page.','Inaccessible target is handled without exposing restricted meeting content.',NOTICE)
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
    return rows
