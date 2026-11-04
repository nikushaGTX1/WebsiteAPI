"""Headless Velven image-processing service on Modal.

Florence-2 detects watermarks and reads their text. LaMa inpaints only when the
caller has authority to modify the photo. Third-party watermarks are reported,
never removed, unless the caller states a licence with a permission reference.

Inference code comes from D-Ogi/WatermarkRemover-AI (MIT, commit
127108a5bff1b880ad34ba26ac8b60710b69bb3b). See vendor/licenses.

Deploy:   py -m modal deploy velven_image_ai.py
Models:   py -m modal run velven_image_ai.py::prepare_models
Benchmark py -m modal run velven_image_ai.py::benchmark --folder <dir>
"""


import base64
import hashlib
import hmac
import io
import json
import os
import time
from pathlib import Path

import modal

APP_NAME = "velven-image-ai"
MODEL_DIR = "/models"
FLORENCE_REPO = "florence-community/Florence-2-large"
FLORENCE_REVISION = "4271c66b88cdbc05735372ec13b2360108de5317"
VENDOR = Path(__file__).parent / "vendor"

# T4 list price on Modal, used only for the per-call cost estimate in responses.
T4_USD_PER_SECOND = float(os.environ.get("T4_USD_PER_SECOND", "0.000164"))
MAX_IMAGE_BYTES = 10 * 1024 * 1024
MAX_PIXELS = 40_000_000

image = (
    modal.Image.debian_slim(python_version="3.12")
    .apt_install("libgl1", "libglib2.0-0")
    .pip_install(
        "torch>=2.14.0",
        "torchvision>=0.29.0",
        "transformers>=4.56.0,<5",
        "numpy>=2,<3",
        "opencv-python-headless>=5.0.0.93,<5.1.0",
        "Pillow>=10.0.0",
        "filelock>=3.16",
        "huggingface_hub",
        "fastapi[standard]",
    )
    .env({
        "HF_HOME": f"{MODEL_DIR}/hf",
        "TORCH_HOME": f"{MODEL_DIR}/torch",
        "HF_HUB_OFFLINE": "1",
    })
    .add_local_dir(VENDOR, "/root/vendor")
)

app = modal.App(APP_NAME, image=image)
models_volume = modal.Volume.from_name("velven-image-models", create_if_missing=True)
secret = modal.Secret.from_name("velven-image-ai")


# Largest share of the photo a removable (own or partner) mark may cover. Partner marks such as
# myhome.ge are often big and centred, larger than the 10% limit used for unknown marks.
MAX_REMOVABLE_BBOX_PERCENT = 25.0
# Extra prompts for faint marks the "watermark" prompt misses. Their boxes are only kept when
# the text in them is a removable brand, so ordinary logos in the photo (a TV, a sign) are left alone.
EXTRA_DETECTION_PROMPTS = ("logo", "text")


def normalize_mark_text(text: str) -> str:
    """Lowercase letters and digits only, with common OCR mix-ups fixed: "MY HOME.GE" -> "myhomege"."""
    text = (text or "").lower().translate(str.maketrans({"0": "o", "1": "l", "|": "l", "5": "s", "$": "s"}))
    return "".join(ch for ch in text if ch.isalnum())


def _close_enough(term: str, text: str) -> bool:
    """term appears in text with at most one wrong letter (for terms of 5+ letters)."""
    if term in text:
        return True
    if len(term) < 5:
        return False
    for start in range(0, max(1, len(text) - len(term) + 1)):
        window = text[start:start + len(term)]
        if len(window) == len(term) and sum(a != b for a, b in zip(window, term)) <= 1:
            return True
    return False


def mark_is_removable(text: str, terms: list[str]) -> bool:
    normalized = normalize_mark_text(text)
    return bool(normalized) and any(
        _close_enough(normalize_mark_text(term), normalized) for term in terms if normalize_mark_text(term))


