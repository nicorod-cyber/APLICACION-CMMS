import { FormEvent, useEffect, useMemo, useState } from "react";
import { BellRing, CheckCircle2, Eye, FileText, Mail, RefreshCw, Save, Send, XCircle } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { AUTH_PERMISSIONS, apiFetch, useAuthStore } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type AlertSeverityLevel = "Info" | "Warning" | "Critical";
type AlertStatus = "Open" | "Acknowledged" | "Resolved";
type NotificationStatus = "Pending" | "Sent" | "Failed";

type AlertRecord = {
  alertId: string;
  ruleCode: string;
  title: string;
  message: string;
  severity: AlertSeverityLevel;
  status: AlertStatus;
  source: string;
  causeKey: string;
  faenaCodigo?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  isCriticalRepeat: boolean;
  repeatCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  acknowledgedAtUtc?: string | null;
  acknowledgedBy?: string | null;
  resolvedAtUtc?: string | null;
  resolvedBy?: string | null;
  resolutionReason?: string | null;
};

type AlertRule = {
  code: string;
  name: string;
  eventType: string;
  enabled: boolean;
  severity: AlertSeverityLevel;
  repeatUntilResolved: boolean;
  generateEmail: boolean;
  generatePdf: boolean;
  templateId: string;
  recipients: string;
  faenaCodigo?: string | null;
};

type NotificationRecord = {
  notificationId: string;
  alertId: string;
  subject: string;
  body: string;
  recipients: string;
  status: NotificationStatus;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  provider?: string | null;
  pdfFileKey?: string | null;
  pdfPath?: string | null;
  error?: string | null;
};

type PdfTemplate = {
  templateId: string;
  name: string;
  eventType: string;
  subjectTemplate: string;
  htmlTemplate: string;
  active: boolean;
  updatedAtUtc: string;
};

type RuleForm = AlertRule & { reason: string };
type TemplateForm = PdfTemplate & { reason: string };
type Tab = "alerts" | "rules" | "notifications" | "templates";

const emptyRule: RuleForm = {
  code: "",
  name: "",
  eventType: "",
  enabled: true,
  severity: "Warning",
  repeatUntilResolved: false,
  generateEmail: true,
  generatePdf: false,
  templateId: "alert-default",
  recipients: "",
  faenaCodigo: "",
  reason: ""
};

const emptyTemplate: TemplateForm = {
  templateId: "",
  name: "",
  eventType: "alert",
  subjectTemplate: "[CMMS] {{Title}}",
  htmlTemplate: "",
  active: true,
  updatedAtUtc: "",
  reason: ""
};

