import { NavLink, useParams, useSearchParams } from "react-router-dom";
import { SchedulingPage } from "./SchedulingPage";

const views = [["calendario", "Calendario"], ["kanban", "Kanban"], ["gantt", "Gantt"], ["talleres", "Talleres"], ["alertas", "Alertas"]] as const;

export function SchedulingLayoutPage() {
  const { view } = useParams();
  const [searchParams] = useSearchParams();
  const query = searchParams.toString();
  const validView = views.some(([value]) => value === view);
  if (!validView) return <section className="panel"><h1>Vista no encontrada</h1><p>La vista de programación solicitada no existe.</p></section>;
  return <section className="stack">
    <nav aria-label="Vistas de programación" className="flex gap-2 overflow-x-auto border-b border-slate-200 pb-2 dark:border-slate-800">
      {views.map(([value, label]) => <NavLink key={value} to={`/programacion/${value}${query ? `?${query}` : ""}`} className={({ isActive }) => `rounded-md px-3 py-2 text-sm font-medium ${isActive ? "bg-teal-600 text-white" : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"}`}>{label}</NavLink>)}
    </nav>
    <SchedulingPage activeView={view} />
  </section>;
}


