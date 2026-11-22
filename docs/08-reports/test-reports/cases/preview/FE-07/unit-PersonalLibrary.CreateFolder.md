# PersonalLibrary.CreateFolder — Unit (FE-07, DOC)

Description: Personal library folders: create, rename, move, delete with name and cycle rules.

Precondition: Owner with a folder tree.

## PersonalLibraryService.CreateFolderAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:107`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Owner U1 with an empty library. | parentId: null; name: "Hồ sơ 2026". | Return: Value: folder row (name "Hồ sơ 2026", ParentId null, MeetingId null); endpoint answers MSG187 with HTTP 201.<br>Exception: N/A<br>Log Message: Folder saved under the owner lock. | Normal | UC-72a · BR-220 · MSG187 |
| UTC002 | Same. | parentId: F9 (not owner's); name: "A". | Return: Failure DestinationNotFound (HTTP 404, MSG179).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72a · BR-242 · MSG179 |
| UTC003 | Same. | name: null. | Return: Failure NameRequired (HTTP 400, MSG188).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72a · BR-220 · MSG188 |
| UTC004 | Same. | name: 401 characters. | Return: Failure NameTooLong (HTTP 400, MSG348).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · MSG348 |
| UTC005 | Parent chain: F1 > F2 > F3 > F4 (parent F4 is at level 4). | parentId: F4; name: "Cấp 5". | Return: Value; the new folder is at level 5, the allowed maximum.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · BR-191 · MSG187 |
| UTC006 | Parent chain of five levels F1 > ... > F5. | parentId: F5; name: "Cấp 6". | Return: Failure TooDeep (HTTP 409, MSG189).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-72a · BR-191 · MSG189 |