export function AlertsPage() {
  const user = useAuthStore((state) => state.user);
  const [alerts, setAlerts] = useState<AlertRecord[]>([]);
  const [rules, setRules] = useState<AlertRule[]>([]);
  const [notifications, setNotifications] = useState<NotificationRecord[]>([]);
  const [templates, setTemplates] = useState<PdfTemplate[]>([]);
  const [selectedAlert, setSelectedAlert] = useState<AlertRecord | null>(null);
  const [selectedNotification, setSelectedNotification] = useState<NotificationRecord | null>(null);
  const [ruleForm, setRuleForm] = useState<RuleForm>(emptyRule);
  const [templateForm, setTemplateForm] = useState<TemplateForm>(emptyTemplate);
  const [previewHtml, setPreviewHtml] = useState("");
  const [activeTab, setActiveTab] = useState<Tab>("alerts");
  const [includeResolved, setIncludeResolved] = useState(false);
  const [resolveReason, setResolveReason] = useState("");
  const [testRecipient, setTestRecipient] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canConfigure = Boolean(user?.permissions.includes(AUTH_PERMISSIONS.configureAlerts) || user?.permissions.includes(AUTH_PERMISSIONS.administration));

  useEffect(() => {
    void loadAll();
  }, [includeResolved]);

  const openAlerts = useMemo(() => alerts.filter((alert) => alert.status !== "Resolved"), [alerts]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);

    try {
      const query = includeResolved ? "?includeResolved=true" : "";
      const [alertResult, ruleResult, notificationResult, templateResult] = await Promise.all([
        apiFetch<AlertRecord[]>(`/api/alerts${query}`),
        apiFetch<AlertRule[]>("/api/alerts/rules"),
        apiFetch<NotificationRecord[]>("/api/notifications"),
        apiFetch<PdfTemplate[]>("/api/pdf/templates")
      ]);

      setAlerts(alertResult);
      setRules(ruleResult);
      setNotifications(notificationResult);
      setTemplates(templateResult);
      setSelectedAlert((current) => current ? alertResult.find((item) => item.alertId === current.alertId) ?? alertResult[0] ?? null : alertResult[0] ?? null);
      setSelectedNotification((current) =>
        current ? notificationResult.find((item) => item.notificationId === current.notificationId) ?? notificationResult[0] ?? null : notificationResult[0] ?? null
      );
      if (!ruleForm.code && ruleResult[0]) {
        setRuleForm(toRuleForm(ruleResult[0]));
      }
      if (!templateForm.templateId && templateResult[0]) {
        setTemplateForm(toTemplateForm(templateResult[0]));
        setPreviewHtml(templateResult[0].htmlTemplate);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar alertas.");
    } finally {
      setIsLoading(false);
    }
  }

  function selectRule(rule: AlertRule) {
    setRuleForm(toRuleForm(rule));
    setMessage(null);
    setError(null);
  }

  function selectTemplate(template: PdfTemplate) {
    setTemplateForm(toTemplateForm(template));
    setPreviewHtml(template.htmlTemplate);
    setMessage(null);
    setError(null);
  }

  async function acknowledgeSelected() {
    if (!selectedAlert) {
      return;
    }

    await runAction(
      () => apiFetch<AlertRecord>(`/api/alerts/${encodeURIComponent(selectedAlert.alertId)}/acknowledge`, { method: "POST" }),
      "Alerta reconocida."
    );
  }

  async function resolveSelected() {
    if (!selectedAlert) {
      return;
    }

    await runAction(
      () =>
        apiFetch<AlertRecord>(`/api/alerts/${encodeURIComponent(selectedAlert.alertId)}/resolve`, {
          method: "POST",
          body: JSON.stringify({ reason: resolveReason })
        }),
      "Alerta resuelta."
    );
    setResolveReason("");
  }

  async function sendTest() {
    if (!selectedAlert) {
      return;
    }

    await runAction(
      () =>
        apiFetch<NotificationRecord>(`/api/alerts/${encodeURIComponent(selectedAlert.alertId)}/send-test`, {
          method: "POST",
          body: JSON.stringify({ recipientEmail: testRecipient || null, comments: "Prueba desde bandeja" })
        }),
      "Notificacion de prueba enviada."
    );
    setTestRecipient("");
  }

  async function runAction<T>(action: () => Promise<T>, successMessage: string) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      await action();
      setMessage(successMessage);
      await loadAll();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : "No fue posible completar la accion.");
    } finally {
      setIsSaving(false);
    }
  }

  async function saveRule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const saved = await apiFetch<AlertRule>(`/api/alerts/rules/${encodeURIComponent(ruleForm.code)}`, {
        method: "PUT",
        body: JSON.stringify(toRulePayload(ruleForm))
      });
      setRuleForm(toRuleForm(saved));
      setMessage("Regla actualizada.");
      await loadAll();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar regla.");
    } finally {
      setIsSaving(false);
    }
  }

  async function saveTemplate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const saved = await apiFetch<PdfTemplate>(`/api/pdf/templates/${encodeURIComponent(templateForm.templateId)}`, {
        method: "PUT",
        body: JSON.stringify(toTemplatePayload(templateForm))
      });
      setTemplateForm(toTemplateForm(saved));
      setPreviewHtml(saved.htmlTemplate);
      setMessage("Plantilla actualizada.");
      await loadAll();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar plantilla.");
    } finally {
      setIsSaving(false);
    }
  }

  async function previewTemplate() {
    if (!templateForm.templateId) {
      return;
    }

    try {
      const preview = await apiFetch<{ html: string }>(`/api/pdf/templates/${encodeURIComponent(templateForm.templateId)}/preview`, {
        method: "POST",
        body: JSON.stringify({
          Title: "Documento vencido",
          Message: "El documento requiere revision.",
          Severity: "Critical",
          Source: "Documentos",
          EntityId: "EQ-001",
          FaenaCodigo: "FAE_COL"
        })
      });
      setPreviewHtml(preview.html);
    } catch (previewError) {
      setError(previewError instanceof Error ? previewError.message : "No fue posible previsualizar.");
    }
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Alertas</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Bandeja, correos, PDF y plantillas HTML.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => void loadAll()}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Actualizar
          </button>
          <label className="flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
            <input checked={includeResolved} onChange={(event) => setIncludeResolved(event.target.checked)} type="checkbox" />
            Resueltas
          </label>
        </div>
      </div>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label="Abiertas" value={openAlerts.length} icon={BellRing} />
        <Metric label="Criticas" value={alerts.filter((item) => item.severity === "Critical" && item.status !== "Resolved").length} icon={XCircle} />
        <Metric label="Notificaciones" value={notifications.length} icon={Mail} />
        <Metric label="Plantillas" value={templates.length} icon={FileText} />
      </section>

      <div className="flex flex-wrap gap-2">
        {[
          ["alerts", "Bandeja"],
          ["rules", "Reglas"],
          ["notifications", "Notificaciones"],
          ["templates", "Plantillas"]
        ].map(([value, label]) => (
          <button
            key={value}
            className={`inline-flex h-9 items-center rounded-md px-3 text-sm font-semibold transition ${
              activeTab === value
                ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950"
                : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
            }`}
            onClick={() => setActiveTab(value as Tab)}
            type="button"
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === "alerts" ? (
        <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
          <AlertsTable alerts={alerts} selectedId={selectedAlert?.alertId} isLoading={isLoading} onSelect={setSelectedAlert} />
          <AlertDetail
            alert={selectedAlert}
            resolveReason={resolveReason}
            testRecipient={testRecipient}
            isSaving={isSaving}
            onResolveReason={setResolveReason}
            onTestRecipient={setTestRecipient}
            onAcknowledge={() => void acknowledgeSelected()}
            onResolve={() => void resolveSelected()}
            onSendTest={() => void sendTest()}
          />
        </section>
      ) : null}

      {activeTab === "rules" ? (
        <RulesView
          rules={rules}
          form={ruleForm}
          templates={templates}
          canConfigure={canConfigure}
          isSaving={isSaving}
          onSelect={selectRule}
          onForm={setRuleForm}
          onSave={saveRule}
        />
      ) : null}

      {activeTab === "notifications" ? (
        <NotificationsView notifications={notifications} selected={selectedNotification} onSelect={setSelectedNotification} />
      ) : null}

      {activeTab === "templates" ? (
        <TemplatesView
          templates={templates}
          form={templateForm}
          previewHtml={previewHtml}
          canConfigure={canConfigure}
          isSaving={isSaving}
          onSelect={selectTemplate}
          onForm={setTemplateForm}
          onSave={saveTemplate}
          onPreview={() => void previewTemplate()}
        />
      ) : null}

      {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{message}</div> : null}
      {error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
    </section>
  );
}

function AlertsTable({
  alerts,
  selectedId,
  isLoading,
  onSelect
}: {
  alerts: AlertRecord[];
  selectedId?: string;
  isLoading: boolean;
  onSelect: (alert: AlertRecord) => void;
}) {
  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Bandeja</h2>
        <span className="text-sm text-slate-500 dark:text-slate-400">{alerts.length}</span>
      </div>
      {isLoading ? (
        <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando alertas...</p>
      ) : (
        <div className="max-h-[620px] overflow-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3 font-medium">Alerta</th>
                <th className="px-4 py-3 font-medium">Severidad</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Origen</th>
                <th className="px-4 py-3 font-medium">Repet.</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {alerts.map((alert) => (
                <tr key={alert.alertId} className={selectedId === alert.alertId ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
                  <td className="px-4 py-3">
                    <button className="text-left font-semibold text-slate-900 dark:text-slate-100" onClick={() => onSelect(alert)} type="button">
                      {alert.title}
                    </button>
                    <p className="mt-1 max-w-md truncate text-xs text-slate-500 dark:text-slate-400">{alert.message}</p>
                  </td>
                  <td className="px-4 py-3"><SeverityBadge severity={alert.severity} /></td>
                  <td className="px-4 py-3"><StatusBadge status={alert.status} /></td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{alert.source}</td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{alert.repeatCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function AlertDetail({
  alert,
  resolveReason,
  testRecipient,
  isSaving,
  onResolveReason,
  onTestRecipient,
  onAcknowledge,
  onResolve,
  onSendTest
}: {
  alert: AlertRecord | null;
  resolveReason: string;
  testRecipient: string;
  isSaving: boolean;
  onResolveReason: (value: string) => void;
  onTestRecipient: (value: string) => void;
  onAcknowledge: () => void;
  onResolve: () => void;
  onSendTest: () => void;
}) {
  if (!alert) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Detalle</h2>
      </section>
    );
  }

  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">{alert.title}</h2>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{alert.alertId}</p>
        </div>
        <SeverityBadge severity={alert.severity} />
      </div>
      <p className="text-sm text-slate-700 dark:text-slate-200">{alert.message}</p>
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <Detail label="Regla" value={alert.ruleCode} />
        <Detail label="Causa" value={alert.causeKey} />
        <Detail label="Faena" value={alert.faenaCodigo ?? "-"} />
        <Detail label="Entidad" value={`${alert.entityType ?? "-"} / ${alert.entityId ?? "-"}`} />
        <Detail label="Creada" value={formatDate(alert.createdAtUtc)} />
        <Detail label="Actualizada" value={formatDate(alert.updatedAtUtc)} />
      </dl>
      <div className="grid gap-2 md:grid-cols-[1fr_auto_auto] md:items-end">
        <Field label="Motivo cierre" value={resolveReason} onChange={onResolveReason} />
        <button
          className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          disabled={isSaving || alert.status === "Resolved"}
          onClick={onAcknowledge}
          type="button"
        >
          <Eye className="h-4 w-4" aria-hidden="true" />
          Reconocer
        </button>
        <button
          className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-teal-700 px-3 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          disabled={isSaving || alert.status === "Resolved" || !resolveReason.trim()}
          onClick={onResolve}
          type="button"
        >
          <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
          Resolver
        </button>
      </div>
      <div className="grid gap-2 md:grid-cols-[1fr_auto] md:items-end">
        <Field label="Correo prueba" value={testRecipient} onChange={onTestRecipient} />
        <button
          className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          disabled={isSaving}
          onClick={onSendTest}
          type="button"
        >
          <Send className="h-4 w-4" aria-hidden="true" />
          Enviar test
        </button>
      </div>
    </section>
  );
}

function RulesView({
  rules,
  form,
  templates,
  canConfigure,
  isSaving,
  onSelect,
  onForm,
  onSave
}: {
  rules: AlertRule[];
  form: RuleForm;
  templates: PdfTemplate[];
  canConfigure: boolean;
  isSaving: boolean;
  onSelect: (rule: AlertRule) => void;
  onForm: (form: RuleForm) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <section className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Reglas</h2>
        </div>
        <div className="max-h-[620px] overflow-auto">
          {rules.map((rule) => (
            <button
              key={rule.code}
              className="block w-full border-b border-slate-100 px-4 py-3 text-left text-sm transition hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800"
              onClick={() => onSelect(rule)}
              type="button"
            >
              <div className="flex items-center justify-between gap-3">
                <span className="font-semibold text-slate-900 dark:text-slate-100">{rule.name}</span>
                <SeverityBadge severity={rule.severity} />
              </div>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{rule.eventType}</p>
            </button>
          ))}
        </div>
      </div>
      <form className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={onSave}>
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Configuracion</h2>
        <div className="grid gap-3 md:grid-cols-2">
          <Field disabled label="Codigo" value={form.code} onChange={(value) => onForm({ ...form, code: value })} />
          <Field label="Nombre" value={form.name} onChange={(value) => onForm({ ...form, name: value })} />
          <Field label="Evento" value={form.eventType} onChange={(value) => onForm({ ...form, eventType: value })} />
          <SelectField label="Severidad" value={form.severity} options={["Info", "Warning", "Critical"]} onChange={(value) => onForm({ ...form, severity: value as AlertSeverityLevel })} />
          <SelectField label="Plantilla" value={form.templateId} options={templates.map((template) => template.templateId)} onChange={(value) => onForm({ ...form, templateId: value })} />
          <FaenaSelect
            emptyLabel="Todas las faenas"
            value={form.faenaCodigo ?? ""}
            onChange={(value) => onForm({ ...form, faenaCodigo: value })}
          />
          <div className="md:col-span-2">
            <Field label="Destinatarios" value={form.recipients} onChange={(value) => onForm({ ...form, recipients: value })} />
          </div>
        </div>
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
          <CheckField label="Activa" checked={form.enabled} onChange={(value) => onForm({ ...form, enabled: value })} />
          <CheckField label="Repetir" checked={form.repeatUntilResolved} onChange={(value) => onForm({ ...form, repeatUntilResolved: value })} />
          <CheckField label="Correo" checked={form.generateEmail} onChange={(value) => onForm({ ...form, generateEmail: value })} />
          <CheckField label="PDF" checked={form.generatePdf} onChange={(value) => onForm({ ...form, generatePdf: value })} />
        </div>
        <Field label="Motivo" value={form.reason} onChange={(value) => onForm({ ...form, reason: value })} />
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          disabled={!canConfigure || isSaving}
          type="submit"
        >
          <Save className="h-4 w-4" aria-hidden="true" />
          Guardar regla
        </button>
      </form>
    </section>
  );
}

function NotificationsView({
  notifications,
  selected,
  onSelect
}: {
  notifications: NotificationRecord[];
  selected: NotificationRecord | null;
  onSelect: (notification: NotificationRecord) => void;
}) {
  return (
    <section className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Historial</h2>
        </div>
        <div className="max-h-[620px] overflow-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3 font-medium">Asunto</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Proveedor</th>
                <th className="px-4 py-3 font-medium">PDF</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {notifications.map((notification) => (
                <tr key={notification.notificationId} className={selected?.notificationId === notification.notificationId ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
                  <td className="px-4 py-3">
                    <button className="text-left font-semibold text-slate-900 dark:text-slate-100" onClick={() => onSelect(notification)} type="button">
                      {notification.subject}
                    </button>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{notification.recipients}</p>
                  </td>
                  <td className="px-4 py-3"><NotificationBadge status={notification.status} /></td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{notification.provider ?? "-"}</td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{notification.pdfFileKey ? "Si" : "No"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Correo</h2>
        {selected ? (
          <>
            <dl className="grid gap-3 text-sm sm:grid-cols-2">
              <Detail label="Estado" value={selected.status} />
              <Detail label="Envio" value={formatDate(selected.sentAtUtc)} />
              <Detail label="PDF" value={selected.pdfFileKey ?? "-"} />
              <Detail label="Ruta" value={selected.pdfPath ?? "-"} />
            </dl>
            <div className="rounded-md bg-slate-50 p-3 text-sm text-slate-700 dark:bg-slate-950 dark:text-slate-200" dangerouslySetInnerHTML={{ __html: selected.body }} />
            {selected.error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{selected.error}</div> : null}
          </>
        ) : null}
      </section>
    </section>
  );
}

function TemplatesView({
  templates,
  form,
  previewHtml,
  canConfigure,
  isSaving,
  onSelect,
  onForm,
  onSave,
  onPreview
}: {
  templates: PdfTemplate[];
  form: TemplateForm;
  previewHtml: string;
  canConfigure: boolean;
  isSaving: boolean;
  onSelect: (template: PdfTemplate) => void;
  onForm: (form: TemplateForm) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onPreview: () => void;
}) {
  return (
    <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Plantillas</h2>
        </div>
        <div className="max-h-[620px] overflow-auto">
          {templates.map((template) => (
            <button
              key={template.templateId}
              className="block w-full border-b border-slate-100 px-4 py-3 text-left text-sm transition hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800"
              onClick={() => onSelect(template)}
              type="button"
            >
              <span className="font-semibold text-slate-900 dark:text-slate-100">{template.name}</span>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{template.templateId}</p>
            </button>
          ))}
        </div>
      </div>
      <div className="grid gap-4 xl:grid-cols-2">
        <form className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={onSave}>
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Editor HTML</h2>
          <Field disabled label="ID" value={form.templateId} onChange={(value) => onForm({ ...form, templateId: value })} />
          <Field label="Nombre" value={form.name} onChange={(value) => onForm({ ...form, name: value })} />
          <Field label="Evento" value={form.eventType} onChange={(value) => onForm({ ...form, eventType: value })} />
          <Field label="Asunto" value={form.subjectTemplate} onChange={(value) => onForm({ ...form, subjectTemplate: value })} />
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            HTML
            <textarea
              className="mt-2 min-h-64 w-full rounded-md border border-slate-300 bg-white px-3 py-2 font-mono text-xs outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
              value={form.htmlTemplate}
              onChange={(event) => onForm({ ...form, htmlTemplate: event.target.value })}
            />
          </label>
          <CheckField label="Activa" checked={form.active} onChange={(value) => onForm({ ...form, active: value })} />
          <Field label="Motivo" value={form.reason} onChange={(value) => onForm({ ...form, reason: value })} />
          <div className="flex flex-wrap gap-2">
            <button
              className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              onClick={onPreview}
              type="button"
            >
              <Eye className="h-4 w-4" aria-hidden="true" />
              Preview
            </button>
            <button
              className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
              disabled={!canConfigure || isSaving}
              type="submit"
            >
              <Save className="h-4 w-4" aria-hidden="true" />
              Guardar
            </button>
          </div>
        </form>
        <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Vista previa</h2>
          <div className="mt-4 min-h-96 overflow-auto rounded-md bg-slate-50 p-4 text-sm text-slate-800 dark:bg-slate-950 dark:text-slate-100" dangerouslySetInnerHTML={{ __html: previewHtml }} />
        </section>
      </div>
    </section>
  );
}

function Metric({ label, value, icon: Icon }: { label: string; value: number; icon: LucideIcon }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-slate-500 dark:text-slate-400">{label}</p>
        <Icon className="h-5 w-5 text-slate-400" aria-hidden="true" />
      </div>
      <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-white">{value}</p>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  disabled
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const id = `alert-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function SelectField({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function CheckField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex min-h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
      <input checked={checked} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      {label}
    </label>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  );
}

function SeverityBadge({ severity }: { severity: AlertSeverityLevel }) {
  const className =
    severity === "Critical"
      ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
      : severity === "Warning"
        ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
        : "bg-sky-50 text-sky-700 dark:bg-sky-950 dark:text-sky-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{severity}</span>;
}

function StatusBadge({ status }: { status: AlertStatus }) {
  const className =
    status === "Resolved"
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : status === "Acknowledged"
        ? "bg-sky-50 text-sky-700 dark:bg-sky-950 dark:text-sky-200"
        : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{status}</span>;
}

function NotificationBadge({ status }: { status: NotificationStatus }) {
  const className =
    status === "Sent"
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : status === "Failed"
        ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
        : "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{status}</span>;
}

function toRuleForm(rule: AlertRule): RuleForm {
  return {
    ...rule,
    faenaCodigo: rule.faenaCodigo ?? "",
    reason: ""
  };
}

function toRulePayload(form: RuleForm) {
  return {
    name: form.name,
    eventType: form.eventType,
    enabled: form.enabled,
    severity: form.severity,
    repeatUntilResolved: form.repeatUntilResolved,
    generateEmail: form.generateEmail,
    generatePdf: form.generatePdf,
    templateId: form.templateId,
    recipients: form.recipients,
    faenaCodigo: emptyToNull(form.faenaCodigo ?? ""),
    reason: emptyToNull(form.reason)
  };
}

function toTemplateForm(template: PdfTemplate): TemplateForm {
  return {
    ...template,
    reason: ""
  };
}

function toTemplatePayload(form: TemplateForm) {
  return {
    name: form.name,
    eventType: form.eventType,
    subjectTemplate: form.subjectTemplate,
    htmlTemplate: form.htmlTemplate,
    active: form.active,
    reason: emptyToNull(form.reason)
  };
}

function formatDate(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("es-CL", {
    dateStyle: "short",
    timeStyle: "short"
  }).format(new Date(value));
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}
