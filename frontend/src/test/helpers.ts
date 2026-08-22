// Shared plumbing for the component and composable tests.
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'

/**
 * Spread into a `mount` call so components get the PrimeVue plugin the real app installs in
 * main.ts. Without it every PrimeVue child throws on its injected config.
 */
export const withPrimeVue = { global: { plugins: [PrimeVue] } }

/**
 * Runs a composable inside a real component instance so its lifecycle hooks actually fire —
 * `useVerification` does its catalog loading in `onMounted`, which never runs if the composable
 * is simply called in a test body.
 */
export function withSetup<T>(composable: () => T): T {
  let result!: T

  mount(
    defineComponent({
      setup() {
        result = composable()
        return () => h('div')
      },
    }),
  )

  return result
}
