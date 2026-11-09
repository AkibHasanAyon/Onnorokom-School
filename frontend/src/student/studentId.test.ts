import { describe, expect, it } from 'vitest';
import { digitCount, isValidStudentId, normalizeStudentId } from './studentId';

describe('normalizeStudentId', () => {
  it('converts Bengali digits to ASCII', () => {
    expect(normalizeStudentId('২০১৫২৬৯১২৩৪৫৬৭৮৯০')).toBe('20152691234567890');
  });

  it('strips spaces, dashes and similar separators', () => {
    expect(normalizeStudentId(' 2015-2691 2345 678–90 ')).toBe('20152691234567890');
    expect(normalizeStudentId('2015.2691_234567890')).toBe('20152691234567890');
  });

  it('handles mixed Bengali and ASCII digits', () => {
    expect(normalizeStudentId('২০১৫ 2691 ২৩৪৫ 67890')).toBe('20152691234567890');
  });

  it('keeps letters and symbols so validation can reject them', () => {
    expect(normalizeStudentId('2015abc')).toBe('2015abc');
  });
});

describe('isValidStudentId', () => {
  it.each(['20152691234567890', '00000000000000000'])('accepts %s', (id) => {
    expect(isValidStudentId(id)).toBe(true);
  });

  it.each(['', '2015269123456789', '201526912345678901', '2015269123456789a', '২০১৫২৬৯১২৩৪৫৬৭৮৯০', ' 20152691234567890'])(
    'rejects %j',
    (id) => expect(isValidStudentId(id)).toBe(false),
  );
});

describe('digitCount', () => {
  it('counts ASCII digits only', () => {
    expect(digitCount('12a34')).toBe(4);
  });
});