def _is_unreadable(text: str) -> bool:
    """No real word read: an icon, a shape or OCR noise."""
    return len(normalize_mark_text(text)) < 3


def _is_unreadable_or_fragment(text: str, terms: list[str]) -> bool:
    """OCR noise, or a piece of a removable brand ("ge", "nome" from "myhome.ge")."""
    normalized = normalize_mark_text(text)
    if _is_unreadable(text):
        return True
    brands = [normalize_mark_text(t) for t in terms]
    def near_piece(piece: str, brand: str) -> bool:
        # One wrong letter is allowed in pieces of 4+ letters ("nome" for "home").
        return len(piece) >= 4 and any(
            sum(a != b for a, b in zip(piece, brand[i:i + len(piece)])) <= 1
            for i in range(0, len(brand) - len(piece) + 1))
    return any(normalized in b or near_piece(normalized, b) for b in brands if b)


def _is_piece_of(text: str, source: str | None) -> bool:
    """A 2+ letter piece of the known partner's name with its .ge ending ("ge", "home", "my")."""
    if not source:
        return False
    normalized = normalize_mark_text(text)
    brand = normalize_mark_text(source)
    form = brand if brand.endswith("ge") else brand + "ge"
    return len(normalized) >= 2 and normalized in form


def partner_read_matches(text: str, terms: list[str]) -> bool:
    """
    Looser reading for the partner search, which only looks where such logos sit (the middle of
    the photo or around a detected mark). Pale logos come out of OCR misspelled ("yone.ge",
    "yfone ge" for "myhome.ge"), so a close resemblance to a brand, or a 3+ letter piece of it,
    is enough here.
    """
    import difflib

    normalized = normalize_mark_text(text)
    if len(normalized) < 3:
        return False
    if mark_is_removable(text, terms):
        return True
    for term in terms:
        brand = normalize_mark_text(term)
        if not brand:
            continue
        forms = {brand, brand if brand.endswith("ge") else brand + "ge"}
        for form in forms:
            if len(normalized) >= 4 and difflib.SequenceMatcher(None, normalized, form).ratio() >= 0.6:
                return True
            if len(form) >= 5 and normalized in form and len(normalized) >= 3:
                return True
    return False


def _overlap_or_near(a: list[int], b: list[int]) -> bool:
    """Boxes touch, or are within one box-height of each other (a logo icon beside its text)."""
    gap = max(a[3] - a[1], b[3] - b[1])
    return not (a[2] + gap < b[0] or b[2] + gap < a[0] or a[3] + gap < b[1] or b[3] + gap < a[1])


def _iou(a: list[int], b: list[int]) -> float:
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    inter = max(0, x2 - x1) * max(0, y2 - y1)
    union = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / union if union else 0.0


def classify_marks(detections: list[dict], terms: list[str]) -> tuple[str, list[dict]]:
    """
    Returns (classification, boxes to remove).
    own_brand: at least one mark reads as Velven or a licensed partner and no mark clearly reads
    as another company. Unreadable boxes next to a removable mark (its icon) are removed with it.
    third_party: some mark clearly names another company.
    none: only unreadable shapes were found (plain photos were blocked by lamps and mirrors).
    """
    if not detections:
        return "none", []
    # Marks the partner search located already passed its (looser) brand check.
    removable = [d for d in detections
                 if d.get("partner_search") or mark_is_removable(d.get("text", ""), terms)]
    # Pieces of a removable brand ("nome" read from "myhome") are not another company's mark.
    other = [d for d in detections if d not in removable
             and not (_is_unreadable_or_fragment(d.get("text", ""), terms) if removable else _is_unreadable(d.get("text", "")))]
    # Nothing readable anywhere ("1", "T", "..."): a lamp, mirror or reflection, not a watermark.
    if not removable and not other:
        return "none", []
    if not removable or other:
        return "third_party", []
    icons = [d for d in detections if d not in removable and d.get("primary")
             and any(_overlap_or_near(d["bbox"], r["bbox"]) for r in removable)]
    return "own_brand", removable + icons


