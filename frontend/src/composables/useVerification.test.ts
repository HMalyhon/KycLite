import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { withSetup } from '../test/helpers'

// The composable is the seam between the screen and the API; mocking the client lets every
// orchestration path — including the failures — be driven deterministically.
vi.mock('../api/client', () => ({
  getStatus: vi.fn(),
  getFields: vi.fn(),
  getFieldRules: vi.fn(),
  getDefaultChecks: vi.fn(),
  verify: vi.fn(),
}))

import * as client from '../api/client'
import { useVerification, makeCheckRow } from './useVerification'
import type { VerifyResponse } from '../api/client'

const FIELDS = [
  { key: 'firstName', label: 'First name', type: 'text' },
  { key: 'dateOfBirth', label: 'Date of birth', type: 'date' },
]

const RULES = [
  {
    key: 'required',
    label: 'Required',
    description: '',
    requiresParam: false,
    paramLabel: null,
    appliesTo: ['text', 'date'],
  },
  {
    key: 'pattern',
    label: 'Matches pattern',
    description: '',
    requiresParam: true,
    paramLabel: 'Pattern',
    appliesTo: ['text'],
  },
]

const DEFAULTS = [{ field: 'firstName', rule: 'required', param: null, name: 'First name present' }]

function response(overrides: Partial<VerifyResponse> = {}): VerifyResponse {
  return {
    status: 'Approve',
    documentType: 'passport',
    extractedFields: {},
    ruleResults: [],
    ignoredChecks: [],
    ignoredFields: [],
    extractorMode: 'mock',
    ...overrides,
  }
}

/** Boots the composable with the catalogs loaded, as the real screen starts. */
async function boot() {
  const state = withSetup(() => useVerification())
  await flushPromises()
  return state
}

beforeEach(() => {
  vi.mocked(client.getStatus).mockResolvedValue({ extractorMode: 'mock', version: 'test' })
  vi.mocked(client.getFields).mockResolvedValue(FIELDS)
  vi.mocked(client.getFieldRules).mockResolvedValue(RULES)
  vi.mocked(client.getDefaultChecks).mockResolvedValue(DEFAULTS)
})

describe('useVerification — startup', () => {
  it('loads the catalogs and seeds the builder from the default check set', async () => {
    const s = await boot()

    expect(s.fields.value).toEqual(FIELDS)
    expect(s.fieldRules.value).toEqual(RULES)
    expect(s.checkRows.value).toHaveLength(1)
    expect(s.checkRows.value[0]).toMatchObject({
      field: 'firstName',
      rule: 'required',
      param: '',
      name: 'First name present',
    })
    expect(s.error.value).toBeNull()
  })

  it('reports the extractor mode so the page can label the engine', async () => {
    vi.mocked(client.getStatus).mockResolvedValue({ extractorMode: 'azure', version: 'test' })
    const s = await boot()

    expect(s.extractorMode.value).toBe('azure')
    expect(s.isLiveExtractor.value).toBe(true)
  })

  it('keeps the screen usable when the status call fails', async () => {
    // The engine badge is informational — its failure must not take the whole page down with it.
    vi.mocked(client.getStatus).mockRejectedValue(new Error('status is down'))
    const s = await boot()

    expect(s.extractorMode.value).toBeNull()
    expect(s.error.value).toBeNull()
    expect(s.fields.value).toEqual(FIELDS)
  })

  it('surfaces a catalog failure, since the form cannot be built without one', async () => {
    vi.mocked(client.getFields).mockRejectedValue(new Error('Loading the page data failed (502).'))
    const s = await boot()

    expect(s.error.value).toBe('Loading the page data failed (502).')
  })
})

