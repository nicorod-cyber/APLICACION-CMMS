import { FormEvent, useEffect, useMemo, useState } from "react";
import { Lock, Save, Unlock, UserPlus } from "lucide-react";
import { AUTH_ROLES, apiFetch, type CurrentUser } from "../auth/authStore";
import { FaenaChecklist } from "../faenas/FaenaSelect";

const roleOptions = [
  { code: AUTH_ROLES.admin, label: "Admin" },
  { code: AUTH_ROLES.planner, label: "Planificador" },
  { code: AUTH_ROLES.maintenanceSupervisor, label: "Supervisor mant." },
  { code: AUTH_ROLES.technician, label: "Tecnico" },
  { code: AUTH_ROLES.warehouse, label: "Bodeguero" },
  { code: AUTH_ROLES.warehouseSupervisor, label: "Supervisor bodega" },
  { code: AUTH_ROLES.management, label: "Gerencia" },
  { code: AUTH_ROLES.faenaViewer, label: "Consulta faena" }
];

type CreateFormState = {
  username: string;
  email: string;
  displayName: string;
  password: string;
  roles: string[];
  faenas: string;
};

const emptyCreateForm: CreateFormState = {
  username: "",
  email: "",
  displayName: "",
  password: "",
  roles: [AUTH_ROLES.faenaViewer],
  faenas: ""
};

