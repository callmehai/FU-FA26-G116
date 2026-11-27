# Agenda.Update — Unit (FE-05, AGENDA)

Description: Updating an agenda item: a planner edits title, description, presenter or documents.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:105`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I "Old" Pending. | actor: S; itemId: I; input {Title: "New"; Description: ""}. | Return: Success; 200 / MSG61.<br>Exception: N/A<br>Log Message: Title changed, UpdatedAt set; agenda change "Updated" published. | Normal | UC-23b · MSG61 |
| UTC002 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I exists. | actor: S; itemId: I; input {Title: ""}. | Return: Failure Invalid on title; 400 / MSG66.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23b · UC-23a, UC-23b · MSG66 |
| UTC003 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I exists. | actor: Member M. | Return: Failure NotFound; 404, empty body.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23b · BR-058, BR-059 · MSG67 |
| UTC004 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Item I exists. | actor: S; the database fails while saving. | Return: Failure SaveFailed; 503 / MSG69.<br>Exception: N/A<br>Log Message: Error logged; no event. | Abnormal | UC-23b · MSG69 |
