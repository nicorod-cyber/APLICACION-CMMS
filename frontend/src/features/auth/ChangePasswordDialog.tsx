import { Eye, EyeOff, KeyRound, X } from "lucide-react";
import { FormEvent, useMemo, useState } from "react";
import { apiFetch } from "./authStore";

type ChangePasswordDialogProps = {
  onClose: () => void;
};

type PasswordField = "current" | "next" | "confirm";

export function ChangePasswordDialog({ onClose }: ChangePasswordDialogProps) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [visible, setVisible] = useState<Record<PasswordField, boolean>>({ current: false, next: false, confirm: false });
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const requirements = useMemo(
    () => [
      { label: "Al menos 12 caracteres", met: newPassword.length >= 12 },
      { label: "Una letra mayúscula", met: /[A-Z]/.test(newPassword) },
      { label: "Una letra minúscula", met: /[a-z]/.test(newPassword) },
      { label: "Un número", met: /\d/.test(newPassword) },
      { label: "Un carácter especial", met: /[^A-Za-z0-9]/.test(newPassword) }
    ],
    [newPassword]
  );

  function toggleVisibility(field: PasswordField) {
    setVisible((current) => ({ ...current, [field]: !current[field] }));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (!currentPassword || !newPassword || !confirmNewPassword) {
      setError("Completa todos los campos.");
      return;
    }

    if (newPassword !== confirmNewPassword) {
      setError("La nueva contraseña y su confirmación no coinciden.");
      return;
    }

    setIsSaving(true);
    try {
      await apiFetch<void>("/api/auth/change-password", {
        method: "POST",
        body: JSON.stringify({ currentPassword, newPassword, confirmNewPassword })
      });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
      setSuccess("Tu contraseña fue actualizada correctamente.");
    } catch (changeError) {
      setError(changeError instanceof Error ? changeError.message : "No fue posible actualizar la contraseña.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4" role="dialog" aria-modal="true" aria-labelledby="change-password-title">
      <form className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl dark:bg-slate-900" onSubmit={(event) => void submit(event)}>
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-slate-950 dark:text-white">
              <KeyRound className="h-5 w-5 text-teal-600 dark:text-teal-400" aria-hidden="true" />
              <h2 id="change-password-title" className="text-lg font-semibold">Cambiar contraseña</h2>
            </div>
            <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">Confirma tu contraseña actual para establecer una nueva.</p>
          </div>
          <button aria-label="Cerrar" className="rounded-md p-1 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800" disabled={isSaving} onClick={onClose} type="button">
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <PasswordInput id="current-password" label="Contraseña actual" value={currentPassword} onChange={setCurrentPassword} visible={visible.current} onToggle={() => toggleVisibility("current")} autoComplete="current-password" />
        <PasswordInput id="new-password" label="Nueva contraseña" value={newPassword} onChange={setNewPassword} visible={visible.next} onToggle={() => toggleVisibility("next")} autoComplete="new-password" />
        <PasswordInput id="confirm-new-password" label="Confirmar nueva contraseña" value={confirmNewPassword} onChange={setConfirmNewPassword} visible={visible.confirm} onToggle={() => toggleVisibility("confirm")} autoComplete="new-password" />

        <ul className="mt-4 grid gap-1 text-xs" aria-label="Reglas de contraseña">
          {requirements.map((requirement) => (
            <li key={requirement.label} className={requirement.met ? "text-emerald-700 dark:text-emerald-300" : "text-slate-500 dark:text-slate-400"}>
              {requirement.met ? "✓" : "○"} {requirement.label}
            </li>
          ))}
        </ul>

        {error ? <p className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-200">{error}</p> : null}
        {success ? <p className="mt-4 rounded-md bg-emerald-50 p-3 text-sm text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-200">{success}</p> : null}

        <div className="mt-6 flex justify-end gap-3">
          <button className="h-10 rounded-md border border-slate-200 px-4 text-sm font-semibold dark:border-slate-700" disabled={isSaving} onClick={onClose} type="button">Cancelar</button>
          <button className="h-10 rounded-md bg-teal-600 px-4 text-sm font-semibold text-white hover:bg-teal-700 disabled:cursor-not-allowed disabled:opacity-60" disabled={isSaving} type="submit">
            {isSaving ? "Actualizando…" : "Actualizar contraseña"}
          </button>
        </div>
      </form>
    </div>
  );
}

type PasswordInputProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  visible: boolean;
  onToggle: () => void;
  autoComplete: "current-password" | "new-password";
};

function PasswordInput({ id, label, value, onChange, visible, onToggle, autoComplete }: PasswordInputProps) {
  return (
    <label className="mt-4 block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <span className="relative mt-1 block">
        <input id={id} className="h-10 w-full rounded-md border border-slate-300 bg-white px-3 pr-10 outline-none ring-teal-500 focus:ring-2 dark:border-slate-700 dark:bg-slate-950" value={value} onChange={(event) => onChange(event.target.value)} type={visible ? "text" : "password"} autoComplete={autoComplete} />
        <button aria-label={visible ? `Ocultar ${label.toLowerCase()}` : `Mostrar ${label.toLowerCase()}`} className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-slate-500" onClick={onToggle} type="button">
          {visible ? <EyeOff className="h-4 w-4" aria-hidden="true" /> : <Eye className="h-4 w-4" aria-hidden="true" />}
        </button>
      </span>
    </label>
  );
}
