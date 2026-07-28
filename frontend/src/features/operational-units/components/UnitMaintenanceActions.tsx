import { ClipboardPlus, FilePlus2, Truck } from "lucide-react";
import { useNavigate } from "react-router-dom";

type MaintenanceActionsProps = {
  targetType: "Asset" | "OperationalUnit";
  targetCode: string;
  onNavigate?: (path: string) => void;
};

export function MaintenanceActions({ targetType, targetCode, onNavigate }: MaintenanceActionsProps) {
  const query = "targetType=" + targetType + "&targetCode=" + encodeURIComponent(targetCode);

  const noticePath = "/avisos?" + query;
  const orderPath = "/ot?" + query;
  return <div className="grid gap-2">
    {onNavigate ? <button className="secondary-button" type="button" onClick={() => onNavigate(noticePath)}><FilePlus2 className="h-4 w-4" />Crear aviso</button> : <a className="secondary-button" href={noticePath}><FilePlus2 className="h-4 w-4" />Crear aviso</a>}
    {onNavigate ? <button className="secondary-button" type="button" onClick={() => onNavigate(orderPath)}><ClipboardPlus className="h-4 w-4" />Crear OT</button> : <a className="secondary-button" href={orderPath}><ClipboardPlus className="h-4 w-4" />Crear OT</a>}
  </div>;
}

export function UnitMaintenanceActions({ unitCode, canTransfer = true }: { unitCode: string; canTransfer?: boolean }) {
  const navigate = useNavigate();
  return <>
    <MaintenanceActions targetType="OperationalUnit" targetCode={unitCode} onNavigate={navigate} />
    {!canTransfer ? <p className="mt-2 text-xs text-slate-500"><Truck className="mr-1 inline h-3 w-3" />No autorizado para trasladar.</p> : null}
  </>;
}