<script setup lang="ts">
import Card from 'primevue/card'
</script>

<template>
  <main class="app">
    <Card>
      <template #title>KYC-Lite</template>
      <template #content>
        <p>ID / passport verification demo. The upload and verification UI is coming next.</p>
      </template>
    </Card>
  </main>
</template>

<style scoped>
.app {
  max-width: 960px;
  margin: 2rem auto;
  padding: 0 1rem;
}
</style>
