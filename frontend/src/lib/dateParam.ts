// Advisory client-side validation for the "Value" of a date rule (On or before / On or after).
// Mirrors the formats the backend accepts in Validation/FieldRules/DateParsing.cs so the user
// gets an immediate hint. This is advisory only — the backend remains the source of truth and
// is free to accept or reject; a non-null message here never blocks submission.
//
// Accepted:
//   • a relative expression: "today", optionally with an offset "today-18y" / "today+30d" / "today-6m"
//   • an absolute calendar date in one of: yyyy-MM-dd, yyyy/MM/dd, dd-MM-yyyy, dd/MM/yyyy, MM/dd/yyyy

const RELATIVE = /^today\s*([+-]\s*\d+\s*[ymd])?$/i

// A short, human hint reused in the placeholder and error message.
export const DATE_PARAM_HINT = 'a date (2030-01-01) or today±offset (today-18y)'

/** True for a y/m/d triple that is a real calendar date (month-aware day count). */
function isRealDate(year: number, month: number, day: number): boolean {
  if (month < 1 || month > 12 || day < 1 || day > 31) return false
  // setFullYear (unlike the Date constructor) does not remap years 0–99 to the 1900s, so
  // leap-day checks stay correct for any year. Day 0 of `month` = last day of that month.
  const probe = new Date(0)
  probe.setFullYear(year, month, 0)
  return day <= probe.getDate()
}

/**
 * Validate a date-rule value. Returns `null` when valid, otherwise a short hint.
 * An empty value is flagged because these rules always require a reference date.
 */
export function dateParamError(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim()
  if (!trimmed) return `Enter ${DATE_PARAM_HINT}.`
  if (RELATIVE.test(trimmed)) return null

  // ISO / dash-or-slash numeric formats. The separator must be consistent (backreference \2),
  // matching the backend's fixed format list rather than accepting mixed separators.
  let m: RegExpMatchArray | null
  if ((m = trimmed.match(/^(\d{4})([-/])(\d{1,2})\2(\d{1,2})$/))) {
    // year-first (yyyy-MM-dd or yyyy/MM/dd)
    if (isRealDate(+m[1], +m[3], +m[4])) return null
  } else if ((m = trimmed.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/))) {
    // dash: day-first (dd-MM-yyyy)
    if (isRealDate(+m[3], +m[2], +m[1])) return null
  } else if ((m = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/))) {
    // slash: accept dd/MM/yyyy or MM/dd/yyyy
    if (isRealDate(+m[3], +m[2], +m[1]) || isRealDate(+m[3], +m[1], +m[2])) return null
  }

  return `Use ${DATE_PARAM_HINT}.`
}