def _florence_snapshot() -> Path:
    return (Path(MODEL_DIR) / "hf" / "hub" / ("models--" + FLORENCE_REPO.replace("/", "--"))
            / "snapshots" / FLORENCE_REVISION)


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        while chunk := source.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


@app.function(volumes={MODEL_DIR: models_volume}, timeout=1800)
def prepare_models() -> dict:
    """Download pinned weights once into the Volume and verify every SHA-256."""
    import sys

    os.environ["HF_HUB_OFFLINE"] = "0"
    sys.path.insert(0, "/root/vendor")
    from huggingface_hub import snapshot_download
    from lama_inpaint.model_store import ensure_model

    manifest = json.loads((Path("/root/vendor/models/florence.json")).read_text())
    snapshot_download(
        FLORENCE_REPO,
        revision=FLORENCE_REVISION,
        allow_patterns=[artifact["name"] for artifact in manifest["files"]],
    )
    root = _florence_snapshot()
    for artifact in manifest["files"]:
        path = root / artifact["name"]
        if path.stat().st_size != artifact["size"] or _sha256_file(path) != artifact["sha256"]:
            raise RuntimeError(f"Florence-2 file failed verification: {artifact['name']}")

    lama_path = ensure_model(Path(MODEL_DIR) / "lama")
    models_volume.commit()
    return {"florence": str(root), "lama": str(lama_path)}


