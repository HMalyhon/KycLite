import { describe, it, expect } from 'vitest'
import { dateParamError } from './dateParam'

// dateParamError returns null when a date-rule Value is well-formed, otherwise a hint string.
// These tests lock the format contract that mirrors the backend's DateParsing.cs.
describe('dateParamError', () => {
  describe('relative expressions', () => {
    it.each(['today', 'today-18y', 'today+30d', 'today-6m', 'TODAY', 'today + 1 d'])(
      'accepts %s',
      (value) => {
        expect(dateParamError(value)).toBeNull()
      },
    )

    it.each(['today-18x', 'tomorrow', 'today-', 'today-18', 'yesterday', 'today*2'])(
      'rejects %s',
      (value) => {
        expect(dateParamError(value)).not.toBeNull()
      },
    )
  })

  describe('absolute calendar dates', () => {
    it.each([
      '2030-01-01', // yyyy-MM-dd
      '2030/01/01', // yyyy/MM/dd
      '2030-1-1', // single-digit month/day
      '01-02-2030', // dd-MM-yyyy
      '01/02/2030', // dd/MM/yyyy or MM/dd/yyyy
      '13/01/2030', // 13 > 12 forces day-first interpretation
      '30-01-2030', // dd-MM-yyyy (30 Jan 2030)
      '2024-02-29', // real leap day
    ])('accepts %s', (value) => {
      expect(dateParamError(value)).toBeNull()
    })

    it.each([
      '2030-02-30', // Feb 30 never exists
      '2030-13-01', // month 13
      '2030-00-05', // month 0
      '2023-02-29', // 2023 is not a leap year
      '2030-01/01', // mixed separators (backend uses a fixed separator)
      '2030.01.01', // unsupported separator
    ])('rejects %s', (value) => {
      expect(dateParamError(value)).not.toBeNull()
    })
  })

  describe('leap-year edge for low years (regression: Date-ctor 0–99 remapping)', () => {
    it('accepts 0000-02-29 (year 0 is a proleptic leap year)', () => {
      expect(dateParamError('0000-02-29')).toBeNull()
    })
    it('rejects 0001-02-29 (year 1 is not a leap year)', () => {
      expect(dateParamError('0001-02-29')).not.toBeNull()
    })
  })

  describe('empty / nullish', () => {
    it.each(['', '   ', null, undefined])('flags %s as missing', (value) => {
      expect(dateParamError(value)).not.toBeNull()
    })
  })
})
