# Velven OpenAI image editing cost audit

Verified on 2026-11-05 using read-only Railway configuration, a READ ONLY PostgreSQL transaction, saved API usage and current official OpenAI documentation. No image-generation request was made, no production records/variables were changed, and nothing was pushed or deployed. Code changes in this checkout are pending review. Image quality, image normalization, output size, model, limits, authentication, approval and storage behavior remain unchanged.

## Exact request and official support

The deployed ASP.NET service sends `POST https://api.openai.com/v1/images/edits`, multipart/form-data, with `model=gpt-image-2.5-sunburst`, `quality=low`, `size=1024x1024`, `n=1`, `output_format=png`, image PNG bytes and an optional PNG mask. The configuration value is passed directly as the model field; no prefix, substitution or automatic model fallback exists. This is the direct Images API, not Responses or Batch. The canonical input PNG uses the original image's displayed dimensions after EXIF normalization; `size` specifies the API output size, not an input resize. The returned image is resized to original displayed dimensions locally, which does not create another provider request.

The exact model ID is officially documented for image editing. Existing successful responses also establish that it worked with this project's key. Official model documentation lists the `/v1/images/edits` endpoint. No undocumented `/Hdreal` or `/RemoveWaterMark` API fields are sent. Saved metadata for jobs 898-902 confirms the requested model, low quality and 1024x1024 size; the old implementation did not retain the full response envelope or `x-request-id`, so those cannot be retrospectively recovered.

