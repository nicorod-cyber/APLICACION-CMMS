import { AssetDocumentManager } from "../documents/components/AssetDocumentManager";
type Matrix = { tipoDocumento: string; obligatorio: boolean; critico: boolean; bloqueaDisponibilidad: boolean; estado: string; documentoVigente?: boolean | null; version?: number | null; fechaVencimiento?: string | null; diasParaVencer?: number | null; motivoPendencia?: string | null };
export function AssetDocumentsTab({ code, matrix }: { code: string; matrix: Matrix[] }) { return <AssetDocumentManager assetCode={code} matrix={matrix} />; }
