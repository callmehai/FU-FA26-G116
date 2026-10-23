"""The pinned model files: where they come from and what their bytes must hash to.

Shared by fetch_models.py (download at image build time) and the server (verify at boot), so a
model swapped on disk — by accident or on purpose — stops the worker instead of silently changing
what every transcript is made of.
"""

from __future__ import annotations

import hashlib
import os
from dataclasses import dataclass
from pathlib import Path

MANIFEST = Path(__file__).resolve().parent.parent / "models.manifest"
DEFAULT_MODELS_DIR = Path.home() / ".cache" / "trustcabinet-speech" / "models"


@dataclass(frozen=True)
class ModelFile:
    path: str
    sha256: str
    url: str

    @property
    def archive_member(self) -> str | None:
        """The file to extract when the URL points at a .tar.bz2, written as `<url>#<member>`."""
        return self.url.split("#", 1)[1] if "#" in self.url else None

    @property
    def download_url(self) -> str:
        return self.url.split("#", 1)[0]


def models_dir() -> Path:
    return Path(os.environ.get("SPEECH_MODELS_DIR") or DEFAULT_MODELS_DIR).expanduser()


def load(manifest: Path = MANIFEST) -> list[ModelFile]:
    files = []
    for number, line in enumerate(manifest.read_text(encoding="utf-8").splitlines(), start=1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        if len(parts) != 3 or len(parts[1]) != 64:
            raise ValueError(f"{manifest}:{number}: expected '<path> <sha256> <url>'")
        files.append(ModelFile(*parts))
    return files


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def verify(root: Path, files: list[ModelFile] | None = None) -> dict[str, str]:
    """Check every model file against its pinned hash; returns {relative path: sha256}.

    Raises with the list of every missing or mismatched file, so one run tells the operator all
    that is wrong rather than one file at a time.
    """
    problems = []
    hashes = {}
    for model in files if files is not None else load():
        path = root / model.path
        if not path.is_file():
            problems.append(f"missing {path}")
            continue
        actual = sha256_of(path)
        if actual != model.sha256:
            problems.append(f"sha256 mismatch {path}: {actual} != {model.sha256}")
            continue
        hashes[model.path] = actual
    if problems:
        raise RuntimeError(
            "Speech models are missing or altered — run fetch_models.py. " + "; ".join(problems)
        )
    return hashes
