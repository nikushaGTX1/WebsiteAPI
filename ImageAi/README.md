# OpenAI image editing migration

Implementation date: 2026-11-05. No deployment, paid API call, remote deletion or production database operation was performed.

## Audit and preserved workflows

The Angular repository is `site`; the ASP.NET Core repository is the sibling `WebsiteAPI`. `server.mjs` is the Angular host/proxy, not the image processor. `WebsiteAPI-watermark-deploy` is a separate older checkout and was not changed; deploy the reviewed `WebsiteAPI` changes, not that checkout.

Previously, `ImageAiService.check` posted multipart uploads to `PhotoChecksController` (`/api/image-processing/check`), which encoded the image and called `ModalImageAiClient` at configured `ImageAi__Endpoint` + `/v1/process`. It also fetched `/health` for cache versions. The Watermark Remover immediately processed added images and asserted permission itself. List Your Property ran concurrent background OCR checks and could automatically replace selected files after detecting own/partner marks. The admin queue used `ImageProcessingQueue` + PostgreSQL `ImageProcessingJobs` + hosted `ImageProcessingWorker`. The worker downloaded original Supabase objects, called Modal, stored processed bytes, and waited for admin approval. Apartment uploads could enqueue detection jobs through `QueueWatermarkScanAsync`.

The provider client, live Modal test, OCR brand-assumption tests, and tracked `ImageAi/modal` Python deployment/vendor assets have been removed. The new shared `OpenAIImageEditingService` uses one multipart `POST https://api.openai.com/v1/images/edits`, with a server-side Bearer key, explicit model, size, quality and PNG output. There is no automatic retry or model fallback. SkiaSharp validates real image formats/dimensions and composites masked edits; matching Linux native assets are included for Railway.

Existing JWT roles, database schema, listing form, upload endpoints, listing publication, Supabase service and gallery workflows remain. No migration is required. GPU fields and historical statuses remain only for compatibility with existing database records. Legacy pending/running jobs are not resumed by this pipeline. Only explicit admin queue jobs tagged with the configured new pipeline fingerprint are eligible. Interrupted requests require manual review, since they may have been billed.

## User workflows

- Both editing workflows are admin-only during this initial trial. Ordinary listing uploads remain available to existing roles and never invoke paid editing.
- Watermark Remover: choose photos, attest ownership or explicit permission, optionally give a same-dimension PNG mask, then click Process. Review with the keyboard-accessible before/after toggle; download original or PNG result. Nothing is published.
- List Your Property: choose natural enhancement, authorized watermark removal or unwanted movable-object removal. Object removal needs a mask and description. Licensed photos need a permission reference. Create a preview for an individual photo, compare it with the original, approve for selection or discard. Saving/submitting the listing remains a separate existing action. Restore original is available before submission.
- Original uploads are separately retained under `image-edit-originals`; drafts under `image-edit-drafts`, referenced by the existing job table. The listing uploader sends only an approved selected file through the existing storage workflow; the original is retained in the image job even when it is not a separate gallery image.
- Existing stored listing jobs retain the original storage path and require the existing admin approve endpoint before the gallery pointer changes. Revert restores the original.
- Masks: PNG only, up to 8 MB, exact displayed dimensions after EXIF orientation, at least one fully transparent pixel. Fully transparent pixels are editable; every other decoded original pixel is copied back into the result. Images are limited to 8 MB, 20 megapixels, one frame, PNG/JPEG/WebP. JPEG EXIF orientation is normalized before editing. Results are PNG and returned at displayed original dimensions. Unmasked generative editing can alter detail; human review is required. Configured API size determines detail/cost before output is resized to original dimensions.
- `/Hdreal` and `/RemoveWaterMark` are interpreted as natural photorealistic preservation and authorized watermark-removal instructions. They are not OpenAI API parameters or documented ChatGPT settings. The service sends supported fields only.

## Railway server configuration

Keep `ImageAi__Enabled=false` until deployment and paid testing are approved. Do not put secrets in Angular, source, logs or browser configuration.

| Variable | Initial value / purpose |
| --- | --- |
| `OPENAI_API_KEY` | Secret, restricted to the ASP.NET service; use a dedicated OpenAI project |
| `ImageAi__Model` | REQUIRED explicit supported image-edit model accessible to your project; no default or paid fallback |
| `ImageAi__Enabled` | `false` initially |
| `ImageAi__Size` | `1024x1024`; allowed `1024x1024`, `1536x1024`, `1024x1536`, `auto` |
| `ImageAi__Quality` | `low`; allowed `low`, `medium`, `high`, `auto` |
| `ImageAi__EstimatedRequestUsd` | Obsolete after the local usage-accounting update; see `COST_AUDIT.md` |
| `ImageAi__DailyUserLimit` | Obsolete / ignored; daily cap removed |
| `ImageAi__MonthlyImageQuota` | `20` attempts per calendar month |
| `ImageAi__MonthlyBudgetUsd` | `10` reporting reference |
| `ImageAi__BudgetCutoffUsd` | `8` application estimated-spend cutoff |
| `ImageAi__RequestTimeoutSeconds` | `180` (10–300 allowed) |
| `ImageAi__PollSeconds` | `10`, explicit stored-listing jobs only |

Current official documentation lists GPT Image 2.5 Sunburst/Flare and GPT Image 2 variants for edits. Choose a supported GPT Image model in your actual project; account access is unverified because no account calls were authorized or performed. `gpt-image-1` was shut down on 2026-10-23; do not configure it. A model permission/not-found error does not trigger another model or an automatic retry. Confirm model access and capabilities in the account before enabling.

