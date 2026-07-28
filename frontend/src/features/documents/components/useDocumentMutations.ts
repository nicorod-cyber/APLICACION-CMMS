import { useMutation, useQueryClient } from "@tanstack/react-query";
import { documentApi, type DocumentPayload } from "./documentApi";
import { documentQueryKeys } from "./documentFormTypes";
export function useDocumentMutations(assetCode?: string) {
 const client = useQueryClient();
 const invalidate = async () => { if (assetCode) await documentQueryKeys.invalidateAsset(client, assetCode); await client.invalidateQueries({ queryKey: ["documents"] }); };
 return {
  create: useMutation({ mutationFn: (payload: Required<Pick<DocumentPayload, "entidadTipo" | "entidadCodigo" | "tipoDocumento">> & DocumentPayload) => documentApi.create(payload), onSuccess: invalidate }),
  update: useMutation({ mutationFn: ({ id, payload }: { id: string; payload: DocumentPayload }) => documentApi.update(id, payload), onSuccess: invalidate }),
  replace: useMutation({ mutationFn: ({ id, payload }: { id: string; payload: Pick<DocumentPayload, "fechaEmision" | "fechaVencimiento" | "sharePointUrl"> & { reason: string } }) => documentApi.replace(id, payload), onSuccess: invalidate }),
  validate: useMutation({ mutationFn: ({ id, comments }: { id: string; comments?: string | null }) => documentApi.validate(id, comments), onSuccess: invalidate }),
  reject: useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => documentApi.reject(id, reason), onSuccess: invalidate }),
  annul: useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => documentApi.annul(id, reason), onSuccess: invalidate })
 };
}