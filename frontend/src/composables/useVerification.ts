// Orchestrates the whole verification screen: loads the discovery catalog (fields + rules),
// holds the form state, derives the request payload, and calls the API. Keeping this out of
// App.vue makes it a thin view and lets the logic be unit-tested in isolation. Client-side
// date validation is advisory (see lib/dateParam) — the backend is the source of truth.
import { computed, onMounted, ref } from 'vue'
import {
  getFields,
  getFieldRules,
  getDefaultChecks,
  verify,
  type FieldCheck,
  type FieldDescriptor,
  type FieldRuleDescriptor,
  type VerifyResponse,
} from '../api/client'

/** A builder row mirrors a FieldCheck but tolerates partially-filled state while editing. */
export interface CheckRow {
  /** Stable identity so `v-for` keys survive add/remove/reorder (never sent to the API). */
  id: number
  field: string | null
  rule: string | null
  param: string
  name: string
}

let rowIdSeq = 0

/** Factory for a builder row, guaranteeing a unique `id`. */
export function makeCheckRow(init: Partial<Omit<CheckRow, 'id'>> = {}): CheckRow {
  return { id: rowIdSeq++, field: null, rule: null, param: '', name: '', ...init }
}

function toErrorMessage(e: unknown): string {
  return e instanceof Error ? e.message : 'Something went wrong. Please try again.'
}

export function useVerification() {
  // Discovery catalog (reference data driving the checkboxes / rule dropdowns).
  const fields = ref<FieldDescriptor[]>([])
  const fieldRules = ref<FieldRuleDescriptor[]>([])

  // Form state.
  const file = ref<File | null>(null)
  const fullResponse = ref(true)
  const selectedFields = ref<string[]>([])
  const checkRows = ref<CheckRow[]>([])

  // Request lifecycle.
  const result = ref<VerifyResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const ruleByKey = computed(() => new Map(fieldRules.value.map((r) => [r.key, r])))

  // Keep only complete rows, drop the param when the chosen rule doesn't need one, and
  // pass the optional custom name through.
  const fieldChecks = computed<FieldCheck[]>(() =>
    checkRows.value
      .filter((row): row is CheckRow & { field: string; rule: string } => !!row.field && !!row.rule)
      .map((row) => ({
        field: row.field,
        rule: row.rule,
        param: ruleByKey.value.get(row.rule)?.requiresParam ? row.param : null,
        name: row.name.trim() || null,
      })),
  )

  // Only a document is required; date-value hints are advisory and never block submission.
  const canSubmit = computed(() => !!file.value)

  // A concise, screen-reader-friendly summary of the request state (announced via a
  // dedicated live region rather than reading out the whole result panel).
  const liveStatus = computed(() => {
    if (loading.value) return 'Verifying document…'
    const r = result.value
    if (!r) return ''
    const verdict = r.status === 'Approve' ? 'Approved' : 'Rejected'
    const total = r.ruleResults.length
    if (!total) return `${verdict}.`
    const passed = r.ruleResults.filter((x) => x.passed).length
    return `${verdict}. ${passed} of ${total} checks passed.`
  })

  onMounted(async () => {
    try {
      const [fieldList, ruleList, defaults] = await Promise.all([
        getFields(),
        getFieldRules(),
        getDefaultChecks(),
      ])
      fields.value = fieldList
      fieldRules.value = ruleList
      // Seed the builder with the default checks (replicating the legacy rule set).
      checkRows.value = defaults.map((c) =>
        makeCheckRow({ field: c.field, rule: c.rule, param: c.param ?? '', name: c.name ?? '' }),
      )
    } catch (e) {
      error.value = toErrorMessage(e)
    }
  })

  async function submit() {
    if (!file.value) {
      error.value = 'Please choose a document image first.'
      return
    }
    loading.value = true
    error.value = null
    result.value = null
    try {
      result.value = await verify(
        file.value,
        fullResponse.value ? [] : selectedFields.value,
        fieldChecks.value,
      )
    } catch (e) {
      error.value = toErrorMessage(e)
    } finally {
      loading.value = false
    }
  }

  return {
    fields,
    fieldRules,
    file,
    fullResponse,
    selectedFields,
    checkRows,
    result,
    loading,
    error,
    canSubmit,
    liveStatus,
    submit,
  }
}