@app.cls(
    gpu="T4",
    volumes={MODEL_DIR: models_volume},
    min_containers=0,
    max_containers=1,
    scaledown_window=60,
    timeout=300,
)
class Processor:
    @modal.enter()
    def load(self):
        import sys

        import torch
        from transformers import AutoProcessor, Florence2ForConditionalGeneration

        sys.path.insert(0, "/root/vendor")
        from lama_inpaint.runtime import LamaInpaint

        started = time.perf_counter()
        root = _florence_snapshot()
        if not root.exists():
            raise RuntimeError("Model weights missing. Run prepare_models first.")
        self.device = "cuda"
        self.florence = Florence2ForConditionalGeneration.from_pretrained(
            root, torch_dtype=torch.float16).to(self.device).eval()
        self.florence_processor = AutoProcessor.from_pretrained(root)
        self.lama = LamaInpaint(self.device, cache_dir=Path(MODEL_DIR) / "lama", download=False)
        self.load_seconds = round(time.perf_counter() - started, 2)
        self.cold = True
        print(json.dumps({"event": "models_loaded", "seconds": self.load_seconds}))

    def _florence(self, task: str, image, text: str | None = None) -> dict:
        import torch

        prompt = task if text is None else task + text
        inputs = self.florence_processor(text=prompt, images=image, return_tensors="pt")
        inputs = {k: v.to(self.device).to(self.florence.dtype) if v.is_floating_point()
                  else v.to(self.device) for k, v in inputs.items()}
        with torch.inference_mode():
            ids = self.florence.generate(
                input_ids=inputs["input_ids"], pixel_values=inputs["pixel_values"],
                max_new_tokens=256, do_sample=False, num_beams=1)
        text_out = self.florence_processor.batch_decode(ids, skip_special_tokens=False)[0]
        return self.florence_processor.post_process_generation(
            text_out, task=task, image_size=(image.width, image.height))

    def _detect(self, image, prompt: str, max_bbox_percent: float) -> list[dict]:
        parsed = self._florence("<OPEN_VOCABULARY_DETECTION>", image, prompt)
        boxes = parsed.get("<OPEN_VOCABULARY_DETECTION>", {}).get("bboxes", [])
        area = image.width * image.height
        detections = []
        for bbox in boxes:
            x1, y1, x2, y2 = (int(v) for v in bbox)
            x1, y1 = max(0, x1), max(0, y1)
            x2, y2 = min(image.width, x2), min(image.height, y2)
            if x2 <= x1 or y2 <= y1:
                continue
            percent = (x2 - x1) * (y2 - y1) / area * 100
            detections.append({"bbox": [x1, y1, x2, y2], "area_percent": round(percent, 3),
                               "accepted": percent <= max_bbox_percent})
        return detections

    def _read_text(self, image, bbox: list[int]) -> str:
        x1, y1, x2, y2 = bbox
        pad = 8
        crop = image.crop((max(0, x1 - pad), max(0, y1 - pad),
                           min(image.width, x2 + pad), min(image.height, y2 + pad)))
        return str(self._florence("<OCR>", crop).get("<OCR>", "")).strip()

    def _find_partner_marks(self, image, terms: list[str], seeds: list[list[int]],
                            known_source: str | None = None) -> list[dict]:
        """
        Faint, semi-transparent partner marks (myhome.ge prints a pale logo in the middle of every
        photo) are found by the detector, but its box often covers only part of the logo, so the
        text read from it is a fragment ("ge", "nome"). Here each found box, and the middle of the
        photo where such logos sit, is read again with room around it and with the contrast
        raised, and the words are located. Only areas whose words read as a removable brand are
        returned, so other text in the photo is never touched.

        known_source: the photo is known to come from this partner (a listing with a MyHome ID).
        Then a piece of its name is enough ("ge" read from a pale "myhome.ge").
        """
        import cv2
        import numpy as np
        from PIL import Image

        w, h = image.size
        # The middle of the photo first (tight, then wider), then around each detector box.
        regions = [[0.28 * w, 0.42 * h, 0.72 * w, 0.58 * h], [0.18 * w, 0.34 * h, 0.82 * w, 0.66 * h]]
        for x1, y1, x2, y2 in seeds:
            cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
            bw, bh = max(x2 - x1, 0.25 * w), max(y2 - y1, 0.08 * h)
            regions.append([cx - bw, cy - bh, cx + bw, cy + bh])
        clahe = cv2.createCLAHE(clipLimit=8.0, tileGridSize=(8, 8))
        brand = "".join(normalize_mark_text(t) for t in terms)
        found = []
        for rx1, ry1, rx2, ry2 in regions:
            box = [int(max(0, rx1)), int(max(0, ry1)), int(min(w, rx2)), int(min(h, ry2))]
            if box[2] - box[0] < 16 or box[3] - box[1] < 16:
                continue
            crop = image.crop(box)
            scale = 2 if crop.width < 900 else 1
            if scale > 1:
                crop = crop.resize((crop.width * scale, crop.height * scale))
            # Pale marks on bright walls: local contrast (CLAHE) makes the faint letters stand out.
            grey = cv2.cvtColor(np.array(crop), cv2.COLOR_RGB2GRAY)
            crop = Image.fromarray(clahe.apply(grey)).convert("RGB")
            parsed = self._florence("<OCR_WITH_REGION>", crop).get("<OCR_WITH_REGION>", {})
            labels = parsed.get("labels", []) or []
            quads = parsed.get("quad_boxes", []) or []
            joined = " ".join(str(label) for label in labels)
            if not (partner_read_matches(joined, terms) or _is_piece_of(joined, known_source)):
                continue
            # Keep the words that belong to the brand (and pieces of it, e.g. "my", "home", ".ge").
            parts = []
            for label, quad in zip(labels, quads):
                text = normalize_mark_text(str(label))
                if not text or not (partner_read_matches(str(label), terms) or (len(text) >= 2 and text in brand)
                                    or _is_piece_of(str(label), known_source)):
                    continue
                xs, ys = quad[0::2], quad[1::2]
                parts.append([box[0] + min(xs) / scale, box[1] + min(ys) / scale,
                              box[0] + max(xs) / scale, box[1] + max(ys) / scale])
            if not parts:
                continue
            x1 = min(p[0] for p in parts)
            y1 = min(p[1] for p in parts)
            x2 = max(p[2] for p in parts)
            y2 = max(p[3] for p in parts)
            if known_source and not mark_is_removable(joined, terms):
                # Only a piece was read ("ge"): cover the whole logo, which is about a quarter of
                # the photo wide, stretching away from the piece towards the rest of the name.
                logo_w = max(x2 - x1, 0.24 * w)
                logo_h = max(y2 - y1, 0.085 * w)
                form = normalize_mark_text(known_source)
                form = form if form.endswith("ge") else form + "ge"
                piece = normalize_mark_text(joined)
                if form.endswith(piece):
                    x1 = x2 - logo_w
                elif form.startswith(piece):
                    x2 = x1 + logo_w
                else:
                    cx = (x1 + x2) / 2
                    x1, x2 = cx - logo_w / 2, cx + logo_w / 2
                cy = (y1 + y2) / 2
                y1, y2 = cy - logo_h / 2, cy + logo_h / 2
                mark = [int(max(0, x1)), int(max(0, y1)), int(min(w, x2)), int(min(h, y2))]
                if any(_iou(mark, d["bbox"]) > 0.5 for d in found):
                    continue
                percent = (mark[2] - mark[0]) * (mark[3] - mark[1]) / (w * h) * 100
                found.append({"bbox": mark, "area_percent": round(percent, 3), "accepted": True,
                              "text": joined, "primary": True, "partner_search": True})
                continue
            # The logo's icon and rounded outline reach past its letters.
            pad_x, pad_y = 0.9 * (y2 - y1), 0.6 * (y2 - y1)
            mark = [int(max(0, x1 - pad_x)), int(max(0, y1 - pad_y)),
                    int(min(w, x2 + pad_x)), int(min(h, y2 + pad_y))]
            if any(_iou(mark, d["bbox"]) > 0.5 for d in found):
                continue
            percent = (mark[2] - mark[0]) * (mark[3] - mark[1]) / (w * h) * 100
            found.append({"bbox": mark, "area_percent": round(percent, 3), "accepted": True,
                          "text": joined, "primary": True, "partner_search": True})
        return found

    @modal.method()
    def process(self, request: dict) -> dict:
        import cv2
        import numpy as np
        from PIL import Image, ImageDraw, ImageOps

        started = time.perf_counter()
        cold, self.cold = self.cold, False
        raw = base64.b64decode(request["image_b64"], validate=True)
        if len(raw) > MAX_IMAGE_BYTES:
            raise ValueError("Image is larger than 10 MB.")
        source_sha = hashlib.sha256(raw).hexdigest()
        expected = request.get("expected_sha256")
        if expected and expected.lower() != source_sha:
            raise ValueError("Image SHA-256 does not match expected_sha256.")

        Image.MAX_IMAGE_PIXELS = MAX_PIXELS
        pil = ImageOps.exif_transpose(Image.open(io.BytesIO(raw))).convert("RGB")
        mode = request.get("mode", "detect")
        basis = request.get("authorization_basis", "unknown")
        permission_reference = (request.get("permission_reference") or "").strip()
        own_terms = [t.lower() for t in request.get("own_brand_terms", ["velven"]) if t]
        # The partner the photo comes from, if known; only a removable brand counts.
        known_source = (request.get("known_source") or "").strip().lower() or None
        if known_source not in own_terms:
            known_source = None
        all_marks = (request.get("watermark_scope") == "all" and mode == "process"
                     and (basis == "owned" or (basis == "licensed" and bool(permission_reference))))
        max_bbox_percent = float(request.get("max_bbox_percent", 10.0))

        removable_limit = max(max_bbox_percent, MAX_REMOVABLE_BBOX_PERCENT)
        detections = self._detect(pil, request.get("detection_prompt", "watermark"), removable_limit)
        for detection in detections:
            detection["primary"] = True
        # Boxes over the normal limit are kept only if they turn out to be our or a partner's mark.
        candidates = sorted((d for d in detections if d["accepted"]),
                            key=lambda d: d["area_percent"], reverse=True)
        for detection in candidates:
            detection["text"] = self._read_text(pil, detection["bbox"])
        candidates = [d for d in candidates
                      if all_marks or d["area_percent"] <= max_bbox_percent or mark_is_removable(d["text"], own_terms)]
        if all_marks or not any(mark_is_removable(d["text"], own_terms) for d in candidates):
            # Faint marks: look again with other prompts, keeping only boxes that read as removable.
            for prompt in EXTRA_DETECTION_PROMPTS:
                for extra in self._detect(pil, prompt, removable_limit):
                    if not extra["accepted"] or any(_iou(extra["bbox"], d["bbox"]) > 0.5 for d in candidates):
                        continue
                    extra["text"] = self._read_text(pil, extra["bbox"])
                    if all_marks or mark_is_removable(extra["text"], own_terms):
                        extra["primary"] = False
                        candidates.append(extra)
                        detections.append(extra)
        if not any(mark_is_removable(d["text"], own_terms) for d in candidates):
            # Still nothing readable: a pale partner logo read only in pieces. Look again around
            # every box and in the middle of the photo with more room and more contrast.
            seeds = [d["bbox"] for d in detections]
            for mark in self._find_partner_marks(pil, own_terms, seeds, known_source):
                if mark["area_percent"] > removable_limit:
                    continue
                # The fragments it replaces ("ge", "nome") are the same logo, not another mark.
                candidates = [d for d in candidates
                              if not (_overlap_or_near(d["bbox"], mark["bbox"])
                                      and _is_unreadable_or_fragment(d["text"], own_terms))]
                candidates.append(mark)
                detections.append(mark)
        for detection in detections:
            detection["accepted"] = detection in candidates

        # Unreadable marks alone, or any mark naming another company, stay third-party.
        classification, accepted = classify_marks(candidates, own_terms)

        if all_marks and candidates:
            accepted = candidates
            if classification == "none":
                classification = "third_party"

        licensed = basis == "licensed" and bool(permission_reference)
        may_modify = basis in {"owned", "licensed"} and (
            all_marks or classification == "own_brand" or (classification == "third_party" and licensed))

        result = {
            "source_sha256": source_sha,
            "width": pil.width,
            "height": pil.height,
            "detections": detections,
            "classification": classification,
            "action": "none",
            "cold_start": cold,
            "model_load_seconds": self.load_seconds if cold else 0,
        }

        if classification == "none":
            result["action"] = "no_watermark"
        elif mode != "process" or not may_modify:
            result["action"] = ("needs_clean_original" if classification == "third_party"
                                else "detected_only")
        else:
            mask = Image.new("L", pil.size, 0)
            draw = ImageDraw.Draw(mask)
            for detection in accepted:
                x1, y1, x2, y2 = detection["bbox"]
                # A few pixels around the box so no outline of the logo is left behind.
                pad = max(4, round(0.08 * max(x2 - x1, y2 - y1)))
                draw.rectangle([max(0, x1 - pad), max(0, y1 - pad),
                                min(pil.width, x2 + pad), min(pil.height, y2 + pad)], fill=255)
            mask_np = np.array(mask)
            source_np = np.array(pil)
            output_bgr = self.lama(source_np, mask_np)
            validation = _validate(source_np, cv2.cvtColor(output_bgr, cv2.COLOR_BGR2RGB),
                                   mask_np, removable_limit)
            result["validation"] = validation
            if validation["ok"]:
                fmt = request.get("output_format", "jpeg").lower()
                buffer = io.BytesIO()
                out = Image.fromarray(cv2.cvtColor(output_bgr, cv2.COLOR_BGR2RGB))
                if fmt == "png":
                    out.save(buffer, "PNG", optimize=True)
                    result["content_type"] = "image/png"
                else:
                    out.save(buffer, "JPEG", quality=92, optimize=True)
                    result["content_type"] = "image/jpeg"
                data = buffer.getvalue()
                result["processed_b64"] = base64.b64encode(data).decode()
                result["processed_sha256"] = hashlib.sha256(data).hexdigest()
                result["action"] = "processed"
            else:
                result["action"] = "validation_failed"

        gpu_seconds = time.perf_counter() - started
        if cold:
            gpu_seconds += self.load_seconds
        result["gpu_seconds"] = round(gpu_seconds, 3)
        result["estimated_cost_usd"] = round(gpu_seconds * T4_USD_PER_SECOND, 6)
        print(json.dumps({"event": "processed", "sha256": source_sha, "mode": mode,
                          "classification": classification, "action": result["action"],
                          "gpu_seconds": result["gpu_seconds"], "cold": cold}))
        return result


