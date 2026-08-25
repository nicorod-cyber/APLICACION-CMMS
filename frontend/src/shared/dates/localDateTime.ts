export function localDateTimeInput(date = new Date()): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

export function localDateTimeToUtc(value: string): string {
  return new Date(value).toISOString();
}