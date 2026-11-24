"""How far a transcription has got, as one share of the whole run that only moves forward.

A run is a fixed series of stages (pipeline.Engine._transcribe). Each stage reports how much of its own work is done; this
module turns that into a share of the whole run, weighted by how long each stage takes, so the backend can show the
Secretary a progress bar rather than a spinner for the 25 minutes an 80-minute room recording takes.
"""

from __future__ import annotations

from typing import Callable, Sequence

# Share of a run's time each stage takes, measured on 12 minutes of Vietnamese TV news (MacBook M1, 4 threads, 77 s a
# run, RTF 0.11). Diarization costs more than recognition: it embeds a 3 s window every 1.5 s of speech. Timed stage by
# stage it took 45–53% of a run and recognition 32–39%. Under the weights below the bar, read at every tenth of the
# run's time, stayed within 7 points of the share of time gone by (with 52/32 it ran up to 8 ahead).
# They only need to be roughly right: the bar moves within a stage as that stage reports, and an error here only makes
# it go a little faster or slower from one stage to the next.
STAGE_WEIGHTS: dict[str, float] = {
    "decode": 0.01,      # ffmpeg to 16 kHz mono
    "detect": 0.04,      # levelling and the VAD over the whole recording
    "diarize": 0.45,     # the window embeddings, clustering, the second pass
    "recognise": 0.39,   # the recogniser over every piece, one at a time (8 at a time when measured)
    "punctuate": 0.05,   # the tagger over each passage
    "voices": 0.06,      # one embedding per speaker, from up to 30 s of their speech
}
# The stages a run without diarization goes through: their weights are scaled up to fill the whole.
WITHOUT_DIARIZATION = ("decode", "detect", "recognise", "punctuate")


def stages_for(diarize: bool) -> tuple[str, ...]:
    return tuple(STAGE_WEIGHTS) if diarize else WITHOUT_DIARIZATION


class Tracker:
    """Turns (stage, fraction of that stage) into a share of the whole run and passes it on when it moved.

    The share never goes back: a stage reported out of order, or a fraction lower than one already reported, is ignored.
    It is passed on at most once per thousandth, so a stage may report as often as it likes.
    """

    def __init__(self, report: Callable[[float], None], stages: Sequence[str]) -> None:
        total = sum(STAGE_WEIGHTS[stage] for stage in stages)
        self._report = report
        self._spans: dict[str, tuple[float, float]] = {}
        start = 0.0
        for stage in stages:
            width = STAGE_WEIGHTS[stage] / total
            self._spans[stage] = (start, width)
            start += width
        self._sent = -1

    def __call__(self, stage: str, fraction: float) -> None:
        span = self._spans.get(stage)
        if span is None:
            return
        start, width = span
        share = start + width * min(1.0, max(0.0, fraction))
        permille = int(share * 1000)
        if permille <= self._sent:
            return
        self._sent = permille
        self._report(permille / 1000)


def ignore(stage: str, fraction: float) -> None:
    """The tracker of a call nobody follows (an enrolment sample, a unit test)."""