def _validate(source, output, mask, max_bbox_percent: float) -> dict:
    """Reject outputs that changed the wrong pixels or look broken."""
    import numpy as np

    checks = {}
    checks["shape_matches"] = output.shape == source.shape
    mask_percent = float((mask > 0).mean() * 100)
    checks["mask_within_limit"] = mask_percent <= min(60.0, max_bbox_percent * 2.5)
    outside = mask == 0
    diff = np.abs(output.astype(np.int16) - source.astype(np.int16))
    checks["outside_mask_unchanged"] = bool(outside.any() and diff[outside].max() <= 2)
    inside_std = float(output[mask > 0].std()) if (mask > 0).any() else 0.0
    checks["not_flat_fill"] = inside_std > 1.0
    checks["finite"] = bool(np.isfinite(output).all())
    return {"ok": all(checks.values()), "mask_percent": round(mask_percent, 3), **checks}


@app.function(secrets=[secret], min_containers=0, max_containers=2, timeout=330)
@modal.concurrent(max_inputs=4)
@modal.asgi_app(label="velven-image-ai")
def web():
    return create_api(lambda request: Processor().process.remote.aio(request))


def create_api(run):
    from fastapi import Body, FastAPI, Header, HTTPException
    from pydantic import BaseModel, Field, ValidationError

    api = FastAPI(title="Velven image AI", docs_url=None, redoc_url=None)
    token = os.environ["VELVEN_IMAGE_AI_TOKEN"]

    class ProcessRequest(BaseModel):
        image_b64: str = Field(max_length=14_000_000)
        expected_sha256: str | None = None
        mode: str = Field("detect", pattern="^(detect|process)$")
        authorization_basis: str = Field("unknown", pattern="^(owned|licensed|unknown)$")
        permission_reference: str | None = Field(None, max_length=500)
        own_brand_terms: list[str] = ["velven"]
        detection_prompt: str = Field("watermark", max_length=100)
        max_bbox_percent: float = Field(10.0, gt=0, le=25)
        output_format: str = Field("jpeg", pattern="^(jpeg|png)$")
        known_source: str | None = Field(None, max_length=40)
        watermark_scope: str = Field("brands", pattern="^(brands|all)$")

    def authorize(value: str | None):
        supplied = (value or "").removeprefix("Bearer ").strip()
        if not supplied or not hmac.compare_digest(supplied, token):
            raise HTTPException(status_code=401, detail="Unauthorized")

    @api.get("/health")
    def health(authorization: str | None = Header(None)):
        authorize(authorization)
        return {"ok": True, "app": APP_NAME}

    @api.post("/v1/process")
    async def process(payload: dict = Body(...), authorization: str | None = Header(None)):
        authorize(authorization)
        try:
            body = ProcessRequest.model_validate(payload)
        except ValidationError as exc:
            raise HTTPException(status_code=422, detail=exc.errors(include_input=False)) from exc
        try:
            return await run(body.model_dump())
        except ValueError as exc:
            raise HTTPException(status_code=422, detail=str(exc)) from exc

    return api