describe('useVerification — the fieldChecks it sends', () => {
  // fieldChecks is derived state the composable keeps to itself, so these assert on what actually
  // reaches the client — the contract that matters — rather than on an internal ref.
  const file = () => new File([new Uint8Array([1])], 'id.png', { type: 'image/png' })

  async function sentChecks(rows: ReturnType<typeof makeCheckRow>[]) {
    vi.mocked(client.verify).mockResolvedValue(response())
    const s = await boot()
    s.file.value = file()
    s.checkRows.value = rows
    await s.submit()
    return vi.mocked(client.verify).mock.calls[0][2]
  }

  it('drops rows that are still half-filled', async () => {
    const checks = await sentChecks([
      makeCheckRow({ field: 'firstName', rule: 'required' }),
      makeCheckRow({ field: 'firstName' }), // no rule chosen yet
      makeCheckRow({ rule: 'required' }), // no field chosen yet
    ])

    expect(checks).toHaveLength(1)
    expect(checks[0].field).toBe('firstName')
  })

  it('sends null for the param of a rule that takes none', async () => {
    // Stale text left in the box by a previous rule choice must not travel with the request.
    const checks = await sentChecks([
      makeCheckRow({ field: 'firstName', rule: 'required', param: 'leftover' }),
    ])

    expect(checks[0].param).toBeNull()
  })

  it('sends the param of a rule that needs one', async () => {
    const checks = await sentChecks([
      makeCheckRow({ field: 'firstName', rule: 'pattern', param: '^[A-Z]+$' }),
    ])

    expect(checks[0].param).toBe('^[A-Z]+$')
  })

  it('omits a blank custom name rather than sending an empty label', async () => {
    const checks = await sentChecks([
      makeCheckRow({ field: 'firstName', rule: 'required', name: '   ' }),
    ])

    expect(checks[0].name).toBeNull()
  })
})

describe('useVerification — submitting', () => {
  const file = () => new File([new Uint8Array([1])], 'id.png', { type: 'image/png' })

  it('refuses to submit without a document and says so', async () => {
    const s = await boot()

    await s.submit()

    expect(client.verify).not.toHaveBeenCalled()
    expect(s.error.value).toBe('Please choose a document image first.')
  })

  it('sends the full extraction when "full response" is on', async () => {
    vi.mocked(client.verify).mockResolvedValue(response())
    const s = await boot()
    s.file.value = file()
    s.selectedFields.value = ['firstName']

    await s.submit()

    // fullResponse defaults to true, so the selection is deliberately not sent.
    expect(vi.mocked(client.verify).mock.calls[0][1]).toEqual([])
  })

  it('sends the chosen fields once "full response" is off', async () => {
    vi.mocked(client.verify).mockResolvedValue(response())
    const s = await boot()
    s.file.value = file()
    s.fullResponse.value = false
    s.selectedFields.value = ['firstName']

    await s.submit()

    expect(vi.mocked(client.verify).mock.calls[0][1]).toEqual(['firstName'])
  })

  it('exposes the result and clears loading on success', async () => {
    vi.mocked(client.verify).mockResolvedValue(response({ status: 'Reject' }))
    const s = await boot()
    s.file.value = file()

    await s.submit()

    expect(s.result.value?.status).toBe('Reject')
    expect(s.loading.value).toBe(false)
    expect(s.error.value).toBeNull()
  })

  it('surfaces the API message and leaves no stale result behind', async () => {
    vi.mocked(client.verify).mockResolvedValue(response())
    const s = await boot()
    s.file.value = file()
    await s.submit()
    expect(s.result.value).not.toBeNull()

    vi.mocked(client.verify).mockRejectedValue(
      new Error('Verification timed out. Please try again.'),
    )
    await s.submit()

    expect(s.error.value).toBe('Verification timed out. Please try again.')
    expect(s.result.value).toBeNull()
    expect(s.loading.value).toBe(false)
  })
})

describe('useVerification — liveStatus', () => {
  it('announces the verdict and the tally', async () => {
    vi.mocked(client.verify).mockResolvedValue(
      response({
        status: 'Reject',
        ruleResults: [
          { checkIndex: 0, ruleKey: 'a:required', ruleLabel: 'A', passed: true, message: '' },
          { checkIndex: 1, ruleKey: 'b:required', ruleLabel: 'B', passed: false, message: '' },
        ],
      }),
    )
    const s = await boot()
    s.file.value = new File([new Uint8Array([1])], 'id.png', { type: 'image/png' })

    await s.submit()

    expect(s.liveStatus.value).toBe('Rejected. 1 of 2 checks passed.')
  })

  it('says nothing before the first verification', async () => {
    const s = await boot()

    expect(s.liveStatus.value).toBe('')
  })
})
