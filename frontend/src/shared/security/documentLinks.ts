export function safeDocumentUrl(value?: string | null): string | undefined {
  if (!value || value.length > 4096 || /[\u0000-\u0020\u007f\\]/.test(value)) return undefined;
  if (value.startsWith("/api/sharepoint/download?fileKey=")) return value;
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password) return undefined;
    return url.href;
  } catch { return undefined; }
}