export function UsersAdminPage() {
  const [users, setUsers] = useState<CurrentUser[]>([]);
  const [form, setForm] = useState<CreateFormState>(emptyCreateForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadUsers();
  }, []);

  async function loadUsers() {
    setIsLoading(true);
    setError(null);

    try {
      const userResult = await apiFetch<CurrentUser[]>("/api/users");
      setUsers(userResult);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar usuarios.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSaving(true);

    try {
      const created = await apiFetch<CurrentUser>("/api/users", {
        method: "POST",
        body: JSON.stringify({
          username: form.username,
          email: form.email,
          displayName: form.displayName,
          password: form.password,
          roles: form.roles,
          faenas: parseList(form.faenas),
          isActive: true
        })
      });

      setUsers((current) => [...current, created].sort((a, b) => a.displayName.localeCompare(b.displayName)));
      setForm(emptyCreateForm);
      setMessage("Usuario creado.");
    } catch (createError) {
      setError(createError instanceof Error ? createError.message : "No fue posible crear el usuario.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Administracion de usuarios</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Roles, permisos y faenas autorizadas.</p>
      </div>

      <form
        className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900"
        onSubmit={(event) => void handleCreate(event)}
      >
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Usuario" value={form.username} onChange={(value) => setForm({ ...form, username: value })} />
          <Field label="Email" value={form.email} onChange={(value) => setForm({ ...form, email: value })} />
          <Field label="Nombre" value={form.displayName} onChange={(value) => setForm({ ...form, displayName: value })} />
          <Field
            label="Clave inicial"
            type="password"
            value={form.password}
            onChange={(value) => setForm({ ...form, password: value })}
          />
        </div>

        <div className="mt-4 grid gap-4 xl:grid-cols-[1.3fr_0.7fr]">
          <RolePicker value={form.roles} onChange={(roles) => setForm({ ...form, roles })} />
          <FaenaChecklist value={parseList(form.faenas)} onChange={(faenas) => setForm({ ...form, faenas: faenas.join("; ") })} />
        </div>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            disabled={isSaving}
            type="submit"
          >
            <UserPlus className="h-4 w-4" aria-hidden="true" />
            Crear usuario
          </button>
          {message ? <span className="text-sm text-emerald-700 dark:text-emerald-300">{message}</span> : null}
          {error ? <span className="text-sm text-red-700 dark:text-red-300">{error}</span> : null}
        </div>
      </form>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Usuarios</h2>
        </div>

        {isLoading ? (
          <div className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando usuarios...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
                <tr>
                  <th className="px-4 py-3 font-medium">Usuario</th>
                  <th className="px-4 py-3 font-medium">Estado</th>
                  <th className="px-4 py-3 font-medium">Roles</th>
                  <th className="px-4 py-3 font-medium">Faenas</th>
                  <th className="px-4 py-3 font-medium">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {users.map((user) => (
                  <UserRow key={user.id} user={user} onUpdated={setUpdatedUser} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  );

  function setUpdatedUser(updated: CurrentUser) {
    setUsers((current) => current.map((user) => (user.id === updated.id ? updated : user)));
  }
}

type UserRowProps = {
  user: CurrentUser;
  onUpdated: (user: CurrentUser) => void;
};

function UserRow({ user, onUpdated }: UserRowProps) {
  const [roles, setRoles] = useState(user.roles);
  const [faenas, setFaenas] = useState(user.faenas.join("; "));
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setRoles(user.roles);
    setFaenas(user.faenas.join("; "));
  }, [user]);

  const statusLabel = useMemo(() => {
    if (user.isLocked) {
      return "Bloqueado";
    }

    return user.isActive ? "Activo" : "Inactivo";
  }, [user.isActive, user.isLocked]);

  async function saveRoles() {
    await save(() =>
      apiFetch<CurrentUser>(`/api/users/${user.id}/roles`, {
        method: "POST",
        body: JSON.stringify({ roles })
      })
    );
  }

  async function saveFaenas() {
    await save(() =>
      apiFetch<CurrentUser>(`/api/users/${user.id}/faenas`, {
        method: "POST",
        body: JSON.stringify({ faenas: parseList(faenas) })
      })
    );
  }

  async function toggleLock() {
    await save(() =>
      apiFetch<CurrentUser>(`/api/users/${user.id}/${user.isLocked ? "unlock" : "lock"}`, {
        method: "POST"
      })
    );
  }

  async function save(operation: () => Promise<CurrentUser>) {
    setIsSaving(true);
    setError(null);

    try {
      onUpdated(await operation());
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <tr className="align-top">
      <td className="px-4 py-3">
        <div className="font-semibold text-slate-900 dark:text-slate-100">{user.displayName}</div>
        <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">{user.username}</div>
        <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">{user.email}</div>
        {error ? <div className="mt-2 text-xs text-red-700 dark:text-red-300">{error}</div> : null}
      </td>
      <td className="px-4 py-3">
        <span
          className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${
            user.isLocked
              ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
              : "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
          }`}
        >
          {statusLabel}
        </span>
      </td>
      <td className="min-w-72 px-4 py-3">
        <RolePicker compact value={roles} onChange={setRoles} />
        <button
          className="mt-3 inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 px-3 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          disabled={isSaving}
          onClick={() => void saveRoles()}
          type="button"
        >
          <Save className="h-3.5 w-3.5" aria-hidden="true" />
          Roles
        </button>
      </td>
      <td className="min-w-64 px-4 py-3">
        <FaenaChecklist compact value={parseList(faenas)} onChange={(next) => setFaenas(next.join("; "))} />
        <button
          className="mt-3 inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 px-3 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          disabled={isSaving}
          onClick={() => void saveFaenas()}
          type="button"
        >
          <Save className="h-3.5 w-3.5" aria-hidden="true" />
          Faenas
        </button>
      </td>
      <td className="px-4 py-3">
        <button
          aria-label={user.isLocked ? "Desbloquear usuario" : "Bloquear usuario"}
          title={user.isLocked ? "Desbloquear usuario" : "Bloquear usuario"}
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-slate-200 text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          disabled={isSaving}
          onClick={() => void toggleLock()}
          type="button"
        >
          {user.isLocked ? <Unlock className="h-4 w-4" aria-hidden="true" /> : <Lock className="h-4 w-4" aria-hidden="true" />}
        </button>
      </td>
    </tr>
  );
}

type RolePickerProps = {
  value: string[];
  onChange: (roles: string[]) => void;
  compact?: boolean;
};

function RolePicker({ value, onChange, compact }: RolePickerProps) {
  function toggleRole(role: string) {
    onChange(value.includes(role) ? value.filter((item) => item !== role) : [...value, role]);
  }

  return (
    <div className={`grid gap-2 ${compact ? "grid-cols-2" : "sm:grid-cols-2 xl:grid-cols-4"}`}>
      {roleOptions.map((role) => (
        <label
          key={role.code}
          className="flex min-h-9 items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200"
        >
          <input checked={value.includes(role.code)} onChange={() => toggleRole(role.code)} type="checkbox" />
          <span>{role.label}</span>
        </label>
      ))}
    </div>
  );
}

type FieldProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
};

function Field({ label, value, onChange, type = "text" }: FieldProps) {
  const id = label.toLowerCase().replace(/\s+/g, "-");

  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function parseList(value: string) {
  return value
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}
