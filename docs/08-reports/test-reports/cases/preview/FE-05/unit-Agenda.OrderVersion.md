# Agenda.OrderVersion — Unit (FE-05, AGENDA)

Description: Order-version rule: the hash identifying the current item order.

Precondition: No database needed; pure rules.

## AgendaRules.OrderVersion

Layer: Domain · `backend/TrustCabinet.Domain/Agenda/AgendaRules.cs:45`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting has no agenda items. | items: []. | Return: SHA-256 of the empty text: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23d · BR-64 · MSG143 |
| UTC002 | One Pending item at position 1. | items: [{id: aaaaaaaa-0000-0000-0000-000000000001; position: 1; status: Pending}]. | Return: SHA-256 of "aaaaaaaa-0000-0000-0000-000000000001:1:Pending": 3fd739a99fdff6a5bd0d80ce84ce251c69aad806ded66b37ffb1d2bea9fde669.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23d · BR-64 |
| UTC003 | Two Pending items at positions 1 and 2. | items: [{aaaaaaaa-0000-0000-0000-000000000001, 1, Pending}, {aaaaaaaa-0000-0000-0000-000000000002, 2, Pending}]. | Return: SHA-256 of both lines joined by a line break: 9d4314cd6201dff90158397739a4dd919b61d12d26772ff7ac2163ee7d7b849d.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-23d · BR-64 |
| UTC004 | Two items share a position. | items: [{aaaaaaaa-0000-0000-0000-000000000002, 1, Pending}, {aaaaaaaa-0000-0000-0000-000000000001, 1, Pending}]. | Return: 7e2852961818f17cfe13b6ae7abc5f4ec6b0b8e666bf2128f14992358a1c4f0f; ties are ordered by id.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-23d · BR-64 |
