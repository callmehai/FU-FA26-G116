# PersonalLibrary.CreateFolder — Unit (FE-07, DOC)

Description: Personal library folders: create, rename, move, delete with name and cycle rules.

Precondition: Owner with a folder tree.

## PersonalLibraryService.CreateFolderAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:107`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Owner U1 with an empty library. | parentId: null; name: "Hồ sơ 2026". | Return: Value: folder row (name "Hồ sơ 2026", ParentId null, MeetingId null); endpoint answers MSG187 with HTTP 201.<br>Exception: N/A<br>Log Message: Folder saved under the owner lock. | Normal | UC-72a · BR-220 · MSG187 |
| UTC002 | Folder F1 exists at the root. | parentId: F1; name: "  Biên bản  ". | Return: Value with name "Biên bản" (trimmed) and ParentId F1.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-72a · BR-220 · MSG187 |
| UTC003 | A folder named "Biên bản" already exists at the same level. | parentId: null; name: "Biên bản". | Return: Value; duplicate names are allowed.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · BR-221 · MSG187 |
| UTC004 | Same. | parentId: F9 (not owner's); name: "A". | Return: Failure DestinationNotFound (HTTP 404, MSG179).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72a · BR-242 · MSG179 |
| UTC005 | Same. | name: null. | Return: Failure NameRequired (HTTP 400, MSG188).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72a · BR-220 · MSG188 |
| UTC006 | Same. | name: "    ". | Return: Failure NameRequired (HTTP 400, MSG188).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72a · BR-220 · MSG188 |
| UTC007 | Same. | name: 400 characters. | Return: Value; 400 characters is accepted.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · MSG187 |
| UTC008 | Same. | name: 401 characters. | Return: Failure NameTooLong (HTTP 400, MSG348).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · MSG348 |
| UTC009 | Parent chain: F1 > F2 > F3 > F4 (parent F4 is at level 4). | parentId: F4; name: "Cấp 5". | Return: Value; the new folder is at level 5, the allowed maximum.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · BR-191 · MSG187 |
| UTC010 | Parent chain of five levels F1 > ... > F5. | parentId: F5; name: "Cấp 6". | Return: Failure TooDeep (HTTP 409, MSG189).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · BR-191 · MSG189 |
| UTC011 | Parent missing and name blank. | parentId: F9; name: "". | Return: Failure DestinationNotFound (MSG179); the parent is checked before the name.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · MSG179 |
| UTC012 | Database error. | parentId: null; name: "A". | Return: Failure SaveFailed (HTTP 503, MSG190); no folder created.<br>Exception: N/A<br>Log Message: Error log "Failed to create a folder". | Abnormal | UC-72a · MSG190 |
