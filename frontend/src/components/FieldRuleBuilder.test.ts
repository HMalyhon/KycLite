import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { withPrimeVue } from '../test/helpers'
import FieldRuleBuilder from './FieldRuleBuilder.vue'
import { makeCheckRow, type CheckRow } from '../composables/useVerification'

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
  {
    key: 'dateOnOrBefore',
    label: 'On or before (≤)',
    description: '',
    requiresParam: true,
    paramLabel: 'Date',
    appliesTo: ['date'],
  },
]

function build(rows: CheckRow[]) {
  return mount(FieldRuleBuilder, {
    ...withPrimeVue,
    props: { fields: FIELDS, fieldRules: RULES, modelValue: rows },
  })
}

describe('FieldRuleBuilder — the type matrix', () => {
  it('offers only the rules that apply to the chosen field type', () => {
    const wrapper = build([makeCheckRow({ field: 'dateOfBirth' })])
    const vm = wrapper.vm as unknown as {
      rulesForRow: (r: CheckRow) => typeof RULES
    }

    const offered = vm.rulesForRow(makeCheckRow({ field: 'dateOfBirth' })).map((r) => r.key)

    expect(offered).toEqual(['required', 'dateOnOrBefore'])
    expect(offered).not.toContain('pattern')
  })

  it('offers nothing until a field is chosen, so the rule box has nothing to show', () => {
    const wrapper = build([makeCheckRow()])
    const vm = wrapper.vm as unknown as { rulesForRow: (r: CheckRow) => typeof RULES }

    expect(vm.rulesForRow(makeCheckRow())).toEqual([])
  })
})

describe('FieldRuleBuilder — keeping a row coherent', () => {
  it('clears a rule that no longer applies when the field type changes', () => {
    // date → text leaves "on or before" attached to a text field, which the backend would only
    // report as ignored. Drop it at the point of change instead.
    const row = makeCheckRow({ field: 'firstName', rule: 'dateOnOrBefore', param: 'today-18y' })
    const wrapper = build([row])
    const vm = wrapper.vm as unknown as { onFieldChange: (r: CheckRow) => void }

    vm.onFieldChange(row)

    expect(row.rule).toBeNull()
    expect(row.param).toBe('')
  })

  it('keeps a rule that still applies after the field changes', () => {
    // text → text: "required" is valid either way, so the user shouldn't lose their selection.
    const row = makeCheckRow({ field: 'firstName', rule: 'required' })
    const wrapper = build([row])
    const vm = wrapper.vm as unknown as { onFieldChange: (r: CheckRow) => void }

    vm.onFieldChange(row)

    expect(row.rule).toBe('required')
  })
})

describe('FieldRuleBuilder — the value input', () => {
  it('shows a value box only for rules that take a param', () => {
    const needsOne = build([makeCheckRow({ field: 'firstName', rule: 'pattern' })])
    const needsNone = build([makeCheckRow({ field: 'firstName', rule: 'required' })])

    expect(needsOne.find('.param-input').exists()).toBe(true)
    expect(needsNone.find('.param-input').exists()).toBe(false)
  })

  it('stays quiet about an empty date value until the user has been there', async () => {
    // Validate-on-blur: flagging a field the user hasn't reached yet is just noise.
    const wrapper = build([makeCheckRow({ field: 'dateOfBirth', rule: 'dateOnOrBefore' })])

    expect(wrapper.find('.param-error').exists()).toBe(false)

    await wrapper.find('.param-input').trigger('blur')

    expect(wrapper.find('.param-error').exists()).toBe(true)
  })

  it('flags a malformed date value', async () => {
    const wrapper = build([
      makeCheckRow({ field: 'dateOfBirth', rule: 'dateOnOrBefore', param: 'yesterday' }),
    ])

    expect(wrapper.find('.param-error').text()).toContain('today±offset')
  })

  it('accepts a relative date value without complaint', () => {
    const wrapper = build([
      makeCheckRow({ field: 'dateOfBirth', rule: 'dateOnOrBefore', param: 'today-18y' }),
    ])

    expect(wrapper.find('.param-error').exists()).toBe(false)
  })

  it('leaves a text rule alone — the date hints do not apply to it', () => {
    const wrapper = build([
      makeCheckRow({ field: 'firstName', rule: 'pattern', param: 'not-a-date' }),
    ])

    expect(wrapper.find('.param-error').exists()).toBe(false)
  })
})

describe('FieldRuleBuilder — adding and removing', () => {
  it('appends a blank row', async () => {
    const wrapper = build([makeCheckRow({ field: 'firstName', rule: 'required' })])

    await wrapper.find('.add-btn').trigger('click')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    expect((emitted![0][0] as CheckRow[]).length).toBe(2)
  })

  it('removes the row that was clicked, not the last one', async () => {
    const rows = [
      makeCheckRow({ field: 'firstName', rule: 'required', name: 'keep me' }),
      makeCheckRow({ field: 'dateOfBirth', rule: 'required', name: 'remove me' }),
    ]
    const wrapper = build(rows)

    await wrapper.findAll('.row-remove')[1].trigger('click')

    const next = wrapper.emitted('update:modelValue')![0][0] as CheckRow[]
    expect(next).toHaveLength(1)
    expect(next[0].name).toBe('keep me')
  })
})
