import { Info } from "lucide-react";

export function EquipmentRepresentationBanner() {
  return <div className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-[10px] border border-amber-200 bg-amber-50 px-4 py-3 text-xs text-amber-800"><Info className="h-4 w-4 shrink-0" /><b>Regla de representación:</b><span>Chasis + Fábrica vigentes → Camión fábrica. Componentes sin montaje vigente → aparecen individualmente.</span></div>;
}