/**
 * Utility functions for consistent, error-free Indian Standard Time (IST, UTC+5:30) formatting.
 * 
 * Prevents timezone parsing bugs where ISO timestamps serialized without trailing 'Z'
 * are mistakenly interpreted as local client time instead of UTC.
 */

/**
 * Normalizes a date string or Date object into a guaranteed UTC-parsed Date.
 */
export function parseUtcDate(dateInput?: string | Date | null): Date | null {
  if (!dateInput) return null;
  if (dateInput instanceof Date) return dateInput;

  let str = String(dateInput).trim();
  if (!str) return null;

  // If ISO string without timezone indicator (no 'Z' and no '+' or '-XX:XX' offset), append 'Z'
  if (!str.endsWith('Z') && !/[+-]\d{2}:\d{2}$/.test(str)) {
    str += 'Z';
  }

  const d = new Date(str);
  return isNaN(d.getTime()) ? null : d;
}

/**
 * Formats a timestamp into strict IST format (e.g. "12:45:30 PM IST" or "20-Nov-2026, 12:45:30 PM IST").
 */
export function formatToIST(
  dateInput?: string | Date | null,
  options: { showDate?: boolean; showSeconds?: boolean } = { showDate: false, showSeconds: true }
): string {
  const d = parseUtcDate(dateInput);
  if (!d) return '-';

  try {
    const timeStr = d.toLocaleTimeString('en-IN', {
      timeZone: 'Asia/Kolkata',
      hour: '2-digit',
      minute: '2-digit',
      second: options.showSeconds !== false ? '2-digit' : undefined,
      hour12: true,
    });

    if (options.showDate) {
      const dateStr = d.toLocaleDateString('en-IN', {
        timeZone: 'Asia/Kolkata',
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      });
      return `${dateStr}, ${timeStr} IST`;
    }

    return `${timeStr} IST`;
  } catch {
    return String(dateInput);
  }
}

/**
 * Formats a date or expiry string into a clean Indian date format (e.g. "23-Nov-2026").
 */
export function formatDateIST(dateInput?: string | Date | null): string {
  const d = parseUtcDate(dateInput);
  if (!d) return '-';

  try {
    return d.toLocaleDateString('en-IN', {
      timeZone: 'Asia/Kolkata',
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return String(dateInput);
  }
}

/**
 * Calculates Days To Expiry (DTE) relative to IST today.
 */
export function getDaysToExpiry(expiryDateStr?: string | Date | null): number {
  const exp = parseUtcDate(expiryDateStr);
  if (!exp) return 0;

  const now = new Date();
  const diffMs = exp.getTime() - now.getTime();
  const days = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
  return Math.max(0, days);
}
