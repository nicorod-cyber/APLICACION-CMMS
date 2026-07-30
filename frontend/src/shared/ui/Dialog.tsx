import { useEffect, useRef } from "react";
import { X } from "lucide-react";

type DialogProps = {
  open: boolean;
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  footer?: React.ReactNode;
  busy?: boolean;
  className?: string;
};

export function Dialog({ open, title, onClose, children, footer, busy = false, className = "" }: DialogProps) {
  const close = useRef<HTMLButtonElement>(null);
  const onCloseRef = useRef(onClose);
  const busyRef = useRef(busy);

  useEffect(() => {
    onCloseRef.current = onClose;
    busyRef.current = busy;
  }, [busy, onClose]);

  const requestClose = () => {
    if (!busyRef.current) onCloseRef.current();
  };

  useEffect(() => {
    if (!open) return;

    const previous = document.activeElement as HTMLElement | null;
    document.body.style.overflow = "hidden";
    close.current?.focus();
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") requestClose();
    };
    window.addEventListener("keydown", escape);

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", escape);
      previous?.focus();
    };
  }, [open]);

  if (!open) return null;

  return <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/60 p-4 dark:bg-black/70" onMouseDown={event => { if (event.target === event.currentTarget) requestClose(); }}><section aria-modal="true" aria-labelledby="dialog-title" role="dialog" className={`flex max-h-[calc(100vh-2rem)] w-full max-w-2xl ${className} flex-col overflow-hidden rounded-xl bg-white shadow-2xl dark:bg-slate-900`}><header className="flex items-center justify-between border-b border-slate-200 dark:border-slate-700 px-5 py-4"><h2 className="font-semibold text-slate-900 dark:text-slate-100" id="dialog-title">{title}</h2><button aria-label="Cerrar diálogo" className="rounded p-1 text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-teal-500" disabled={busy} onClick={requestClose} ref={close} type="button"><X className="h-5 w-5" /></button></header><div className="min-h-0 overflow-y-auto p-5">{children}</div>{footer ? <footer className="border-t border-slate-200 dark:border-slate-700 px-5 py-4">{footer}</footer> : null}</section></div>;
}