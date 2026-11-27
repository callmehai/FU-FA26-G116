# QA fixtures — synthetic audio only

All audio in this folder is **machine-generated speech (TTS)** for QA runs.
No recording of a real person is stored here or used by the QA plans.

- `p0.mp3`, `p1.mp3`, `t.mp3`: Vietnamese TTS clips (edge-tts `vi-VN`, 24 kHz
  mono MP3) — short meeting-style sentences. `p1` pitched down becomes the
  second speaker.
- `said.wav`: a longer TTS reading for enrolment-style checks.
- `silence.wav`: generated silence for empty-audio cases.

Why TTS matters: under Vietnam's Personal Data Protection Law 91/2025/QH15,
a voice used to recognise someone is sensitive biometric data. This public
repo must never contain a real person's voice, so the QA plans build every
test sound from these synthetic clips with ffmpeg (see `tests/qa/plans/fe-10.mjs`).
