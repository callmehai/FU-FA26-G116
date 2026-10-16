-- AI-assisted: read-only checks matching the SCRUM-20 migration preflight.
\set ON_ERROR_STOP on
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '30s';
SELECT 'legacy_guest' AS check_name, count(*) AS blockers
FROM meeting_participants WHERE role = 'Guest'
UNION ALL
SELECT 'delegation', count(*) FROM meeting_participants
WHERE response = 'Delegated' OR delegated_to_user_id IS NOT NULL
UNION ALL
SELECT 'multiple_chairs', count(*) FROM (
    SELECT meeting_id FROM meeting_participants WHERE role = 'Chair'
    GROUP BY meeting_id HAVING count(*) > 1
) AS invalid_meetings;
SELECT EXISTS (
    SELECT 1 FROM meeting_participants
    WHERE role = 'Guest' OR response = 'Delegated' OR delegated_to_user_id IS NOT NULL
) OR EXISTS (
    SELECT meeting_id FROM meeting_participants WHERE role = 'Chair'
    GROUP BY meeting_id HAVING count(*) > 1
) AS blocked \gset
COMMIT;
\if :blocked
    \echo 'BLOCKED: Do not merge; review legacy data mapping without deleting evidence.'
    -- psql 17 ignores an exit-code argument to \quit. ON_ERROR_STOP yields exit 3 here.
    SELECT 1 / 0 AS migration_blocked;
\else
    \echo 'PASS: No SCRUM-20 migration blockers in this database snapshot.'
\endif
