import { useState, type AnchorHTMLAttributes } from "react";
import { useAuthStore } from "../../features/auth/authStore";
import { safeDocumentUrl } from "./documentLinks";

export function DocumentLink({ href, children, download, ...props }: AnchorHTMLAttributes<HTMLAnchorElement>) {
  const [error, setError] = useState<string | null>(null);
  const safe = safeDocumentUrl(href);
  if (!safe) return null;
  if (!safe.startsWith("/api/sharepoint/download?fileKey="))
    return <a {...props} href={safe} download={download} rel="noopener noreferrer">{children}</a>;

  async function openFile() {
    try {
      setError(null);
      const token = useAuthStore.getState().token;
      if (!token) throw new Error("Inicie sesion para acceder al documento.");
      const response = await fetch(safe!, { headers: { Authorization: `Bearer ${token}` }, redirect: "error" });
      if (!response.ok) throw new Error("No fue posible acceder al documento.");
      const blob = await response.blob();
      const previewable = ["application/pdf", "image/png", "image/jpeg", "image/webp"].includes(blob.type);
      const url = URL.createObjectURL(previewable ? blob : new Blob([blob], { type: "application/octet-stream" }));
      const link = document.createElement("a"); link.href = url; link.rel = "noopener noreferrer";
      if (download || !previewable) link.download = new URL(safe!, window.location.origin).searchParams.get("fileKey")?.split("/").pop() ?? "documento";
      else link.target = "_blank";
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "No fue posible acceder al documento."); }
  }
  return <><a {...props} href={safe} onClick={event => { event.preventDefault(); void openFile(); }}>{children}</a>{error && <span role="alert">{error}</span>}</>;
}
