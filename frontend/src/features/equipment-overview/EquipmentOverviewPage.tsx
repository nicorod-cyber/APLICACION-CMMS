import { useMemo, useState } from "react";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { apiFetch } from "../auth/authStore";
import { normalizeFaenaZone } from "../faenas/faenaZones";
import { EquipmentOverviewFilters } from "./EquipmentOverviewFilters";
import { EquipmentOverviewTable } from "./EquipmentOverviewTable";
import { EquipmentPageHeader } from "./EquipmentPageHeader";
import { EquipmentRepresentationBanner } from "./EquipmentRepresentationBanner";
import { EquipmentSummaryCards } from "./EquipmentSummaryCards";
import type { AssetCatalog, EquipmentOverviewFiltersValue, EquipmentOverviewRow, Page } from "./types";
import { AssetEditorDialog } from "../assets/components/AssetEditorDialog";

const emptyFilters: EquipmentOverviewFiltersValue = { search: "", faenaCodigo: "", zona: "", tipoActivoCodigo: "", estadoOperacionalCodigo: "", tipoUbicacionFisica: "", tallerCodigo: "" };

export function EquipmentOverviewPage() {
  const navigate = useNavigate();
  const [draft, setDraft] = useState(emptyFilters);
  const [filters, setFilters] = useState(emptyFilters);
  const [newOpen, setNewOpen] = useState(false);
  const catalogQuery = useQuery({ queryKey: ["asset-catalog"], queryFn: async () => { const result = await apiFetch<AssetCatalog>("/api/assets/catalog"); if (!result || !Array.isArray(result.tiposActivo) || !Array.isArray(result.estadosOperacionales)) throw new Error("El cat?logo de equipos tiene un formato inv?lido."); return result; } });
  const overviewQuery = useInfiniteQuery({
    queryKey: ["equipment-overview", filters], initialPageParam: 1,
    queryFn: async ({ pageParam }) => { const params = new URLSearchParams({ page: String(pageParam), pageSize: "25" }); if (filters.search) params.set("search", filters.search); if (filters.faenaCodigo) params.set("faenaCodigo", filters.faenaCodigo); const normalizedZone = normalizeFaenaZone(filters.zona); if (normalizedZone) params.set("zona", normalizedZone); else if (filters.zona.trim()) params.set("zona", filters.zona.trim()); if (filters.tipoActivoCodigo) params.set("tipoActivoCodigo", filters.tipoActivoCodigo); if (filters.estadoOperacionalCodigo) params.set("estadoOperacionalCodigo", filters.estadoOperacionalCodigo); if (filters.tipoUbicacionFisica) params.set("tipoUbicacionFisica", filters.tipoUbicacionFisica); if (filters.tallerCodigo) params.set("tallerCodigo", filters.tallerCodigo); const result = await apiFetch<Page<EquipmentOverviewRow>>("/api/assets/equipment-overview?" + params.toString()); if (!result || !Array.isArray(result.items)) throw new Error("La respuesta de equipos tiene un formato inv?lido."); return result; },
    getNextPageParam: last => last.hasNextPage ? last.page + 1 : undefined
  });
  const rows = useMemo(() => { const seen = new Set<string>(); return (overviewQuery.data?.pages.flatMap(page => page.items) || []).filter(row => !seen.has(row.rowId) && (seen.add(row.rowId), true)); }, [overviewQuery.data]);
  const total = overviewQuery.data?.pages[0]?.totalCount || 0;
  const catalog = catalogQuery.data || { tiposActivo: [], estadosOperacionales: [] };
  const error = overviewQuery.error || catalogQuery.error;
  const loading = overviewQuery.isLoading || catalogQuery.isLoading;
  function applyFilters() { if (JSON.stringify(draft) === JSON.stringify(filters)) void overviewQuery.refetch(); else setFilters({ ...draft }); }
  function open(row: EquipmentOverviewRow) { if (row.rowType === "COMPOSITE_UNIT") navigate("/equipos/unidades/" + encodeURIComponent(row.code) + "?tab=composicion"); else navigate("/equipos/activos/" + encodeURIComponent(row.code)); }
  return <section className="equipment-theme-scope space-y-4">
    <EquipmentPageHeader onNew={() => setNewOpen(true)} />
    <EquipmentRepresentationBanner />
    <EquipmentSummaryCards metrics={{ total, composite: null, loose: null, nonOperational: null, expiringDocuments: null }} />
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <EquipmentOverviewFilters value={draft} catalog={catalog} onChange={setDraft} onApply={applyFilters} disabled={loading} />
      {error ? <div className="m-4 error-banner">No fue posible cargar los equipos. <button className="underline" type="button" onClick={() => void overviewQuery.refetch()}>Reintentar</button></div> : null}
      {loading ? <p className="p-5 text-sm text-slate-500">Cargando equipos?</p> : rows.length === 0 ? <p className="p-5 text-sm text-slate-500">No hay equipos que coincidan con los filtros.</p> : <EquipmentOverviewTable rows={rows} onOpen={open} />}
      {overviewQuery.hasNextPage ? <div className="border-t border-slate-200 p-4 text-center"><button className="secondary-button" disabled={overviewQuery.isFetchingNextPage} type="button" onClick={() => void overviewQuery.fetchNextPage()}>{overviewQuery.isFetchingNextPage ? "Cargando m?s?" : "Cargar m?s equipos"}</button></div> : null}
      <div className="border-t border-teal-100 bg-teal-50 px-4 py-3 text-xs text-teal-800">Los componentes montados no se duplican en el listado general. Accede a ellos desde la composición del camión fábrica.</div>
    </section>
    <button className="secondary-button" type="button" onClick={() => void overviewQuery.refetch()}><RefreshCw className="h-4 w-4" />Actualizar resultados</button>
    <AssetEditorDialog open={newOpen} onClose={() => setNewOpen(false)} onSaved={assetCode => { setNewOpen(false); navigate("/equipos/activos/" + encodeURIComponent(assetCode)); }} />
  </section>;
}