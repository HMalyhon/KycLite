import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { withPrimeVue } from '../test/helpers'
import ResultPanel from './ResultPanel.vue'
import type { VerifyResponse } from '../api/client'

function result(overrides: Partial<VerifyResponse> = {}): VerifyResponse {
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

const panel = (r: VerifyResponse) => mount(ResultPanel, { ...withPrimeVue, props: { result: r } })

describe('ResultPanel — verdict', () => {
  it('shows the approve verdict and the engine that produced it', () => {
    const wrapper = panel(result({ extractorMode: 'azure' }))

    expect(wrapper.text()).toContain('Approved')
    expect(wrapper.text()).toContain('extractor: azure')
  })

  it('shows the reject verdict', () => {
    expect(panel(result({ status: 'Reject' })).text()).toContain('Rejected')
  })
})

describe('ResultPanel — rule results', () => {
  it('lists every result with its label and reason', () => {
    const wrapper = panel(
      result({
        ruleResults: [
          {
            checkIndex: 0,
            ruleKey: 'firstName:required',
            ruleLabel: 'First name present',
            passed: true,
            message: 'Present.',
          },
          {
            checkIndex: 1,
            ruleKey: 'documentNumber:pattern',
            ruleLabel: 'Document number format',
            passed: false,
            message: 'Does not match /^ZZZ$/.',
          },
        ],
      }),
    )

    const items = wrapper.findAll('.rules li')
    expect(items).toHaveLength(2)
    expect(items[0].text()).toContain('First name present')
    expect(items[0].find('.pass').exists()).toBe(true)
    expect(items[1].text()).toContain('Does not match /^ZZZ$/.')
    expect(items[1].find('.fail').exists()).toBe(true)
  })

  it('renders two checks that share a field and rule as separate rows', () => {
    // ruleKey collides here; only checkIndex distinguishes them, and it is what keys the list.
    const wrapper = panel(
      result({
        ruleResults: [
          {
            checkIndex: 0,
            ruleKey: 'documentNumber:pattern',
            ruleLabel: 'Format',
            passed: true,
            message: 'Matches.',
          },
          {
            checkIndex: 1,
            ruleKey: 'documentNumber:pattern',
            ruleLabel: 'Second pattern',
            passed: false,
            message: 'Does not match.',
          },
        ],
      }),
    )

    const items = wrapper.findAll('.rules li')
    expect(items).toHaveLength(2)
    expect(items[0].text()).toContain('Format')
    expect(items[1].text()).toContain('Second pattern')
  })

  it('explains a vacuous approval rather than showing an empty list', () => {
    expect(panel(result()).text()).toContain('No rules selected')
  })
})

describe('ResultPanel — ignored input', () => {
  it('reports ignored checks with the reason they were dropped', () => {
    const wrapper = panel(
      result({
        ignoredChecks: [
          {
            checkIndex: 0,
            field: 'firstName',
            rule: 'pattern',
            reason: 'Invalid pattern /([bad/.',
          },
        ],
      }),
    )

    const warning = wrapper.find('.ignored')
    expect(warning.text()).toContain('1 check(s) were ignored')
    expect(warning.text()).toContain('Invalid pattern /([bad/.')
  })

  it('reports requested fields that do not exist', () => {
    // Unreachable from the UI — the selector is built from the catalog — so this is the only place
    // the ignoredFields rendering is exercised at all.
    const wrapper = panel(result({ ignoredFields: ['notARealField', 'lstName'] }))

    const warning = wrapper.find('.ignored')
    expect(warning.text()).toContain("2 requested field(s) don't exist")
    expect(warning.text()).toContain('notARealField')
    expect(warning.text()).toContain('lstName')
  })

  it('shows both kinds in one warning rather than two competing boxes', () => {
    const wrapper = panel(
      result({
        ignoredChecks: [
          { checkIndex: 0, field: 'firstName', rule: 'pattern', reason: 'Invalid pattern.' },
        ],
        ignoredFields: ['nope'],
      }),
    )

    expect(wrapper.findAll('.ignored')).toHaveLength(1)
    expect(wrapper.find('.ignored').text()).toContain('1 check(s) were ignored')
    expect(wrapper.find('.ignored').text()).toContain("1 requested field(s) don't exist")
  })

  it('stays hidden when the server acted on everything', () => {
    expect(panel(result()).find('.ignored').exists()).toBe(false)
  })
})

describe('ResultPanel — extracted fields', () => {
  it('humanises the provider field keys and shows the values', () => {
    const wrapper = panel(
      result({
        extractedFields: {
          dateOfBirth: { value: '1990-01-15', confidence: 0.97 },
          firstName: { value: 'Erika', confidence: null },
        },
      }),
    )

    const text = wrapper.text()
    expect(text).toContain('Date of birth')
    expect(text).toContain('1990-01-15')
    expect(text).toContain('First name')
    expect(text).toContain('Erika')
    expect(text).toContain('97%')
  })
})