Existing PostgreSQL and Supabase connection/storage variables remain unchanged. New drafts use the existing bucket, authentication, signed URL and storage client. Keep originals and drafts private using the bucket's existing access policies. No storage policies were changed remotely.

## Cost and concurrency

Concurrency is fixed at ONE paid request globally across controller requests, queued work and Railway replicas using a PostgreSQL session advisory lock (`865214900172`). Concurrent callers receive 429; worker skips a busy cycle. Budget and duplicate checks and spend reservations execute while the lock is held. Before sending to OpenAI, the existing job table records a reservation. Failures, timeouts, provider errors and storage failures retain it; no automatic retry, stale-job recovery or retry endpoint is enabled. Successful duplicate upload requests return the saved draft without paying again; failed duplicates are blocked. Fingerprints include user, image bytes, mask, operation, instructions, rights and provider settings. Stored-listing job reuse is scoped to user, source, mode, authorization and pipeline settings. Quotas include failed attempts. Hourly and daily caps have been removed; admin enqueue batches contain at most 5 images.

The local usage-accounting update replaces the original flat $0.10 reservation with a token-based forecast and a dated Standard rate card, then settles costs using returned image/text usage. Five saved low-quality 1024x1024 edits calculate to $0.018021 each ($0.090105 total), while the deployed old code recorded $0.50. The local default preflight reservation is twice that measured sample cost ($0.036042); this is a configurable assumption and not a guaranteed maximum. Missing usage retains the reservation. Historical OpenAI records are repriced on read from saved usage without rewriting production history. See [COST_AUDIT.md](COST_AUDIT.md) for exact rates, sample data, volume projections, variables, failure behavior and deployment status. No quality, quota or production setting was changed for the audit.

Sources checked 2026-11-05:
- https://developers.openai.com/api/reference/resources/images/methods/edit
- https://developers.openai.com/api/docs/guides/image-generation
- https://developers.openai.com/api/docs/pricing

## Modal resources to remove manually after rollout verification

No remote Modal resources were modified. In the correct Modal workspace, verify no other consumers exist, then manually retire:

- App/deployment: `velven-image-ai` (including `Processor`, web `/v1/process` and `/health` functions).
- Volume: `velven-image-models` (cached Florence/LaMa model weights).
- Modal secret: `velven-image-ai`, containing `VELVEN_IMAGE_AI_TOKEN`.
- Railway variables: `ImageAi__Endpoint`, `ImageAi__Token`, `ImageAi__AutoEnqueueUploads`, `ImageAi__GpuUsdPerSecond`, `ImageAi__IdleSecondsPerColdStart`, `ImageAi__MaxAttempts`, `ImageAi__OwnBrandTerms__*`, `ImageAi__PartnerBrandTerms__*`; remove any separately configured `VELVEN_IMAGE_AI_TOKEN` for this integration.
- Personal Modal CLI credentials (`MODAL_TOKEN_ID`/`MODAL_TOKEN_SECRET`), if present, may serve other projects. Do not delete them without a separate account audit.

Old database jobs, original Supabase objects, volumes and secrets are not automatically deleted. No Python/Modal dependency remains in application runtime. Any ignored local Python caches are excluded from the Docker build context.

## Validation and deployment

Offline integration tests use TestServer, an EF in-memory database, mocked OpenAI and Supabase HTTP handlers, and a test lock. They cover real multipart upload routes, admin authorization, permission attestation, corrupt uploads, PNG/JPEG/WebP, mask validation/compositing, provider failure/cancellation, duplicate prevention, estimated-spend retention, quota/busy responses, draft approval, original/result downloads and storage failure. They do not prove production PostgreSQL locking, real account access, model output quality or remote Supabase policies.

Run:
```
dotnet test "Website API.Tests/Website API.Tests.csproj" --filter FullyQualifiedName~OpenAIImageEditingTests
dotnet publish "Website API.csproj" -c Release -o <local-output-directory>
```
From `site`, run `npx ng build --configuration production` and `node scripts/test-image-editing-ui.mjs` against a local Angular server. UI requests are intercepted with fixtures; no provider calls are made. Production PostgreSQL/Supabase and paid output fidelity require a separately approved deployment/smoke test. Do not enable, deploy, delete remote resources or make paid tests as part of these local checks.

Final local verification: 175 backend tests passed, 11 PostgreSQL-dependent tests skipped (including the optional real advisory-lock test). The image-editing subset has 18 passing offline tests and one optional PostgreSQL test. Angular production build, offline browser checks, and API Release publish passed. Existing Angular CSS budget / CommonJS warnings and pre-existing transitive `System.Security.Cryptography.Pkcs` vulnerability warnings remain. Native Linux binaries are included but this Windows session did not execute the Railway Linux container.

Budget accounting correction: OpenAI monthly estimated spend, monthly attempt quotas and daily per-user limits include only jobs whose pipeline starts with `openai-edit-v1:`. Historical Modal jobs remain unchanged and do not consume the OpenAI trial. OpenAI reservations remain counted across model/size/quality changes and after failures.

Hourly and daily trial caps were removed at the user's request. Admin-only access, a global concurrency of one, duplicate protection, 20-image monthly quota and $8 estimated-spend cutoff remain. `ImageAi__DailyUserLimit` is now ignored. No paid tests were used to validate this change.

Failed upload edits now support an explicit `retryFailedJobId` plus `acknowledgePossibleCharge=true` action by the same admin for the same fingerprint. It creates a new attempt and retains the previous reservation/history. Ordinary resubmissions still reuse successful results or reject prior failures; they never auto-retry. Retry selection uses the latest attempt, preventing an old failure from shadowing a later success. Provider HTTP status, allowlisted error code and request ID are recorded without storing provider error text. Queue-worker retries remain disabled.
