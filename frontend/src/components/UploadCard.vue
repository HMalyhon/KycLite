<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import Card from 'primevue/card'
import Button from 'primevue/button'
import Image from 'primevue/image'
import Message from 'primevue/message'

// Mirrors the server's limits (VerificationController.MaxUploadBytes and the media types
// FileSignatures knows). Duplicated across the stack by necessity — the point is to fail here,
// instantly, instead of after uploading megabytes only to be told no. The server stays
// authoritative: it also reads the magic bytes, which the browser can't tell us.
const MAX_BYTES = 10 * 1024 * 1024
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/tiff', 'application/pdf']

// The file input's `accept` narrows the picker, but drag-and-drop ignores it completely — so both
// paths funnel through setFile below, which is where the real check lives. Extensions and media
// types are both listed because desktop and mobile pickers honour them differently.
const ACCEPT_ATTR =
  '.jpg,.jpeg,.png,.tif,.tiff,.pdf,image/jpeg,image/png,image/tiff,application/pdf'

const file = defineModel<File | null>({ required: true })

const input = ref<HTMLInputElement | null>(null)
const dragging = ref(false)
const previewUrl = ref<string | null>(null)
const fileError = ref<string | null>(null)

const isImage = computed(() => file.value?.type.startsWith('image/') ?? false)

function describeSize(bytes: number) {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** Why this file can't be used, or null when it's fine. */
function rejectionReason(f: File): string | null {
  if (!ACCEPTED_TYPES.includes(f.type))
    return `“${f.name}” is not a supported format. Use a JPEG, PNG, TIFF or PDF.`
  if (f.size > MAX_BYTES) return `“${f.name}” is ${describeSize(f.size)}, over the 10 MB limit.`
  return null
}

function setFile(f: File | null) {
  fileError.value = f ? rejectionReason(f) : null

  // A rejected pick clears the selection rather than silently keeping the previous file, so what
  // the card shows is always what would be submitted.
  const accepted = fileError.value ? null : f

  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value =
    accepted && accepted.type.startsWith('image/') ? URL.createObjectURL(accepted) : null
  file.value = accepted

  // Reset the input so re-picking the *same* file still raises a change event — otherwise a user
  // who retries the file they just chose gets no feedback at all.
  if (input.value) input.value.value = ''
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
        <input ref="input" type="file" :accept="ACCEPT_ATTR" hidden @change="onInput" />

        <template v-if="file">
          <Image
            v-if="isImage && previewUrl"
            :src="previewUrl"
            alt="Document preview"
            preview
            image-class="preview-img"
          />
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

      <Message v-if="fileError" severity="error" role="alert" :closable="false" class="file-error">
        {{ fileError }}
      </Message>
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
  transition:
    border-color 0.15s,
    background 0.15s;
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
.file-error {
  margin-top: 0.85rem;
}
</style>
