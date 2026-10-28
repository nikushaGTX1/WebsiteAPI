# Image AI (watermark detection and authorized cleanup)

Headless Modal service built from the Florence-2 and LaMa inference code in
[D-Ogi/WatermarkRemover-AI](https://github.com/D-Ogi/WatermarkRemover-AI)
(MIT, commit `127108a`). The LaMa adapter is Apache-2.0 code from IOPaint.
License texts are in `modal/vendor/licenses`. The desktop GUI is not used.

## Policy

- Every photo is scanned with Florence-2. The text in each watermark is read with OCR.
- A photo is edited only when the job says `Owned` or `Licensed`, and the watermark
  is our own brand (`ImageAi__OwnBrandTerms`), or the job is `Licensed` with a
  `permissionReference`.
- Third-party or unreadable watermarks become `NeedsCleanOriginal`. Ask the owner
  for a clean photo. Nothing is removed.
- Processed output goes live only after an admin approves it. The original stays
  in storage (`ApartmentImage.OriginalStoragePath`) and `revert` restores it.

## Modal

Run from `ImageAi/modal` (Windows: `py -m modal`, else `modal`):

```
modal secret create velven-image-ai VELVEN_IMAGE_AI_TOKEN=<random 48+ chars>
modal run velven_image_ai.py::prepare_models      # one time, caches weights in Volume
modal deploy velven_image_ai.py
modal run velven_image_ai.py::benchmark --folder <dir of 100 authorized photos> --mode detect
modal run velven_image_ai.py::benchmark --folder <dir> --mode process --basis owned --out bench-process.csv
```

GPU class: T4, `min_containers=0`, `max_containers=1`, `scaledown_window=60`.

Endpoint: `POST https://<workspace>--velven-image-ai.us-east.modal.run/v1/process`
with `Authorization: Bearer <token>`. `GET /health` checks the token.

## API configuration (Railway variables)

| Variable | Test value |
| --- | --- |
| `ImageAi__Enabled` | `false` until approved |
| `ImageAi__AutoEnqueueUploads` | `false` |
| `ImageAi__Endpoint` | Modal URL above |
| `ImageAi__Token` | same value as the Modal secret |
| `ImageAi__MonthlyBudgetUsd` | `15` |
| `ImageAi__BudgetCutoffUsd` | `12` |
| `ImageAi__MonthlyImageQuota` | `5000` |
| `ImageAi__GpuUsdPerSecond` | `0.000164` |

## Admin endpoints (`Admin` role)

- `GET  /api/image-processing/status` - budget, quota, counts per status
- `POST /api/image-processing/jobs` - `{ apartmentId | apartmentImageIds, mode, authorizationBasis, permissionReference }`
- `GET  /api/image-processing/jobs?status=NeedsCleanOriginal`
- `POST /api/image-processing/jobs/{id}/approve | reject | revert | retry | clean-original-requested`
