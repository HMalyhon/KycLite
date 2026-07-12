<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import Card from 'primevue/card'
import Button from 'primevue/button'
import Image from 'primevue/image'

const file = defineModel<File | null>({ required: true })

const input = ref<HTMLInputElement | null>(null)
const dragging = ref(false)
const previewUrl = ref<string | null>(null)

const isImage = computed(() => file.value?.type.startsWith('image/') ?? false)

function setFile(f: File | null) {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = f && f.type.startsWith('image/') ? URL.createObjectURL(f) : null
  file.value = f
}

function onInput(e: Event) {
  setFile((e.target as HTMLInputElement).files?.[0] ?? null)
}

function onDrop(e: DragEvent) {
  dragging.value = false
  setFile(e.dataTransfer?.files?.[0] ?? null)
}

// Avoid leaking the last object URL if the component is torn down with a file selected.
onBeforeUnmount(() => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
})
</script>

<template>
  <Card>
    <template #title>
      <div class="card-title"><i class="pi pi-id-card" /> Document</div>
    </template>
    <template #content>
      <div
        class="dropzone"
        :class="{ dragging }"
        @dragover.prevent="dragging = true"
        @dragleave.prevent="dragging = false"
        @drop.prevent="onDrop"
      >
        <input ref="input" type="file" accept="image/*,application/pdf" hidden @change="onInput" />

        <template v-if="file">
          <Image v-if="isImage && previewUrl" :src="previewUrl" alt="Document preview" preview imageClass="preview-img" />
          <div v-else class="placeholder"><i class="pi pi-file-pdf" /></div>
          <p class="filename">{{ file.name }}</p>
          <Button label="Choose a different file" link size="small" @click="input?.click()" />
        </template>

        <template v-else>
          <div class="placeholder"><i class="pi pi-cloud-upload" /></div>
          <p class="prompt">Drag &amp; drop an ID or passport image here</p>
          <Button label="Browse files" icon="pi pi-upload" @click="input?.click()" />
          <p class="hint">JPEG, PNG, TIFF or PDF · max 10&nbsp;MB</p>
        </template>
      </div>
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
.dropzone {
  border: 2px dashed var(--p-content-border-color);
  border-radius: var(--p-border-radius-lg, 12px);
  padding: 1.75rem 1.25rem;
  text-align: center;
  transition: border-color 0.15s, background 0.15s;
}
.dropzone.dragging {
  border-color: var(--p-primary-color);
  background: var(--p-primary-50);
}
.placeholder {
  font-size: 2.5rem;
  color: var(--p-text-muted-color);
  margin-bottom: 0.5rem;
}
:deep(.preview-img) {
  max-width: 100%;
  max-height: 220px;
  border-radius: 10px;
  object-fit: contain;
}
.filename {
  font-weight: 600;
  word-break: break-all;
  margin: 0.75rem 0 0.25rem;
}
.prompt {
  margin: 0 0 0.75rem;
}
.hint {
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
  margin: 0.75rem 0 0;
}
</style>
