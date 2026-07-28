import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { na } from "./formatters";

type Node={codigo:string;nombre:string;nivel:string;codigoPadre?:string|null;familiasEquipo:string[];obsoleto:boolean;ruta:string};
type Tree={node:Node;children:Tree[]};
export function AssetHierarchyTab({ familyCode }: { familyCode?:string|null }) {
 const [selected,setSelected]=useState<Node|null>(null);
 const [expanded,setExpanded]=useState<string[]>([]);
 const [collapsedRoots,setCollapsedRoots]=useState<string[]>([]);
 const query=useQuery({queryKey:["equipment-asset-hierarchy",familyCode],enabled:!!familyCode,queryFn:()=>apiFetch<Tree[]>("/api/technical-hierarchy/tree?familia="+encodeURIComponent(familyCode!)+"&includeObsolete=false")});
 if(!familyCode)return <p className="rounded-lg border border-dashed border-slate-300 p-5 text-sm text-slate-500">NA</p>;
 if(query.isLoading)return <p className="text-sm text-slate-500">Cargando jerarquía…</p>;
 if(query.error)return <button className="secondary-button" onClick={()=>void query.refetch()} type="button">Reintentar jerarquía</button>;
 const rows=query.data??[]; if(!rows.length)return <p className="rounded-lg border border-dashed border-slate-300 p-5 text-sm text-slate-500">No existe jerarquía técnica para esta familia.</p>;
 const render=(item:Tree,depth=0):React.ReactNode=>{
   const hasChildren=item.children.length>0;
   const isExpanded=depth===0?!collapsedRoots.includes(item.node.codigo):expanded.includes(item.node.codigo);
   const childrenId="asset-hierarchy-"+item.node.codigo;
   const toggle=()=>depth===0?setCollapsedRoots(current=>current.includes(item.node.codigo)?current.filter(code=>code!==item.node.codigo):[...current,item.node.codigo]):setExpanded(current=>current.includes(item.node.codigo)?current.filter(code=>code!==item.node.codigo):[...current,item.node.codigo]);
   return <div key={item.node.codigo}><div className="flex items-center border-b border-slate-100" style={{paddingLeft:12+depth*18}}>{hasChildren?<button aria-controls={childrenId} aria-expanded={isExpanded} className="mr-1 rounded p-1 hover:bg-slate-100" onClick={toggle} type="button">{isExpanded?<ChevronDown className="h-4 w-4"/>:<ChevronRight className="h-4 w-4"/>}</button>:<span className="mr-1 h-6 w-6"/>}<button className={"min-w-0 flex-1 px-2 py-2 text-left text-sm hover:bg-teal-50 "+(selected?.codigo===item.node.codigo?"bg-teal-50 font-bold text-teal-800":"")} onClick={()=>setSelected(item.node)} type="button">{item.node.nombre}</button></div>{hasChildren&&isExpanded?<div id={childrenId}>{item.children.map(child=>render(child,depth+1))}</div>:null}</div>;
 }; const current=selected??rows[0].node;return <div className="grid gap-4 md:grid-cols-[280px_1fr]"><div className="overflow-hidden rounded-lg border border-slate-200">{rows.map(item=><div key={item.node.codigo}>{render(item)}</div>)}</div><article className="rounded-lg border border-slate-200 p-4"><p className="text-[11px] font-extrabold uppercase tracking-[.12em] text-teal-700">Componente técnico</p><h3 className="mt-1 text-lg font-semibold">{current.nombre}</h3><p className="mt-3 rounded bg-teal-50 p-2 text-xs text-teal-800">{current.ruta}</p><div className="mt-3 grid gap-2 sm:grid-cols-2">{[["Código",current.codigo],["Nivel",current.nivel],["Familias",current.familiasEquipo.join(", ")],["Estado",current.obsoleto?"Obsoleto":"Vigente"],["Criticidad","NA"],["Mantenible","NA"]].map(([label,value])=><div className="rounded border border-slate-200 bg-slate-50 p-2" key={label}><small className="block text-slate-500">{label}</small><b className="text-sm">{na(value)}</b></div>)}</div></article></div>;
}