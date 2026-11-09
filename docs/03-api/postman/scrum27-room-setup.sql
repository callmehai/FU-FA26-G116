-- SCRUM-27 Postman setup: puts the seeded Chair, Secretary and Member of "[SCRUM-25] Điều hành chương trình"
-- (InProgress) in its online room, as LiveKit's participant_joined webhook would, and clears raised hands left
-- by an earlier run. Development database only; never run it on the test server.
--
--   docker exec -i trustcabinet-postgres psql -U trustcabinet -d <database> -v ON_ERROR_STOP=1 < docs/03-api/postman/scrum27-room-setup.sql
BEGIN;

UPDATE speaking_requests
SET status = CASE WHEN status = 'Speaking' THEN 'Finished' ELSE 'Cancelled' END, ended_at = now(), updated_at = now()
WHERE meeting_id = '25000000-0000-7000-8000-000000000002' AND status IN ('Queued', 'Invited', 'Speaking');

UPDATE meetings SET status = 'InProgress', actual_ended_at = NULL, updated_at = now()
WHERE id = '25000000-0000-7000-8000-000000000002';

INSERT INTO call_sessions (id, kind, meeting_id, status, started_at, created_at, updated_at)
SELECT gen_random_uuid(), 'MeetingRoom', '25000000-0000-7000-8000-000000000002', 'Active', now(), now(), now()
WHERE NOT EXISTS (SELECT 1 FROM call_sessions WHERE meeting_id = '25000000-0000-7000-8000-000000000002'
    AND kind = 'MeetingRoom' AND status = 'Active');

INSERT INTO call_participants (id, call_session_id, user_id, joined_at, declined, created_at, updated_at)
SELECT gen_random_uuid(), s.id, u.id, now(), false, now(), now()
FROM call_sessions s
CROSS JOIN users u
WHERE s.meeting_id = '25000000-0000-7000-8000-000000000002' AND s.kind = 'MeetingRoom' AND s.status = 'Active'
  AND u.email IN ('chutoa@trustcabinet.local', 'thuky@trustcabinet.local', 'thanhvien@trustcabinet.local')
  AND NOT EXISTS (SELECT 1 FROM call_participants p WHERE p.call_session_id = s.id AND p.user_id = u.id AND p.left_at IS NULL);

COMMIT;
