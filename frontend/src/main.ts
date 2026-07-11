import { createApp } from 'vue'
import PrimeVue from 'primevue/config'
import Aura from '@primevue/themes/aura'
import 'primeicons/primeicons.css'
import './style.css'
import App from './App.vue'

// Follow the OS colour scheme: PrimeVue's Aura preset emits dark tokens under `.dark`.
const applyColorScheme = (matches: boolean) =>
  document.documentElement.classList.toggle('dark', matches)
const media = window.matchMedia('(prefers-color-scheme: dark)')
applyColorScheme(media.matches)
media.addEventListener('change', (e) => applyColorScheme(e.matches))

createApp(App)
  .use(PrimeVue, {
    theme: {
      preset: Aura,
      options: {
        darkModeSelector: '.dark',
        cssLayer: { name: 'primevue', order: 'theme, base, primevue' },
      },
    },
  })
  .mount('#app')
