# Agenda.Normalize — Unit (FE-05, AGENDA)

Description: Input normalization: trimming the title and defaulting the description.

Precondition: No database needed; pure rules.

## AgendaRules.Normalize

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:26`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Title has surrounding spaces. | input {Title: "  Opening  "; Description: "Notes"}. | Return: {Title: "Opening"; Description: "Notes"}.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23a · BR-66 |
| UTC002 | Title is null and description is null. | input {Title: null; Description: null}. | Return: {Title: ""; Description: ""}.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 · MSG66 |
| UTC003 | Description keeps its own spacing. | input {Title: "A"; Description: "  line  "}. | Return: Description is left as "  line  " (not trimmed).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23a · BR-66 |
