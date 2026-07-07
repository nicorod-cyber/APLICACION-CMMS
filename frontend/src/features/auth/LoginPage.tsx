import { ShieldCheck } from "lucide-react";
import { FormEvent, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { Logo } from "../../shared/ui/Logo";
import { ThemeToggle } from "../../shared/layout/ThemeToggle";
import { useAuthStore } from "./authStore";

export function LoginPage() {
  const navigate = useNavigate();
  const token = useAuthStore((state) => state.token);
  const login = useAuthStore((state) => state.login);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (token) {
    return <Navigate to="/dashboard" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await login(username, password);
      navigate("/dashboard", { replace: true });
    } catch (loginError) {
      setError(loginError instanceof Error ? loginError.message : "No fue posible iniciar sesion.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="min-h-screen bg-slate-100 text-slate-950 dark:bg-slate-950 dark:text-slate-100">
      <div className="mx-auto grid min-h-screen max-w-6xl grid-cols-1 lg:grid-cols-[1fr_420px]">
        <section className="flex flex-col justify-between px-6 py-8 md:px-10">
          <div className="flex items-center justify-between">
            <Logo />
            <ThemeToggle />
          </div>

          <div className="max-w-3xl py-16">
            <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-teal-200 bg-teal-50 px-3 py-1 text-sm font-medium text-teal-800 dark:border-teal-900 dark:bg-teal-950 dark:text-teal-200">
              <ShieldCheck className="h-4 w-4" aria-hidden="true" />
              CMMS industrial
            </div>
            <h1 className="text-4xl font-semibold leading-tight text-slate-950 dark:text-white md:text-5xl">
              Mantenimiento [Nombre Empresa]
            </h1>
            <div className="mt-8 grid max-w-2xl grid-cols-1 gap-3 sm:grid-cols-3">
              {["OT", "Activos", "Bodega"].map((item) => (
                <div
                  key={item}
                  className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900"
                >
                  <span className="text-sm font-semibold text-slate-700 dark:text-slate-200">{item}</span>
                  <div className="mt-3 h-2 rounded-full bg-slate-100 dark:bg-slate-800">
                    <div className="h-2 w-2/3 rounded-full bg-teal-500" />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <p className="text-sm text-slate-500 dark:text-slate-400">Excel-first. SQL-ready.</p>
        </section>

        <section className="flex items-center px-6 py-8 md:px-10">
          <form
            className="w-full rounded-lg border border-slate-200 bg-white p-6 shadow-panel dark:border-slate-800 dark:bg-slate-900"
            onSubmit={(event) => void handleSubmit(event)}
          >
            <h2 className="text-xl font-semibold text-slate-950 dark:text-white">Ingreso</h2>
            <label className="mt-6 block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor="username">
              Usuario
            </label>
            <input
              id="username"
              className="mt-2 h-11 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
              type="text"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
            />
            <label className="mt-4 block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor="password">
              Clave
            </label>
            <input
              id="password"
              className="mt-2 h-11 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
            {error ? (
              <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">
                {error}
              </div>
            ) : null}
            <button
              className="mt-6 inline-flex h-11 w-full items-center justify-center rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 focus:outline-none focus:ring-2 focus:ring-teal-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
              disabled={isSubmitting}
              type="submit"
            >
              {isSubmitting ? "Ingresando..." : "Entrar"}
            </button>
          </form>
        </section>
      </div>
    </main>
  );
}
