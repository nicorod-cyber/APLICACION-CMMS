import { ChevronRight } from "lucide-react";
import { DocumentRequirementCell } from "./DocumentRequirementCell";
import { formatDate, formatLocation, formatUsage, na, rowTypeLabel } from "./formatters";
import type { EquipmentOverviewRow } from "./types";

const headers = ["Zona", "Faena", "Nombre equipo", "Estado", "Lugar", "Tipo de equipo", "Marca", "Año", "Tipo último preventivo", "Hrs/Km desde último preventivo", "Fecha aproximada próxima mantención", "Revisión técnica", "Sernageomin", "DGMN", "Supresión de incendio"];



export function EquipmentOverviewTable({ rows, onOpen }: { rows: EquipmentOverviewRow[]; onOpen: (row: EquipmentOverviewRow) => void }) {
  return <div className="max-h-[680px] overflow-auto">
    <table className="min-w-[2050px] text-left text-sm">
      <thead className="sticky top-0 z-10 bg-slate-50 shadow-sm dark:bg-slate-950"><tr>{headers.map(header => <th className={header === "Nombre equipo" ? "sticky left-0 z-20 min-w-[270px] bg-slate-50 p-3 text-[11px] uppercase tracking-[.05em] text-slate-500 dark:bg-slate-950" : "min-w-36 p-3 text-[11px] uppercase tracking-[.05em] text-slate-500"} key={header}>{header}</th>)}<th className="w-12 p-3"><span className="sr-only">Abrir</span></th></tr></thead>
      <tbody>{rows.map(row => <tr className="cursor-pointer border-t border-slate-100 align-middle hover:bg-slate-50 focus-within:bg-teal-50" key={row.rowId} tabIndex={0} role="link" onClick={() => onOpen(row)} onKeyDown={event => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); onOpen(row); } }}>
        <td className="p-3" title={na(row.zone)}>{na(row.zone)}</td><td className="p-3" title={na(row.siteName)}>{na(row.siteName)}</td>
        <td className="sticky left-0 z-[1] min-w-[270px] bg-white p-3 dark:bg-slate-900"><div className="min-w-0"><b className="block truncate text-slate-900">{row.name || "NA"}</b><small className="block text-slate-500">{row.code}</small><span className={"mt-1 inline-flex rounded-full px-2 py-0.5 text-[10px] font-extrabold " + (row.rowType === "COMPOSITE_UNIT" ? "bg-sky-100 text-sky-800" : row.rowType === "LOOSE_COMPONENT" ? "bg-orange-50 text-orange-800" : "bg-teal-50 text-teal-800")}>{rowTypeLabel(row.rowType)}</span></div></td>
        <td className="p-3" title={row.operationalStateCode}><span className="inline-flex rounded-full bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-700">{na(row.operationalStateName || row.operationalStateCode)}</span></td>
        <td className="p-3" title={row.physicalLocationCommune || formatLocation(row)}><div>{formatLocation(row)}</div>{row.physicalLocationCommune ? <small className="text-slate-500">{row.physicalLocationCommune}</small> : null}</td><td className="p-3" title={row.equipmentTypeCode || row.equipmentTypeName}>{na(row.equipmentTypeName)}</td><td className="p-3">{na(row.brand)}</td><td className="p-3">{na(row.manufacturingYear)}</td><td className="p-3">{na(row.lastPreventiveType)}</td><td className="p-3">{formatUsage(row)}</td><td className="p-3">{formatDate(row.approximateNextMaintenanceDate)}</td>
        <td className="p-3"><DocumentRequirementCell value={row.technicalReview} /></td><td className="p-3"><DocumentRequirementCell value={row.sernageomin} /></td><td className="p-3"><DocumentRequirementCell value={row.dgmn} /></td><td className="p-3"><DocumentRequirementCell value={row.fireSuppression} /></td>
        <td className="p-3"><button aria-label={"Abrir " + row.name} className="rounded p-1 text-teal-700 hover:bg-teal-100 focus:outline-none focus:ring-2 focus:ring-teal-500" type="button" onClick={event => { event.stopPropagation(); onOpen(row); }}><ChevronRight className="h-5 w-5" /></button></td>
      </tr>)}</tbody>
    </table>
  </div>;
}