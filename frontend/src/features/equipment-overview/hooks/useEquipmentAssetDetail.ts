import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../auth/authStore";

export function useEquipmentAssetDetail(code:string) {
  const detail=useQuery({queryKey:["equipment-asset-detail",code],enabled:!!code,queryFn:()=>apiFetch<any>("/api/assets/"+encodeURIComponent(code))});
  const location=useQuery({queryKey:["equipment-asset-location",code],enabled:!!code,queryFn:()=>apiFetch<any>("/api/assets/"+encodeURIComponent(code)+"/physical-location")});
  const history=useQuery({queryKey:["equipment-asset-location-history",code],enabled:!!code,queryFn:()=>apiFetch<any[]>("/api/assets/"+encodeURIComponent(code)+"/physical-location-history")});
  const matrix=useQuery({queryKey:["equipment-asset-document-matrix",code],enabled:!!code,queryFn:()=>apiFetch<any[]>("/api/assets/"+encodeURIComponent(code)+"/document-matrix")});
  return {detail,location,history,matrix};
}