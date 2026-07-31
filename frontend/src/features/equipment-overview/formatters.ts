import type { DocumentRequirementStatus, EquipmentOverviewRow } from "./types";

export const na = (value?: string | number | null) => value === null || value === undefined || value === "" ? "NA" : String(value);

export function formatDate(value?: string | null) {
  if (!value) return "NA";
  const date = new Date(value + "T00:00:00");
  return Number.isNaN(date.getTime()) ? "NA" : new Intl.DateTimeFormat("es-CL").format(date);
}

export function formatUsage(row: EquipmentOverviewRow) {
  if (row.usageSinceLastPreventive === null || row.usageSinceLastPreventive === undefined || !row.usageUnit) return "NA";
  return new Intl.NumberFormat("es-CL", { maximumFractionDigits: 2 }).format(row.usageSinceLastPreventive) + " " + row.usageUnit;
}

export function formatLocation(row: EquipmentOverviewRow) {
  if (!row.physicalLocationType || !row.physicalLocationName) return "NA";
  return row.physicalLocationType === "FAENA" ? "En faena" : row.physicalLocationName;
}

export function documentLabel(value?: DocumentRequirementStatus | null) {
  if (!value) return "NA";
  if (value.applies === false) return "No aplica";
  const labels: Record<string, string> = {
    Vigente: "Vigente", PorVencer: "Por vencer", Vencido: "Vencido", PendienteCarga: "Pendiente de carga",
    PendienteValidacion: "Pendiente de validación", Rechazado: "Rechazado", PENDIENTE_CARGA: "Pendiente de carga",
    PENDIENTE_VALIDACION: "Pendiente de validación", POR_VENCER: "Por vencer", VIGENTE: "Vigente", VENCIDO: "Vencido", RECHAZADO: "Rechazado"
  };
  return value.status ? labels[value.status] || "NA" : "NA";
}

export function rowTypeLabel(type: EquipmentOverviewRow["rowType"]) {
  if (type === "COMPOSITE_UNIT") return "Unidad compuesta";
  return type === "LOOSE_COMPONENT" ? "Componente suelto" : "Activo";
}