# Agenda.Create — Unit (FE-05, AGENDA)

Description: Creating an agenda item: a planner adds a titled item while planning is allowed.

Precondition: Meeting in a planning or running status; actor with the needed role.

## AgendaService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Agenda/AgendaService.cs:103`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Meeting has no agenda items. | actor: Secretary S; input {Title: "Opening"}. | Return: Success; 201 / MSG60; new item id returned.<br>Exception: N/A<br>Log Message: Item saved Pending at position 1 with empty description and no presenter; agenda change "Created" published; no presenter notice. | Normal | UC-23a · BR-65 · BR-66 · BR-67 · MSG60 |
| UTC002 | Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster. | actor: S; input {Title: 301 characters}. | Return: Failure Invalid on title; 400 / MSG66.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC003 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. Actor is the organizer without a roster seat. | actor: organizer; input {Title: "A"}. | Return: Failure Forbidden; 403 / MSG67.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · BR-65 · MSG67 |
| UTC004 | Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster. | actor: S; input.ParseFailure: UnsupportedMedia. | Return: Failure UnsupportedMedia; 415 / MSG34; errors {body}.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-23a · MSG34 |
| UTC005 | Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster. | actor: S; input.ParseFailure: TooLarge. | Return: Failure TooLarge; 413 / MSG87.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · MSG87 |
| UTC006 | 1. Meeting in status Draft; active Chair C, active Secretary S, active Member M on the roster.<br>2. An existing item already holds position 2147483647. | actor: S; input {Title: "A"}. | Return: Failure SaveFailed; 503 / MSG68.<br>Exception: N/A<br>Log Message: Nothing saved. | Boundary | UC-23a · MSG68 |