@app.local_entrypoint()
def benchmark(folder: str, mode: str = "detect", basis: str = "owned",
              limit: int = 100, out: str = "benchmark.csv"):
    """Run up to `limit` authorized local images through the deployed pipeline."""
    import csv
    import statistics

    files = sorted(p for p in Path(folder).iterdir()
                   if p.suffix.lower() in {".jpg", ".jpeg", ".png", ".webp"})[:limit]
    if not files:
        raise SystemExit(f"No images found in {folder}")
    output_dir = Path(out).with_suffix("")
    output_dir.mkdir(parents=True, exist_ok=True)
    processor = modal.Cls.from_name(APP_NAME, "Processor")()
    rows = []
    wall_started = time.perf_counter()
    for path in files:
        started = time.perf_counter()
        try:
            r = processor.process.remote({
                "image_b64": base64.b64encode(path.read_bytes()).decode(),
                "mode": mode, "authorization_basis": basis})
            if r.get("processed_b64"):
                (output_dir / f"{path.stem}_processed.jpg").write_bytes(
                    base64.b64decode(r["processed_b64"]))
            rows.append({"file": path.name, "action": r["action"],
                         "classification": r["classification"],
                         "detections": len(r["detections"]),
                         "texts": " | ".join(d.get("text", "") for d in r["detections"]),
                         "gpu_seconds": r["gpu_seconds"], "cold": r["cold_start"],
                         "round_trip_seconds": round(time.perf_counter() - started, 3),
                         "validation_ok": r.get("validation", {}).get("ok", ""), "error": ""})
        except Exception as exc:  # keep benchmarking remaining images
            rows.append({"file": path.name, "action": "error", "error": str(exc)[:300]})
        print(rows[-1])
    with open(out, "w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys() | {"error"}))
        writer.writeheader()
        writer.writerows(rows)

    ok = [r for r in rows if r["action"] != "error"]
    warm = [r["gpu_seconds"] for r in ok if not r["cold"]]
    total_gpu = sum(r["gpu_seconds"] for r in ok)
    summary = {
        "images": len(rows), "errors": len(rows) - len(ok),
        "wall_seconds": round(time.perf_counter() - wall_started, 1),
        "gpu_seconds_total": round(total_gpu, 1),
        "warm_gpu_seconds_median": round(statistics.median(warm), 3) if warm else None,
        "warm_gpu_seconds_p95": round(sorted(warm)[int(len(warm) * 0.95) - 1], 3) if len(warm) > 1 else None,
        "est_cost_usd": round(total_gpu * T4_USD_PER_SECOND, 4),
        "est_cost_per_5000_usd": round(total_gpu / max(len(ok), 1) * 5000 * T4_USD_PER_SECOND, 2),
        "actions": {a: sum(1 for r in ok if r["action"] == a) for a in {r["action"] for r in ok}},
    }
    print(json.dumps(summary, indent=2))
    Path(out).with_suffix(".summary.json").write_text(json.dumps(summary, indent=2))


@app.local_entrypoint()
def try_folder(folder: str, out: str = "try-out", terms: str = "velven,myhome,ss.ge", source: str = ""):
    """Runs this file's code (not the deployed app) on local photos and saves the results."""
    output_dir = Path(out)
    output_dir.mkdir(parents=True, exist_ok=True)
    processor = Processor()
    for path in sorted(p for p in Path(folder).iterdir() if p.suffix.lower() in {".jpg", ".jpeg", ".png", ".webp"}):
        r = processor.process.remote({
            "image_b64": base64.b64encode(path.read_bytes()).decode(), "mode": "process",
            "authorization_basis": "owned", "own_brand_terms": terms.split(","), "known_source": source or None})
        if r.get("processed_b64"):
            (output_dir / f"{path.stem}_clean.jpg").write_bytes(base64.b64decode(r["processed_b64"]))
        print(path.name, r["action"], r["classification"],
              [(d.get("text", ""), d["accepted"], d.get("partner_search", False)) for d in r["detections"]])
