import pluginVue from 'eslint-plugin-vue'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import skipFormatting from '@vue/eslint-config-prettier/skip-formatting'

// Flat ESLint config. The backend has StyleCop + analyzers as a build-breaking gate; this is the
// frontend counterpart: `npm run lint` fails on any warning (see the --max-warnings 0 script).
//
// eslint-plugin-vue is the point of this setup — it lints *inside* SFC templates (unused
// components, missing :key on v-for, invalid v-model targets), which a formatter can't see.
// Formatting itself is left entirely to Prettier: `skipFormatting` disables the stylistic ESLint
// rules that would otherwise fight it.
export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{ts,mts,tsx,vue}'],
  },
  {
    name: 'app/files-to-ignore',
    ignores: ['**/dist/**', '**/coverage/**'],
  },

  pluginVue.configs['flat/recommended'],
  vueTsConfigs.recommended,
  skipFormatting,
)
