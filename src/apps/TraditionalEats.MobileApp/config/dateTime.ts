const FALLBACK_TIME_ZONE = "UTC";

function getDeviceTimeZone(): string {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    return tz?.trim() || FALLBACK_TIME_ZONE;
  } catch {
    return FALLBACK_TIME_ZONE;
  }
}

function normalizeServerDateInput(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) return trimmed;

  // Our services store timestamps in UTC; if no offset is present, treat them as UTC.
  const hasOffset = /(Z|[+-]\d{2}:\d{2}|[+-]\d{4})$/i.test(trimmed);
  return hasOffset ? trimmed : `${trimmed}Z`;
}

export function parseServerDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(normalizeServerDateInput(value));
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatLocalOrderDateTime(
  value: string | Date | null | undefined,
): string {
  return formatWithTimeZone(value, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZoneName: "short",
  });
}

export function formatLocalDate(
  value: string | Date | null | undefined,
): string {
  return formatWithTimeZone(value, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

export function formatLocalTime(
  value: string | Date | null | undefined,
): string {
  return formatWithTimeZone(value, {
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
  });
}

export function formatLocalChatTimestamp(
  value: string | Date | null | undefined,
): string {
  const date = parseServerDate(value);
  if (!date) return "GMT";

  if (isSameDeviceDay(date, new Date())) {
    return formatLocalTime(date);
  }

  return formatWithTimeZone(date, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
  });
}

function formatWithTimeZone(
  value: string | Date | null | undefined,
  options: Intl.DateTimeFormatOptions,
): string {
  const date = parseServerDate(value);
  if (!date) return "GMT";

  const timeZone = getDeviceTimeZone();

  try {
    return new Intl.DateTimeFormat("en-US", {
      ...options,
      timeZone,
    }).format(date);
  } catch {
    return new Intl.DateTimeFormat("en-US", {
      ...options,
      timeZone: FALLBACK_TIME_ZONE,
    })
      .format(date)
      .replace(/\bUTC\b/g, "GMT");
  }
}

function isSameDeviceDay(left: Date, right: Date): boolean {
  const leftDay = formatDayKey(left);
  const rightDay = formatDayKey(right);
  return leftDay.length > 0 && leftDay === rightDay;
}

function formatDayKey(value: Date): string {
  const timeZone = getDeviceTimeZone();

  try {
    return new Intl.DateTimeFormat("en-CA", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      timeZone,
    }).format(value);
  } catch {
    return new Intl.DateTimeFormat("en-CA", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      timeZone: FALLBACK_TIME_ZONE,
    }).format(value);
  }
}
