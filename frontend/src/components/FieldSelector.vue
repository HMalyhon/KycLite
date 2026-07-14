<script setup lang="ts">
import Card from 'primevue/card'
import Checkbox from 'primevue/checkbox'
import ToggleSwitch from 'primevue/toggleswitch'
import type { FieldDescriptor } from '../api/client'

defineProps<{ fields: FieldDescriptor[] }>()

const fullResponse = defineModel<boolean>('fullResponse', { required: true })
const selected = defineModel<string[]>('selected', { required: true })
</script>

<template>
  <Card>
    <template #title>
      <div class="card-title"><i class="pi pi-list-check" /> Fields to return</div>
    </template>
    <template #content>
      <div class="toggle">
        <ToggleSwitch v-model="fullResponse" input-id="full" />
        <label for="full"><strong>Full response</strong> — return every extracted field</label>
      </div>

      <div class="grid" :class="{ disabled: fullResponse }">
        <div v-for="f in fields" :key="f.key" class="item">
          <Checkbox
            v-model="selected"
            :value="f.key"
            :input-id="`f-${f.key}`"
            :disabled="fullResponse"
          />
          <label :for="`f-${f.key}`">{{ f.label }}</label>
        </div>
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
.toggle {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding-bottom: 0.85rem;
  margin-bottom: 0.85rem;
  border-bottom: 1px solid var(--p-content-border-color);
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 0.6rem 1rem;
  transition: opacity 0.15s;
}
.grid.disabled {
  opacity: 0.45;
}
.item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
label {
  cursor: pointer;
}
</style>
