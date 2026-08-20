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
  ruleKey: string
  ruleLabel: string
  passed: boolean
  message: string
}

export interface IgnoredCheck {
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

async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`)
  if (!res.ok) throw new Error(`Request failed (${res.status})`)
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

  const res = await fetch(`${BASE}/api/verify`, { method: 'POST', body: form })
  if (!res.ok) {
    // Errors come back as RFC 7807 ProblemDetails (`detail`/`title`).
    const problem = await res.json().catch(() => null)
    throw new Error(problem?.detail ?? problem?.title ?? `Verification failed (${res.status})`)
  }
  return res.json() as Promise<VerifyResponse>
}
