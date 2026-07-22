<script setup lang="ts">
import Button from 'primevue/button'
import Message from 'primevue/message'
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import UploadCard from './components/UploadCard.vue'
import FieldSelector from './components/FieldSelector.vue'
import FieldRuleBuilder from './components/FieldRuleBuilder.vue'
import ResultPanel from './components/ResultPanel.vue'
import { useVerification } from './composables/useVerification'

const {
  fields,
  fieldRules,
  extractorMode,
  isLiveExtractor,
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
} = useVerification()
</script>

<template>
  <div class="page">
    <header class="masthead">
      <p class="eyebrow"><i class="pi pi-verified" /> Identity verification</p>
      <h1>KYC-Lite · Document Verification</h1>
      <p class="sub">
        Upload an ID or passport, choose the fields you want back and the rules to apply, and get an
        approve/reject verdict with reasons.
      </p>

      <!-- Say up-front which engine this deployment runs, so nobody has to guess whether the
           result came from real OCR or the offline sample document. -->
      <p v-if="extractorMode" class="engine">
        <Tag
          :value="isLiveExtractor ? 'Live OCR' : 'Mock extractor'"
          :severity="isLiveExtractor ? 'success' : 'warn'"
          :icon="isLiveExtractor ? 'pi pi-cloud' : 'pi pi-box'"
        />
        <span>{{
          isLiveExtractor
            ? 'Documents are analysed by Azure AI Document Intelligence.'
            : 'No OCR provider configured — uploads return a fixed sample document.'
        }}</span>
      </p>
    </header>

    <main class="layout">
      <form class="col" novalidate @submit.prevent="submit">
        <UploadCard v-model="file" />
        <FieldSelector
          v-model:full-response="fullResponse"
          v-model:selected="selectedFields"
          :fields="fields"
        />
        <FieldRuleBuilder v-model="checkRows" :fields="fields" :field-rules="fieldRules" />

        <Button
          type="submit"
          label="Verify document"
          icon="pi pi-check"
          size="large"
          class="verify-btn"
          :loading="loading"
          :disabled="!canSubmit"
        />
        <Message v-if="error" severity="error" role="alert" :closable="false">{{ error }}</Message>
      </form>

      <aside class="col results-col" aria-label="Verification result">
        <p class="sr-only" role="status">{{ liveStatus }}</p>
        <ResultPanel v-if="result" :result="result" />
        <Card v-else class="empty-state">
          <template #content>
            <div class="empty-inner">
              <i class="pi pi-inbox" aria-hidden="true" />
              <p>Results will appear here after you verify a document.</p>
            </div>
          </template>
        </Card>
      </aside>
    </main>
  </div>
</template>

<style scoped>
.page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 3rem 1.5rem 4rem;
}
.masthead {
  margin-bottom: 2rem;
}
.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  margin: 0 0 0.6rem;
  font-size: 0.78rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--p-primary-color);
}
.masthead h1 {
  margin: 0 0 0.5rem;
  font-size: clamp(1.6rem, 3vw, 2.1rem);
  font-weight: 700;
  letter-spacing: -0.02em;
  line-height: 1.15;
}
.sub {
  color: var(--p-text-muted-color);
  margin: 0;
  max-width: 62ch;
  font-size: 1.02rem;
}
.engine {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin: 0.9rem 0 0;
  font-size: 0.88rem;
  color: var(--p-text-muted-color);
}
.verify-btn {
  margin-top: 0.25rem;
  font-weight: 600;
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
  gap: 1.5rem;
  align-items: start;
}
.results-col {
  position: sticky;
  top: 1.5rem;
}
@media (max-width: 960px) {
  .layout {
    grid-template-columns: 1fr;
  }
  .results-col {
    position: static;
  }
}
.col {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}
.empty-state {
  color: var(--p-text-muted-color);
}
.empty-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  min-height: 300px;
  gap: 0.75rem;
}
.empty-inner .pi {
  font-size: 2.5rem;
  opacity: 0.5;
}
.empty-inner p {
  margin: 0;
}
</style>
