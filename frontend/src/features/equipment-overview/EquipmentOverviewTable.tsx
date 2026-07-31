import { ChevronRight } from "lucide-react";
import { DocumentRequirementCell } from "./DocumentRequirementCell";
import { OperationalStatusBadge } from "./OperationalStatusBadge";
import { formatDate, formatLocation, formatUsage, na, rowTypeLabel } from "./formatters";
import type { EquipmentOverviewRow } from "./types";

type Header = { label: string; width: string; lines?: string[] };
const headers: Header[] = [
  { label: "Nombre equipo", width: "w-[190px]" }, { label: "Estado", width: "w-[105px]" }, { label: "Lugar", width: "w-[110px]" }, { label: "Tipo de equipo", width: "w-[130px]" },
  { label: "Hrs/Km desde \u00faltimo preventivo", width: "w-[130px]", lines: ["Hrs/Km desde", "\u00faltimo", "preventivo"] },
  { label: "Fecha aproximada pr\u00f3xima mantenci\u00f3n", width: "w-[150px]", lines: ["Fecha aproximada", "pr\u00f3xima", "mantenci\u00f3n"] },
  { label: "Revisi\u00f3n t\u00e9cnica", width: "w-[145px]" }, { label: "Sernageomin", width: "w-[130px]" }, { label: "DGMN", width: "w-[115px]" }, { label: "Supresi\u00f3n de incendio", width: "w-[145px]" },
  { label: "Faena", width: "w-[110px]" }, { label: "Tipo \u00faltimo preventivo", width: "w-[110px]" }, { label: "Zona", width: "w-[80px]" }, { label: "Marca", width: "w-[90px]" }, { label: "A\u00f1o", width: "w-[65px]" }
];
const head = "h-[62px] p-2 align-middle text-[10px] uppercase leading-3 tracking-[.04em] text-slate-500 whitespace-normal break-normal [overflow-wrap:normal]";
const cell = "p-2 align-middle";

export function EquipmentOverviewTable({ rows, onOpen }: { rows: EquipmentOverviewRow[]; onOpen: (row: EquipmentOverviewRow) => void }) {
  return <div className="max-h-[680px] overflow-x-auto overflow-y-auto">
    <table className="min-w-[1850px] table-fixed text-left text-sm">
      <colgroup>{headers.map(header => <col className={header.width} key={header.label} />)}<col className="w-11" /></colgroup>
      <thead className="sticky top-0 z-10 bg-slate-50 shadow-sm dark:bg-slate-950"><tr>{headers.map(header => <th className={`${head} ${header.width}`} key={header.label}>{header.lines ? <span className="block whitespace-nowrap">{header.lines.map((line, index) => <span className="block" key={index}>{line}</span>)}</span> : header.label}</th>)}<th className="w-11 p-2"><span className="sr-only">Abrir</span></th></tr></thead>
      <tbody>{rows.map(row => <tr className="cursor-pointer border-t border-slate-100 align-middle hover:bg-slate-50 focus-within:bg-teal-50" key={row.rowId} tabIndex={0} role="link" onClick={() => onOpen(row)} onKeyDown={event => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); onOpen(row); } }}>
        <td className={`${cell} min-w-0`}><div className="min-w-0"><b className="block truncate text-slate-900">{row.name || "NA"}</b><small className="block truncate text-slate-500">{row.code}</small><span className={"mt-1 inline-flex max-w-full truncate rounded-full px-2 py-0.5 text-[10px] font-extrabold " + (row.rowType === "COMPOSITE_UNIT" ? "bg-sky-100 text-sky-800" : row.rowType === "LOOSE_COMPONENT" ? "bg-orange-50 text-orange-800" : "bg-teal-50 text-teal-800")}>{rowTypeLabel(row.rowType)}</span></div></td>
        <td className={cell}><OperationalStatusBadge code={row.operationalStateCode} name={row.operationalStateName} /></td>
        <td className={cell} title={row.physicalLocationCommune || formatLocation(row)}><div className="truncate">{formatLocation(row)}</div>{row.physicalLocationCommune ? <small className="block truncate text-slate-500">{row.physicalLocationCommune}</small> : null}</td><td className={cell} title={row.equipmentTypeCode || row.equipmentTypeName}><span className="block truncate">{na(row.equipmentTypeName)}</span></td>
        <td className={cell}>{formatUsage(row)}</td><td className={cell}>{formatDate(row.approximateNextMaintenanceDate)}</td>
        <td className={cell}><DocumentRequirementCell value={row.technicalReview} /></td><td className={cell}><DocumentRequirementCell value={row.sernageomin} /></td><td className={cell}><DocumentRequirementCell value={row.dgmn} /></td><td className={cell}><DocumentRequirementCell value={row.fireSuppression} /></td>
        <td className={cell} title={na(row.siteName)}>{na(row.siteName)}</td><td className={cell}>{na(row.lastPreventiveType)}</td><td className={cell} title={na(row.zone)}>{na(row.zone)}</td><td className={cell}>{na(row.brand)}</td><td className={cell}>{na(row.manufacturingYear)}</td>
        <td className="p-2"><button aria-label={"Abrir " + row.name} className="rounded p-1 text-teal-700 hover:bg-teal-100 focus:outline-none focus:ring-2 focus:ring-teal-500" type="button" onClick={event => { event.stopPropagation(); onOpen(row); }}><ChevronRight className="h-5 w-5" /></button></td>
      </tr>)}</tbody>
    </table>
  </div>;
}