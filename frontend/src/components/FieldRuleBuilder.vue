<script setup lang="ts">
import { computed, reactive } from 'vue'
import Card from 'primevue/card'
import Select from 'primevue/select'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import type { FieldDescriptor, FieldRuleDescriptor } from '../api/client'
import { dateParamError } from '../lib/dateParam'
import { makeCheckRow, type CheckRow } from '../composables/useVerification'

const props = defineProps<{
  fields: FieldDescriptor[]
  fieldRules: FieldRuleDescriptor[]
}>()

const rows = defineModel<CheckRow[]>({ required: true })

const fieldByKey = computed(() => new Map(props.fields.map((f) => [f.key, f])))
const ruleByKey = computed(() => new Map(props.fieldRules.map((r) => [r.key, r])))

// Only the rules whose `appliesTo` includes the selected field's type (the matrix).
function rulesForRow(row: CheckRow): FieldRuleDescriptor[] {
  const type = row.field ? fieldByKey.value.get(row.field)?.type : undefined
  if (!type) return []
  return props.fieldRules.filter((r) => r.appliesTo.includes(type))
}

function needsParam(row: CheckRow): FieldRuleDescriptor | undefined {
  const rule = row.rule ? ruleByKey.value.get(row.rule) : undefined
  return rule?.requiresParam ? rule : undefined
}

// A param is a date expression when it feeds a rule attached to a date field.
function isDateParam(row: CheckRow): boolean {
  const type = row.field ? fieldByKey.value.get(row.field)?.type : undefined
  return type === 'date' && !!needsParam(row)
}

// Track which value inputs the user has interacted with so we don't flag an empty
// field before they've had a chance to fill it (validate-on-blur).
const touched = reactive(new WeakSet<CheckRow>())
function markTouched(row: CheckRow) {
  touched.add(row)
}

// The inline error for a row's Value input, or null when valid / not a date rule.
function paramError(row: CheckRow): string | null {
  if (!isDateParam(row)) return null
  const err = dateParamError(row.param)
  if (!err) return null
  if (!row.param.trim() && !touched.has(row)) return null
  return err
}

// Changing the field can invalidate the chosen rule (different type) — clear it if so.
function onFieldChange(row: CheckRow) {
  if (row.rule && !rulesForRow(row).some((r) => r.key === row.rule)) {
    row.rule = null
    row.param = ''
  }
}

function addRow() {
  rows.value = [...rows.value, makeCheckRow()]
}

function removeRow(index: number) {
  rows.value = rows.value.filter((_, i) => i !== index)
}
</script>

<template>
  <Card>
    <template #title>
      <div class="card-title"><i class="pi pi-sliders-h" /> Validation checks</div>
    </template>
    <template #subtitle>
      Apply a rule to a field. Available rules depend on the field's type — dates offer
      before/after, text offers required / pattern / length. Seeded with sensible defaults.
    </template>
    <template #content>
      <div v-if="rows.length === 0" class="empty">
        No checks — this document will be auto-approved.
      </div>

      <div class="rows">
        <div v-if="rows.length" class="row row-head" aria-hidden="true">
          <span>Field</span>
          <span>Rule</span>
          <span>Value</span>
          <span>Label</span>
          <span class="row-head-spacer"></span>
        </div>

        <div v-for="(row, i) in rows" :key="row.id" class="row">
          <Select
            v-model="row.field"
            :options="fields"
            option-label="label"
            option-value="key"
            placeholder="Field"
            :aria-label="`Field for check ${i + 1}`"
            class="cell field-select"
            @change="onFieldChange(row)"
          />
          <Select
            v-model="row.rule"
            :options="rulesForRow(row)"
            option-label="label"
            option-value="key"
            placeholder="Rule"
            :disabled="!row.field"
            :aria-label="`Rule for check ${i + 1}`"
            class="cell rule-select"
          />
          <InputText
            v-if="needsParam(row)"
            v-model="row.param"
            :placeholder="needsParam(row)!.paramLabel ?? 'Value'"
            :aria-label="`Value for check ${i + 1}`"
            :invalid="!!paramError(row)"
            :aria-invalid="!!paramError(row)"
            :aria-describedby="paramError(row) ? `date-err-${row.id}` : undefined"
            class="cell param-input"
            @blur="markTouched(row)"
          />
          <div v-else class="cell param-spacer" aria-hidden="true"></div>
          <InputText
            v-model="row.name"
            placeholder="Name (optional)"
            :aria-label="`Label for check ${i + 1}`"
            class="cell name-input"
          />
          <Button
            icon="pi pi-trash"
            severity="danger"
            text
            rounded
            :aria-label="`Remove check ${i + 1}`"
            class="row-remove"
            @click="removeRow(i)"
          />
          <small v-if="paramError(row)" :id="`date-err-${row.id}`" class="param-error" role="alert">
            <i class="pi pi-exclamation-circle" aria-hidden="true" /> {{ paramError(row) }}
          </small>
        </div>
      </div>

      <Button
        label="Add check"
        icon="pi pi-plus"
        severity="secondary"
        outlined
        size="small"
        class="add-btn"
        @click="addRow"
      />
    </template>
  </Card>
</template>

<style scoped>
.card-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 1.05rem;
}
.empty {
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
  margin-bottom: 0.75rem;
}
.rows {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.9rem;
}
.row {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1.2fr) minmax(0, 1fr) minmax(0, 1fr) auto;
  align-items: center;
  gap: 0.5rem;
}
.row-head {
  padding: 0 0.15rem 0.15rem;
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
.row-head-spacer {
  width: 2.25rem;
}
.param-error {
  grid-column: 3 / -1;
  display: flex;
  align-items: center;
  gap: 0.3rem;
  margin-top: -0.15rem;
  color: var(--p-red-500);
  font-size: 0.8rem;
}
.param-error .pi {
  font-size: 0.85rem;
}
/* Let selects/inputs shrink to their grid track instead of forcing overflow. */
.cell {
  min-width: 0;
  width: 100%;
}
.row-remove {
  flex: none;
}

@media (max-width: 720px) {
  .row-head {
    display: none;
  }
  .row {
    grid-template-columns: 1fr 1fr;
    gap: 0.5rem 0.6rem;
    padding: 0.75rem;
    padding-right: 2.5rem;
    border: 1px solid var(--p-content-border-color);
    border-radius: var(--p-border-radius-lg, 10px);
    position: relative;
  }
  .param-spacer {
    display: none;
  }
  .row-remove {
    position: absolute;
    top: 0.35rem;
    right: 0.35rem;
  }
  .param-error {
    grid-column: 1 / -1;
  }
}
.add-btn {
  margin-top: 0.1rem;
}
</style>
