# EX cuối cùng — nhóm tai-lieu

Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.

## UC32c_PreviewMeetingDocument
- EX-1 (MSG156): Document is no longer accessible
- EX-2 (MSG159): Download requested for a Secret Meeting
- EX-3 (MSG382): The stored file no longer exists

## UC33a_AddDocumentsFromLibrary
- EX-1 (MSG162): Actor is no longer the Chair or Secretary
- EX-2 (MSG249): Selection is empty, unavailable or already attached
- EX-3 (MSG172): Meeting no longer accepts documents

## UC33a_UploadDocumentsToMeeting
- EX-1 (MSG162): Actor is no longer the Chair or Secretary
- EX-2 (MSG164): Any file is invalid or over the size limit
- EX-3 (MSG172): Meeting no longer accepts documents

## UC33b_AttachDocumentsToAgendaItem
- EX-1 (MSG240): Actor is no longer the Chair or Secretary
- EX-2 (MSG170): Agenda Item or a document is no longer in the Meeting, or already linked
- EX-3 (MSG247): Meeting no longer accepts document links

## UC33c_RemoveDocumentFromMeeting
- EX-1 (MSG169): Actor is no longer the Chair or Secretary
- EX-2 (MSG170): Document is no longer attached
- EX-3 (MSG248): Meeting no longer accepts changes

## UC33d_DetachDocumentFromAgendaItem
- EX-1 (MSG241): Actor is no longer the Chair or Secretary
- EX-2 (MSG246): Document, Agenda Item or link no longer exists
- EX-3 (MSG247): Meeting no longer accepts changes

## UC34_ViewPersonalLibrary
- EX-1 (MSG175): Selected folder is no longer available

## UC34b_ViewLockedAccountLibrary
- EX-1 (MSG175): Account is missing or is not locked

## UC35_CommentOnMeetingDocument
- EX-1 (MSG200): Comment is empty or too long
- EX-2 (MSG156): Document is no longer available
- EX-3 (MSG251): Meeting no longer accepts comments

## UC36_UploadToPersonalLibrary
- EX-1 (MSG164): Any file is invalid or over the size limit
- EX-2 (MSG179): Destination folder is no longer available
- EX-3 (MSG346): System storage is full

## UC71a_DeleteLibraryDocument
- EX-1 (MSG175): Document is missing or no longer belongs to the actor

## UC71b_RenameLibraryDocument
- EX-1 (MSG175): Document is missing or no longer belongs to the actor
- EX-2 (MSG347): Document name is invalid

## UC71c_MoveLibraryDocument
- EX-1 (MSG175): Document is missing or no longer belongs to the actor
- EX-2 (MSG179): Destination is missing or does not belong to the actor

## UC72a_CreateFolder
- EX-1 (MSG188): Folder name is blank or too long
- EX-2 (MSG179): Parent folder is missing or not owned by the user
- EX-3 (MSG189): New folder would be deeper than level five

## UC72b_RenameFolder
- EX-1 (MSG175): Folder is missing or not owned by the user
- EX-2 (MSG188): Folder name is blank
- EX-3 (MSG348): Folder name exceeds 400 characters

## UC72c_MoveFolder
- EX-1 (MSG179): Folder or destination is missing or not owned by the user
- EX-2 (MSG193): Destination is the folder itself or one of its subfolders
- EX-3 (MSG189): Moved folders would be deeper than level five

## UC72d_DeleteFolder
- EX-1 (MSG175): Folder is missing or not owned by the user
- EX-2 (MSG196): Folder contains a document or subfolder

## UC74_ConfirmDocumentRead
- EX-1 (MSG254): Last page not reached
- EX-2 (MSG156): Document or Meeting is no longer accessible
- EX-3 (MSG224): Not an eligible voter, or no Open Vote remains
