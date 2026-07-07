type LogoProps = {
  compact?: boolean;
};

export function Logo({ compact = false }: LogoProps) {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-slate-950 text-sm font-bold text-white dark:bg-white dark:text-slate-950">
        CM
      </div>
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold leading-5 text-slate-950 dark:text-white">
          {compact ? "Mantenimiento" : "Mantenimiento [Nombre Empresa]"}
        </p>
        <p className="truncate text-xs text-slate-500 dark:text-slate-400">CMMS</p>
      </div>
    </div>
  );
}

