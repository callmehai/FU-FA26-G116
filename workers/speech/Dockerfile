# Speech worker: Vietnamese ASR, speaker diarization and voice embeddings on CPU (sherpa-onnx).
# The models are downloaded and hash-checked at build time, so the running container needs no
# internet access and cannot pick up a different model than the one pinned in models.manifest.
FROM python:3.12.14-slim-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

COPY requirements.txt ./
RUN pip install -r requirements.txt

# Models before the code, so editing the code does not download ~110 MB again.
COPY models.manifest fetch_models.py ./
COPY speech/__init__.py speech/manifest.py speech/
RUN SPEECH_MODELS_DIR=/models python fetch_models.py

COPY speech/ speech/
COPY selfcheck.py ./

RUN useradd --uid 10001 --no-create-home --shell /usr/sbin/nologin speech
USER speech

ENV SPEECH_HOST=0.0.0.0 \
    SPEECH_PORT=8090 \
    SPEECH_MODELS_DIR=/models
EXPOSE 8090
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD ["python", "-c", "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8090/health', timeout=4)"]
CMD ["python", "-m", "speech"]
