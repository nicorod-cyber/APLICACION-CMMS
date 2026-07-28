import { useQuery } from "@tanstack/react-query";
import { Dialog } from "../../../shared/ui/Dialog";
import { documentApi } from "./documentApi";
import { documentQueryKeys } from "./documentFormTypes";

const date = (value?: string | null) => value ? new Intl.DateTimeFormat("es-CL").format(new Date(value)) : "NA";

export function DocumentVersionsDialog({ open, documentId, onClose }: { open: boolean; documentId?: string | null; onClose: () => void }) {
  const query = useQuery({ queryKey: documentQueryKeys.versions(documentId ?? ""), enabled: open && !!documentId, queryFn: () => documentApi.versions(documentId!) });
  return <Dialog open={open} onClose={onClose} title="Versiones documentales"><table className="min-w-full text-sm"><thead><tr><th>Version</th><th>Estado</th><th>Carga</th><th>Usuario</th><th>Archivo</th></tr></thead><tbody>{query.isLoading ? <tr><td colSpan={5}>Cargando...</td></tr> : query.error ? <tr><td className="error-banner" colSpan={5}>{query.error instanceof Error ? query.error.message : "No fue posible cargar versiones."}</td></tr> : query.data?.length ? query.data.map(version => <tr className="border-t" key={version.versionId}><td className="p-2">{version.codigoVersion}</td><td className="p-2">{version.estadoValidacion ?? (version.vigente ? "Vigente" : "Historica")}</td><td className="p-2">{date(version.fechaCargaUtc)}</td><td className="p-2">{version.cargadoPor}</td><td className="p-2">{version.sharePointUrl ? <><a className="text-teal-700 underline" href={version.sharePointUrl} target="_blank" rel="noreferrer">Abrir</a><a className="ml-2 text-teal-700 underline" download href={version.sharePointUrl}>Descargar</a></> : "NA"}{version.motivoRechazo ? <small className="block text-red-700">{version.motivoRechazo}</small> : null}</td></tr>) : <tr><td className="p-3 text-slate-500" colSpan={5}>No hay versiones.</td></tr>}</tbody></table></Dialog>;
}
