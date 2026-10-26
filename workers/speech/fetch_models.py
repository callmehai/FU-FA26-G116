"""Download the pinned speech models into $SPEECH_MODELS_DIR and verify their sha256.

Run once on a dev machine, and at image build time in the Dockerfile so the image ships with the
models baked in. Files already present with the right hash are skipped. Exits non-zero when a
download does not match its pinned hash — a changed upstream file is a decision, not an update.
"""

from __future__ import annotations

import sys
import tarfile
import tempfile
import urllib.request
from pathlib import Path

from speech import manifest


def download(url: str, target: Path) -> None:
    request = urllib.request.Request(url, headers={"User-Agent": "trustcabinet-speech/1"})
    with urllib.request.urlopen(request, timeout=120) as response, target.open("wb") as out:
        while block := response.read(1 << 20):
            out.write(block)


def fetch(model: manifest.ModelFile, root: Path) -> None:
    target = root / model.path
    if target.is_file() and manifest.sha256_of(target) == model.sha256:
        print(f"ok       {model.path}")
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=root) as scratch:
        downloaded = Path(scratch) / "download"
        print(f"download {model.path} <- {model.download_url}")
        download(model.download_url, downloaded)
        if model.archive_member:
            with tarfile.open(downloaded, "r:bz2") as archive:
                member = archive.getmember(model.archive_member)
                source = archive.extractfile(member)
                if source is None:
                    raise RuntimeError(f"{model.archive_member} is not a file in the archive")
                extracted = Path(scratch) / "extracted"
                with extracted.open("wb") as out:
                    while block := source.read(1 << 20):
                        out.write(block)
                downloaded = extracted
        actual = manifest.sha256_of(downloaded)
        if actual != model.sha256:
            raise RuntimeError(f"sha256 mismatch for {model.path}: got {actual}, pinned {model.sha256}")
        downloaded.replace(target)
    print(f"fetched  {model.path}")


def main() -> int:
    root = manifest.models_dir()
    root.mkdir(parents=True, exist_ok=True)
    try:
        for model in manifest.load():
            fetch(model, root)
    except Exception as error:  # noqa: BLE001 — report and fail the build
        print(f"error: {error}", file=sys.stderr)
        return 1
    print(f"all models verified in {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
