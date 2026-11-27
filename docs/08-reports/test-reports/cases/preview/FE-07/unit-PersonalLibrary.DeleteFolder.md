# PersonalLibrary.DeleteFolder — Unit (FE-07, DOC)

Description: Personal library folders: create, rename, move, delete with name and cycle rules.

Precondition: Owner with a folder tree.

## PersonalLibraryService.DeleteFolderAsync

Layer: Application · `backend/TrustCabinet.Application/Documents/PersonalLibraryService.cs:156`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Folder F1 is empty (no subfolder, no document). | folderId: F1. | Return: None; endpoint answers MSG197.<br>Exception: N/A<br>Log Message: Folder row deleted. | Normal | UC-41d · BR-084 · MSG197 |
| UTC002 | F9 missing or another user's. | folderId: F9. | Return: NotFound (HTTP 404, MSG175).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41d · BR-083 · MSG175 |
| UTC003 | F1 has a subfolder. | folderId: F1. | Return: NotEmpty (HTTP 409, MSG196); nothing deleted.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-41d · BR-084 · MSG196 |
| UTC004 | F2 is the upload folder of a meeting and is empty. | folderId: F2. | Return: None; the next upload to that meeting creates a new upload folder.<br>Exception: N/A<br>Log Message: Folder row deleted. | Boundary | UC-41d · UC-36a · MSG197 |
