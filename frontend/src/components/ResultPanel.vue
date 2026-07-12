<script setup lang="ts">
import { computed } from 'vue'
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Message from 'primevue/message'
import type { VerifyResponse } from '../api/client'

const props = defineProps<{ result: VerifyResponse }>()

const approved = computed(() => props.result.status === 'Approve')

const fieldRows = computed(() =>
  Object.entries(props.result.extractedFields).map(([key, fv]) => ({
    key,
    value: fv.value,
    confidence: fv.confidence,
  })),
)

function confidenceSeverity(c: number | null) {
  if (c == null) return 'secondary'
  if (c >= 0.9) return 'success'
  if (c >= 0.75) return 'warn'
  return 'danger'
}

// Turn provider field keys (e.g. "dateOfBirth") into readable labels ("Date of birth").
function humanize(key: string) {
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}
</script>

<template>
  <Card>
    <template #content>
      <div class="verdict">
        <Tag
          :value="approved ? 'Approved' : 'Rejected'"
          :severity="approved ? 'success' : 'danger'"
          :icon="approved ? 'pi pi-check-circle' : 'pi pi-times-circle'"
          class="verdict-tag"
        />
        <Tag :value="`extractor: ${result.extractorMode}`" severity="secondary" />
      </div>

      <h3>Rule results</h3>
      <ul class="rules">
        <li v-for="r in result.ruleResults" :key="r.ruleKey">
          <i :class="r.passed ? 'pi pi-check-circle pass' : 'pi pi-times-circle fail'" />
          <span><strong>{{ r.ruleLabel }}</strong> — {{ r.message }}</span>
        </li>
        <li v-if="result.ruleResults.length === 0" class="muted">
          No rules selected — approved by default.
        </li>
      </ul>

      <Message
        v-if="result.ignoredChecks.length > 0"
        severity="warn"
        :closable="false"
        class="ignored"
      >
        <strong>{{ result.ignoredChecks.length }} check(s) were ignored</strong> and did not
        affect the verdict:
        <ul>
          <li v-for="(c, i) in result.ignoredChecks" :key="i">
            <code>{{ c.field }} · {{ c.rule }}</code> — {{ c.reason }}
          </li>
        </ul>
      </Message>

      <h3>Extracted fields</h3>
      <DataTable :value="fieldRows" size="small" stripedRows>
        <Column header="Field">
          <template #body="{ data }">{{ humanize(data.key) }}</template>
        </Column>
        <Column field="value" header="Value" bodyClass="tabular-nums" />
        <Column header="Confidence" headerStyle="width:7rem">
          <template #body="{ data }">
            <Tag
              v-if="data.confidence != null"
              :value="`${Math.round(data.confidence * 100)}%`"
              :severity="confidenceSeverity(data.confidence)"
              class="tabular-nums"
            />
          </template>
        </Column>
      </DataTable>
    </template>
  </Card>
</template>

<style scoped>
.verdict {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}
.verdict-tag {
  font-size: 1.05rem;
  padding: 0.5rem 0.9rem;
}
h3 {
  font-size: 0.95rem;
  margin: 1.25rem 0 0.5rem;
}
.rules {
  list-style: none;
  padding: 0;
  margin: 0;
}
.rules li {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.35rem 0;
}
.rules .pass {
  color: var(--p-green-500);
}
.rules .fail {
  color: var(--p-red-500);
}
.muted {
  color: var(--p-text-muted-color);
}
.ignored {
  margin-top: 1rem;
}
.ignored ul {
  margin: 0.4rem 0 0;
  padding-left: 1.1rem;
}
.ignored code {
  font-size: 0.85em;
}
</style>
