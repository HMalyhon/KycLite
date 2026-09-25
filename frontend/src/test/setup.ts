// Global test setup, applied to every Vitest file (see `test.setupFiles` in vite.config.ts).
import { beforeEach, vi } from 'vitest'

// jsdom ships neither of the browser APIs below. They are stubbed as plain functions rather than
// vi.fn()s so that no amount of mock clearing between tests can strip them out again.

// UploadCard builds and releases an object URL for its image preview; without these, any test that
// selects a file throws before reaching its assertion. Assigned outright, not with `??=`: Vitest's
// jsdom environment installs its own createObjectURL, which converts jsdom Blobs by reading jsdom's
// private internals and broke when jsdom 30.1 renamed them. The tests need a URL, not a real blob.
URL.createObjectURL = () => 'blob:mock'
URL.revokeObjectURL = () => {}

// PrimeVue's Select registers a matchMedia listener on mount, and main.ts uses one to follow the
// OS colour scheme. Report "no match" — the tests assert on behaviour, not on responsive styling.
window.matchMedia ??= (query: string) =>
  ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }) as MediaQueryList

beforeEach(() => {
  vi.clearAllMocks()
})
