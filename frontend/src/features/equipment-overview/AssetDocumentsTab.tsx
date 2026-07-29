import { AssetDocumentManager } from "../documents/components/AssetDocumentManager";
import type { DocumentMatrixRow } from "../documents/components/documentFormTypes";
export function AssetDocumentsTab({ code, matrix }: { code: string; matrix: DocumentMatrixRow[] }) { return <AssetDocumentManager assetCode={code} matrix={matrix} />; }
