/** A birth registration number is exactly 17 digits; the API accepts ASCII digits only. */
export const STUDENT_ID_LENGTH = 17;

const BENGALI_ZERO = 0x09e6; // ০ … ৯ = U+09E6 … U+09EF

/**
 * What the parent typed → what we send: Bengali digits become ASCII digits, and spaces, dashes and other
 * separators people commonly type are removed. Anything else (letters, symbols) is kept so validation can
 * reject it rather than silently changing the number.
 */
export function normalizeStudentId(input: string): string {
  let out = '';
  for (const ch of input) {
    const code = ch.codePointAt(0) ?? 0;
    if (code >= BENGALI_ZERO && code <= BENGALI_ZERO + 9) out += String(code - BENGALI_ZERO);
    else if (/[\s\-‐‑‒–—_.]/u.test(ch)) continue;
    else out += ch;
  }
  return out;
}

export function isValidStudentId(value: string): boolean {
  return /^[0-9]{17}$/.test(value);
}

/** Count of digits after normalization, for "you entered 15". */
export const digitCount = (normalized: string): number => (normalized.match(/[0-9]/g) ?? []).length;
