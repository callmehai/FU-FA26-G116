# RoomTake.Append — Unit (FE-09, REC)

Description: Room take lifecycle: begin, append, complete, complete idle, delete.

Precondition: Meeting in progress; storage recorder.

## RoomTakeService.AppendAsync

Layer: Application · `backend/TrustCabinet.Application/Recordings/RoomTakeService.cs:95`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB.<br>3. The take is empty. | part 1; 320,000 bytes starting with a valid 44-byte 16 kHz mono 16-bit WAV header; length 320,000; final false. | Return: Success (204) with the take id: file written at offset 0, ByteSize 320,000, DurationMs 9,998.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-44a · BR-345 · MSG361 |
| UTC002 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB. | part 1 final with 0 bytes. | Return: BadPart (400, MSG361); the first part must open with the WAV header.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44a · BR-345 · MSG361 |
| UTC003 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB. | part 2; declared length 100,000; final false. | Return: BadPart (400, MSG361); only the final part may be shorter.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44a · BR-345 · MSG361 |
| UTC004 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB.<br>3. ByteSize is 640,000. | part 4; 320,000 bytes. | Return: BadPart (400, MSG361); part 3 is missing, the file must have no hole.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44a · BR-345 · MSG361 |
| UTC005 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB.<br>3. ByteSize is 314,560,000 (983 full parts). | part 984; 320,000 bytes. | Return: TooLarge (413, MSG293); the take would pass 314,572,800 bytes.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44a · BR-345 · MSG361 · MSG293 |
| UTC006 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB.<br>3. ByteSize is 314,560,000. | part 984 as final with 12,800 bytes. | Return: Success; the take reaches exactly 314,572,800 bytes, the limit.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-44a · BR-345 · MSG361 |
| UTC007 | The take is already saved (status Available). | part 2; 320,000 bytes. | Return: TakeClosed (409, MSG362).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-44a · BR-345 · MSG361 · MSG362 |
| UTC008 | 1. A Pending room take made by the actor (the Secretary) in an InPerson meeting that is InProgress.<br>2. Part size 320,000 bytes; upload limit 300 MB.<br>3. The disk fails on writing. | part 1 with a valid header. | Return: SaveFailed (503, MSG298).<br>Exception: IOException or PersistenceException caught; logged.<br>Log Message: Error log 'Failed to write part'. | Abnormal | UC-44a · BR-345 · MSG361 · MSG298 |
