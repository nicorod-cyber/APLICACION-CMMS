import { AssetDocumentManager } from "../documents/components/AssetDocumentManager";
type Matrix = { tipoDocumento: string; bloqueaDisponibilidad: boolean; estado: string; fechaVencimiento?: string | null };
export function AssetDocumentsTab({ code, matrix }: { code: string; matrix: Matrix[] }) { return <AssetDocumentManager assetCode={code} matrix={matrix} />; }
