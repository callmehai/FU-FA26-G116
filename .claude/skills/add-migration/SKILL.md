---
name: add-migration
description: Create an EF Core migration for TrustCabinet after changing an entity or its IEntityTypeConfiguration. Use only when the user asks for a schema change or migration.
disable-model-invocation: true
---

# Add a migration

The database is code-first. Never change a schema by hand.

## Steps
1. Edit the entity in `backend/TrustCabinet.Domain` or its `IEntityTypeConfiguration` in `backend/TrustCabinet.Infrastructure`. Read `docs/02-design/02-erd.md` first.
2. Conventions to respect:
   - enums stored as strings
   - keys are `Guid.CreateVersion7()`
   - users are deactivated, never deleted
   - `MeetingStatusHistory` is append-only evidence: no update or delete
   - one organisation: no `OrgUnit*` properties (a test fails if they return)
3. Create the migration from the repository root:
   ```bash
   dotnet dotnet-ef migrations add <Name> --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir Migrations
   ```
4. Review the generated `Up` and `Down` and the `ModelSnapshot` diff. They must contain only what you intended. Commit the migration and the updated `ModelSnapshot` together.
5. **A migration that has reached `main` is immutable.** If `git log origin/main -- <file>` shows it there, do not edit it; correct it with a new migration.
6. In Development the app migrates and seeds itself at startup. Nothing outside Development ever does.
7. Update `docs/02-design/02-erd.md` and `docs/02-design/03-co-so-du-lieu.md` in the same commit.

## Verify
```bash
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
```
Then consider the `ef-migration-reviewer` agent.
