// Typed client for the KYC-Lite API. The frontend talks only to this; it never touches Azure
// itself and only learns which extractor is active from what the API reports (`extractorMode`).

const BASE = import.meta.env.VITE_API_BASE ?? ''

export interface FieldDescriptor {
  key: string
  label: string
  type: string
}

export interface ApiStatus {
  /** "azure" (real OCR) or "mock" (offline sample data). */
  extractorMode: string
  /** The running build, stamped with the commit SHA at publish time. Unused by the UI — the
   *  deploy smoke test polls it to confirm the new container is the one answering. */
  version: string
}

export interface FieldRuleDescriptor {
  key: string
  label: string
  description: string
  requiresParam: boolean
  paramLabel: string | null
  appliesTo: string[]
}

export interface FieldCheck {
  field: string
  rule: string
  param?: string | null
  name?: string | null
}

export interface FieldValue {
  value: string
  confidence: number | null
}

export interface RuleResult {
  /** Position of this check in the fieldChecks array that was submitted. Unique; ruleKey is not. */
  checkIndex: number
  ruleKey: string
  ruleLabel: string
  passed: boolean
  message: string
}

export interface IgnoredCheck {
  /** Position of this check in the fieldChecks array that was submitted. */
  checkIndex: number
  field: string
  rule: string
  reason: string
}

export interface VerifyResponse {
  status: 'Approve' | 'Reject'
  documentType: string | null
  extractedFields: Record<string, FieldValue>
  ruleResults: RuleResult[]
  // Checks the backend dropped without evaluating (unknown field/rule, type mismatch, or a param
  // the rule couldn't interpret).
  ignoredChecks: IgnoredCheck[]
  // Requested field keys that don't exist in the catalog. A known field the document didn't carry
  // is simply absent from extractedFields — it is not listed here.
  ignoredFields: string[]
  extractorMode: string
}

// Every request is bounded, so a stalled connection surfaces as an error the user can act on
// rather than a spinner that never stops.
//
// The catalogs are the first calls on page load, which is exactly when a free-tier App Service may
// still be cold-starting (~30-40s), so they get room for that. Verify needs more again: a cold
// start, then the upload itself, then the server's own 60s cap on a single Azure analysis
// (AzureDocumentExtractor.AnalyzeTimeout, past which it answers 504). These are backstops against
// "never returns", not latency targets.
const CATALOG_TIMEOUT_MS = 30_000
const VERIFY_TIMEOUT_MS = 120_000

/**
 * One request path for the whole client, so success and failure are shaped the same way wherever
 * they come from: a bounded fetch, and RFC 7807 ProblemDetails (`detail`/`title`) turned into the
 * Error message the UI displays.
 */
async function send(path: string, init: RequestInit, timeoutMs: number, label: string) {
  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, { ...init, signal: AbortSignal.timeout(timeoutMs) })
  } catch (e) {
    // AbortSignal.timeout rejects with a TimeoutError DOMException whose own message ("signal timed
    // out") means nothing to a user; anything else thrown by fetch is a transport failure.
    if (e instanceof DOMException && e.name === 'TimeoutError')
      throw new Error(`${label} timed out. Please try again.`)
    throw new Error('Could not reach the server. Check your connection and try again.')
  }

  if (!res.ok) {
    const problem = await res.json().catch(() => null)
    throw new Error(problem?.detail ?? problem?.title ?? `${label} failed (${res.status}).`)
  }

  return res
}

async function getJson<T>(path: string): Promise<T> {
  const res = await send(path, {}, CATALOG_TIMEOUT_MS, 'Loading the page data')
  return res.json() as Promise<T>
}

export const getStatus = () => getJson<ApiStatus>('/api/status')
export const getFields = () => getJson<FieldDescriptor[]>('/api/fields')
export const getFieldRules = () => getJson<FieldRuleDescriptor[]>('/api/field-rules')
export const getDefaultChecks = () => getJson<FieldCheck[]>('/api/default-checks')

export async function verify(
  file: File,
  fields: string[],
  fieldChecks: FieldCheck[],
): Promise<VerifyResponse> {
  const form = new FormData()
  form.append('file', file)
  // Empty / "*" means "full response".
  form.append('fields', fields.length === 0 ? '*' : fields.join(','))
  form.append('fieldChecks', JSON.stringify(fieldChecks))

  const res = await send(
    '/api/verify',
    { method: 'POST', body: form },
    VERIFY_TIMEOUT_MS,
    'Verification',
  )
  return res.json() as Promise<VerifyResponse>
}
