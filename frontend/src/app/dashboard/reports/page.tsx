"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ManagementMessage, ManagementPanel } from "@/components/admin/ManagementUi";
import { ReportForm } from "@/components/reports/ReportForm";
import { useT } from "@/lib/i18n";
import { reportsApi, type ReportContent } from "@/lib/reportsApi";

export default function DashboardReports() {
  const router = useRouter();
  const t = useT();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const create = async (content: ReportContent): Promise<void> => {
    setBusy(true); setError(null);
    try { const report = await reportsApi.create(content); router.push(`/dashboard/reports/${report.summary.id}`); }
    catch (failure: unknown) { setError(failure); setBusy(false); }
  };
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold">{t("Yangi hisobot")}</h1><p className="text-sm text-slate-500">{t("Avval qoralama yarating, keyin fayllarni biriktirib ko'rib chiqishga yuboring.")}</p></div>{error !== null && <ManagementMessage error={error} />}<ManagementPanel><ReportForm busy={busy} onSave={create} /></ManagementPanel></div>;
}