Official sources checked:
- [Exact model, supported endpoint and Standard token rates](https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst)
- [Current pricing](https://developers.openai.com/api/docs/pricing)
- [Images edit API, multipart and usage schema](https://developers.openai.com/api/reference/resources/images/methods/edit)
- [Direct image edits have no cached-input discount](https://developers.openai.com/api/docs/guides/image-generation#cached-input-pricing)

## Pricing and calculation

Standard direct API USD rates per 1,000,000 tokens:

| Category | Rate |
| --- | ---: |
| Image input | $8.00 |
| Text input | $5.00 |
| Image output | $30.00 |

The lower Batch rates do not apply. The model outputs images, so no text-output charge is included. Responses cached-input discounts do not apply to this direct edits endpoint. This calculator intentionally does not subtract any cached-token fields. A GPT Image 2 size/quality calculator cannot predict GPT Image 2.5 token consumption; use returned usage instead.

`USD = (image_input_tokens * 8 + text_input_tokens * 5 + image_output_tokens * 30) / 1,000,000`

Unknown models receive no assumed rate card. Only `gpt-image-2.5-sunburst` and its documented `gpt-image-2.5-sunburst-2026-09-08` snapshot have verified rates in this change. New models need an explicitly verified card; they do not inherit another model's prices.

## Real saved usage

The read-only query returned five OpenAI jobs: 898, 899, 900, 901 and 902, all AwaitingApproval. Each saved response contains this usage:

```json
{
  "input_tokens": 1554,
  "input_tokens_details": { "image_tokens": 1457, "text_tokens": 97 },
  "output_tokens": 196,
  "output_tokens_details": { "image_tokens": 196, "text_tokens": 0 },
  "total_tokens": 1750
}
```

| Per successful edit | USD |
| --- | ---: |
| Image input: 1,457 tokens | $0.011656 |
| Text input: 97 tokens | $0.000485 |
| Image output: 196 tokens | $0.005880 |
| Total | **$0.018021** |

Five edits calculate to **$0.090105**. The existing app reserved $0.50, an overestimate of $0.409895 (about 5.55 times the usage-priced amount). These are Standard list-price calculations from real returned usage, not an invoice reconciliation. Tax, credits, discounts, other projects and other account activity are not known. No assertion is made about the actual deduction on the account billing dashboard.

[Sanitized read-only audit data](cost-audit-2026-11-05.json) contains each job's saved usage and computed component costs; it contains no credentials, image paths, images or uploader identities.

## Projections for similar images

These projections multiply the measured average of these five edits, including both image input and output plus prompt text. All five had identical reported token counts. Five samples are too few to guarantee costs for other photos, prompts or masks; input resolution and content and generated token counts can change the result. They are capacity estimates, not requests to raise the current quotas or process these volumes.

| Images | Image input USD | Image output USD | Text input USD | Total USD |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 1.165600 | 0.588000 | 0.048500 | **1.802100** |
| 1,000 | 11.656000 | 5.880000 | 0.485000 | **18.021000** |
| 5,000 | 58.280000 | 29.400000 | 2.425000 | **90.105000** |

Most of the measured cost is input-image tokens. Low output quality does not eliminate image-input charges. Neither the original input image quality nor output quality was changed to reduce cost.

## Local implementation

`Service/OpenAIImageCostCalculator.cs` defines the verified, dated Standard rate card and decimal token math. It validates the input modality counts, total input/output counts and, when present, output modality details and total tokens. Missing, malformed, inconsistent, zero or unpriced usage retains the outstanding reservation; it never becomes an assumed free edit. Legacy string-encoded usage and new structured usage are both supported. Duplicate-result reuse is counted as zero new provider spend.

`OpenAIImageEditingService` captures usage immediately after parsing a successful provider response, before image decoding/compositing or Supabase upload. It also records requested model/quality/size, returned model/quality/size when present, `x-request-id`, raw structured usage, cost components, rates, rate-card version and `invoiceVerified=false`. An unknown returned model cannot silently use the configured model's rates. Both the controller and worker persist this accounting through a callback before output processing/storage; later failures retain the known usage cost. HTTP failures/timeouts without usable usage keep the reservation and never auto-retry.

`ImageProcessingQueue.GetBudgetAsync` derives old flat records' effective costs from their saved usage on read, leaves their database values untouched, and separates `usageCostUsd`, `reservedCostUsd`, `usagePricedRequests` and `unreconciledRequests`. `spentUsd` is the sum of usage-priced costs plus outstanding reservations. The admin jobs response includes `costBasis` and token/cost components. No database migration is needed. `EstimatedCostUsd` remains the column/response field name for compatibility; usage-derived charges are still estimates of invoice charges. Prior Modal records remain excluded from OpenAI budgets.

The former `ImageAiOptions.EstimatedRequestUsd` is removed. Before a response exists, the application still needs a reservation for concurrency-safe budget protection. Its replacement uses this measured token profile and a configurable safety factor, not a fixed dollar charge:

| New optional variable | Default |
| --- | --- |
| `ImageAi__ForecastModel` | `gpt-image-2.5-sunburst` |
| `ImageAi__ForecastQuality` | `low` |
| `ImageAi__ForecastSize` | `1024x1024` |
| `ImageAi__ForecastTextInputTokens` | `97` |
| `ImageAi__ForecastImageInputTokens` | `1457` |
| `ImageAi__ForecastImageOutputTokens` | `196` |
| `ImageAi__ReservationMultiplier` | `2` |

Initial reservation = `(97*5 + 1457*8 + 196*30)/1,000,000 * 2` = **$0.036042**. A response with the measured usage settles it to $0.018021. This safety factor is an explicit planning assumption, not a proven upper bound or hard provider billing cap. For substantially different input dimensions, masks or prompts, recalibrate the forecast. A mismatched model, quality or output size requires a matching verified rate/forecast profile before paid processing. This does not change the actual request's model, quality or size. Verified rates are versioned; future pricing changes require a new reviewed rate card rather than silently editing historical rates.

If deployment is approved later, remove the obsolete Railway `ImageAi__EstimatedRequestUsd` variable; the new code ignores it. Current production configuration remains untouched. Existing five-edit daily/20-edit monthly quotas and $8 estimated-spend cutoff are unchanged.

## Tests and scope

Offline tests cover measured five-job arithmetic, all requested volume projections, known snapshot rates, no direct cached discount, malformed/missing/zero/inconsistent usage, unknown models, mismatched forecast profiles, legacy metadata, duplicate reuse, old-flat-record reconciliation, retained reservations on provider failure, exact multipart model/quality/size and usage retention after storage failure. The full backend suite and local Release publish are run; exact results are reported with delivery. No paid requests, account billing queries, production updates, pushes or deployments are part of this change.

Final local verification: 186 tests passed and 11 database-dependent tests were skipped. Local Release publish passed. Existing transitive `System.Security.Cryptography.Pkcs` vulnerability warnings remain. No production write, configuration change, paid request, git push or deployment was performed.
