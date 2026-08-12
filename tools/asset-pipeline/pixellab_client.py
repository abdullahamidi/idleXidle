"""Minimal PixelLab v2 REST client — stdlib only, no third-party deps.

The MCP server is only reachable from an interactive Claude session; a hook or CI
run needs the plain HTTP API, so the pipeline talks to that directly.

Auth token is read from the PIXELLAB_API_TOKEN environment variable and is never
written to disk or into the repo.
"""

from __future__ import annotations

import base64
import json
import os
import time
import urllib.error
import urllib.request

BASE_URL = os.environ.get("PIXELLAB_BASE_URL", "https://api.pixellab.ai/v2")
TOKEN_ENV = "PIXELLAB_API_TOKEN"

# The API rejects new jobs past 8 in flight, and counts recently-finished jobs
# toward that ceiling for a short while — so the pipeline stays a couple under.
MAX_IN_FLIGHT = 2


class PixelLabError(RuntimeError):
    pass


class MissingToken(PixelLabError):
    pass


ENV_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".env")


def _token() -> str:
    """Token from the environment, falling back to the gitignored .env file.

    The environment wins so CI can inject a different token without editing
    files. `.env` is listed in .gitignore precisely so the secret never reaches
    a commit — do not move this value into any tracked file.
    """
    tok = os.environ.get(TOKEN_ENV, "").strip()
    if not tok and os.path.exists(ENV_FILE):
        with open(ENV_FILE, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line.startswith("#") or "=" not in line:
                    continue
                name, _, value = line.partition("=")
                if name.strip() == TOKEN_ENV:
                    tok = value.strip().strip("'\"")
                    break
    if not tok:
        raise MissingToken(
            f"{TOKEN_ENV} is not set and {ENV_FILE} has no token. Either:\n"
            f"  export {TOKEN_ENV}='<your-token>'\n"
            f"  or put {TOKEN_ENV}=<your-token> in {ENV_FILE}"
        )
    return tok


# 429 = concurrent-job ceiling, 529 = service overloaded. Both are transient and
# the only sane response is to wait and retry; the generation endpoint is
# synchronous, so a slot frees up as soon as some other request finishes.
RETRY_STATUS = (429, 529)
MAX_RETRIES = 8
# Kept short on purpose. A 429 here usually means "too many jobs in flight right
# now", which clears in seconds. Long backoff was actively harmful: status polls
# for jobs that had ALREADY finished sat in a 60s sleep, so workers idled while
# the server had their results waiting.
BACKOFF_BASE = 1.5
BACKOFF_CAP = 8.0


def _request(method: str, path: str, body: dict | None = None, raw: bool = False):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None

    last_detail = ""
    for attempt in range(MAX_RETRIES):
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Authorization", f"Bearer {_token()}")
        if data:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                payload = resp.read()
            return payload if raw else json.loads(payload or b"{}")
        except urllib.error.HTTPError as exc:
            last_detail = exc.read().decode("utf-8", "replace")[:300]
            if exc.code not in RETRY_STATUS or attempt == MAX_RETRIES - 1:
                raise PixelLabError(f"{method} {path} -> HTTP {exc.code}: {last_detail}") from exc
            # Jitter by attempt index so parallel workers do not resynchronise
            # and collide again on the same free slot.
            delay = min(BACKOFF_CAP, BACKOFF_BASE * (2 ** attempt))
            time.sleep(delay + (attempt % 3))
        except urllib.error.URLError as exc:
            if attempt == MAX_RETRIES - 1:
                raise PixelLabError(f"{method} {path} -> {exc.reason}") from exc
            time.sleep(min(BACKOFF_CAP, BACKOFF_BASE * (2 ** attempt)))
    raise PixelLabError(f"{method} {path} -> exhausted retries: {last_detail}")


def create_image(
    description: str,
    width: int = 256,
    height: int = 256,
    *,
    no_background: bool = True,
    outline: str | None = None,
    shading: str | None = None,
    detail: str | None = None,
    text_guidance_scale: float | None = None,
    negative_description: str | None = None,
    seed: int | None = None,
) -> bytes:
    """Generate a pixflux image. Returns raw PNG bytes.

    This endpoint is SYNCHRONOUS — it blocks and returns the image inline as
    base64, with no job id and nothing to poll. Size goes in a nested
    ``image_size`` object; passing width/height at the top level is silently
    ignored and the request fails validation.
    """
    body: dict = {
        "description": description,
        "image_size": {"width": width, "height": height},
        "no_background": bool(no_background),
    }
    for name, value in (
        ("outline", outline),
        ("shading", shading),
        ("detail", detail),
        ("text_guidance_scale", text_guidance_scale),
        ("negative_description", negative_description),
        ("seed", seed),
    ):
        if value is not None:
            body[name] = value

    result = _request("POST", "/create-image-pixflux", body)
    image = result.get("image") or {}
    b64 = image.get("base64")
    if not b64:
        raise PixelLabError(f"no image in response: {json.dumps(result)[:300]}")
    payload = base64.b64decode(b64)
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        raise PixelLabError(f"decoded payload was not a PNG ({len(payload)} bytes)")
    return payload


def create_image_pixen(
    description: str,
    width: int = 640,
    height: int = 360,
    *,
    no_background: bool = False,
    outline: str | None = None,
    detail: str | None = None,
    view: str | None = None,
    seed: int | None = None,
) -> bytes:
    """Generate via the pixen model. Returns raw PNG bytes.

    Pixen allows up to 768 per side versus pixflux's 400, which is what makes
    full-screen backgrounds viable: authoring at 640x360 and upscaling exactly
    3x lands on 1920x1080 with no resampling blur at all.

    Also synchronous. Note ``no_background`` defaults False here — backgrounds
    are opaque scenes, not cut-out sprites.
    """
    body: dict = {
        "description": description,
        "image_size": {"width": width, "height": height},
        "no_background": bool(no_background),
    }
    for name, value in (("outline", outline), ("detail", detail), ("view", view), ("seed", seed)):
        if value is not None:
            body[name] = value

    result = _request("POST", "/create-image-pixen", body)
    image = result.get("image") or {}
    b64 = image.get("base64")
    if not b64:
        raise PixelLabError(f"no image in response: {json.dumps(result)[:300]}")
    payload = base64.b64decode(b64)
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        raise PixelLabError(f"decoded payload was not a PNG ({len(payload)} bytes)")
    return payload


def inpaint(
    image_b64: str,
    mask_b64: str,
    description: str,
    width: int,
    height: int,
    *,
    outline: str | None = None,
    shading: str | None = None,
    detail: str | None = None,
    negative_description: str | None = None,
    text_guidance_scale: float | None = None,
    color_b64: str | None = None,
    seed: int | None = None,
) -> bytes:
    """Repaint the WHITE area of ``mask_b64`` inside ``image_b64``. Returns raw PNG bytes.

    This is how worn equipment is authored: the armour is painted ONTO the character, in the
    character's own pose, lighting and line weight, instead of being drawn as an isolated icon and
    pasted on at some scale. An icon can never match — it carries its own perspective and its own
    light — which is exactly what made worn gear read as stickers.

    /inpaint (v2), because /inpaint-v3 answers "Tier 2 is required for this" on this subscription.
    v2 is synchronous and takes the same style controls as the rest of the pipeline, but it caps each
    side at 200px — which is why a slot covering two limbs is repainted as two regions rather than
    one wide one.
    """
    body: dict = {
        "description": description,
        "image_size": {"width": width, "height": height},
        "inpainting_image": {"type": "base64", "base64": image_b64},
        "mask_image": {"type": "base64", "base64": mask_b64},
        "no_background": True,
    }
    for name, value in (
        ("outline", outline),
        ("shading", shading),
        ("detail", detail),
        ("negative_description", negative_description),
        ("text_guidance_scale", text_guidance_scale),
        ("seed", seed),
    ):
        if value is not None:
            body[name] = value
    if color_b64 is not None:
        # A forced palette. Without it the model matches the surrounding figure's muted browns and a
        # "deep violet arcane metal" breastplate comes back as the same leather it was replacing —
        # the material was the one thing the prompt could not make stick.
        body["color_image"] = {"type": "base64", "base64": color_b64}

    result = _request("POST", "/inpaint", body)
    image = result.get("image") or {}
    b64 = image.get("base64")
    if not b64:
        raise PixelLabError(f"no image in response: {json.dumps(result)[:300]}")
    payload = base64.b64decode(b64)
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        raise PixelLabError(f"decoded payload was not a PNG ({len(payload)} bytes)")
    return payload


def save_image(payload: bytes, dest_path: str) -> str:
    with open(dest_path, "wb") as fh:
        fh.write(payload)
    return dest_path


def balance() -> dict:
    return _request("GET", "/balance")
