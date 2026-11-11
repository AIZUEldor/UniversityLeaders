"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ManagementMessage, ManagementPanel, inputClass, primaryClass } from "@/components/admin/ManagementUi";
import { PhotoUploader } from "@/components/admin/PhotoUploader";
import { useAuth } from "@/lib/auth";
import { facultiesApi, facultyLeadersApi, ownProfileApi } from "@/lib/adminApi";
import { useT } from "@/lib/i18n";
import { publicApi } from "@/lib/publicApi";
import { reportsApi } from "@/lib/reportsApi";

export default function AdminHome() {
  const t = useT();
  const { state } = useAuth();
  const [counts, setCounts] = useState({ faculties: 0, facultyLeaders: 0, pending: 0 });
  const [photoId, setPhotoId] = useState<string | null>(null);
  const [contact, setContact] = useState({ phone: "", phoneIsPublic: false });
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [faculties, leaders, queue, directory, me] = await Promise.all([facultiesApi.list(), facultyLeadersApi.list(false), reportsApi.reviewQueue(), publicApi.directory(), ownProfileApi.get()]);
      setPhotoId(directory.universityLeaderPhotoId);
      setContact({ phone: me.phone ?? "", phoneIsPublic: me.phoneIsPublic });
      setCounts({ faculties: faculties.length, facultyLeaders: leaders.length, pending: queue.length });
      setError(null);
    } catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const saveContact = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const phone = contact.phone.trim();
    setSaving(true); setSaved(false); setError(null);
    ownProfileApi.setContact({ phone, phoneIsPublic: contact.phoneIsPublic && phone !== "" })
      .then((me) => { setContact({ phone: me.phone ?? "", phoneIsPublic: me.phoneIsPublic }); setSaved(true); })
      .catch((failure: unknown) => setError(failure))
      .finally(() => setSaving(false));
  };

  const stats = [
    { label: "Fakultetlar", value: counts.faculties },
    { label: "Faol fakultet yetakchilari", value: counts.facultyLeaders },
    { label: "Tasdiqlash navbati", value: counts.pending },
  ];
  return <div className="space-y-6"><h1 className="text-2xl font-bold">{t("Umumiy ko'rinish")}</h1>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {state.status === "authenticated" && <ManagementPanel>
      <h2 className="font-semibold">{t("Mening ommaviy profilim")}</h2>
      <p className="mb-3 text-sm text-slate-500">{t("Rasmingiz va ruxsat bergan telefon raqamingiz «Universitet yetakchisi» profilida ko'rinadi.")}</p>
      <PhotoUploader photoId={photoId} name={state.user.fullName} disabled={loading} upload={(photo) => ownProfileApi.setPhoto("", photo)} remove={() => ownProfileApi.removePhoto("")} onChanged={() => void load()} />
      <form onSubmit={saveContact} className="mt-4 grid gap-3 md:grid-cols-2">
        <label className="text-sm">{t("Telefon raqami (ixtiyoriy)")}<input type="tel" inputMode="tel" maxLength={20} placeholder="+998 90 123-45-67" value={contact.phone} onChange={(event) => { setContact({ ...contact, phone: event.target.value }); setSaved(false); }} className={`${inputClass} mt-1`} /></label>
        <label className="flex items-center gap-2 text-sm md:col-span-2"><input type="checkbox" checked={contact.phoneIsPublic} disabled={contact.phone.trim() === ""} onChange={(event) => { setContact({ ...contact, phoneIsPublic: event.target.checked }); setSaved(false); }} />{t("Telefon raqamim ommaviy saytda ko'rsatilsin")}</label>
        <div className="flex items-center gap-3 md:col-span-2"><button disabled={saving || loading} className={primaryClass}>{t(saving ? "Saqlanmoqda..." : "Saqlash")}</button>{saved && <span role="status" className="text-sm text-emerald-700">{t("Saqlandi")}</span>}</div>
      </form>
    </ManagementPanel>}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Ma'lumotlar yuklanmoqda...")}</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{stats.map((stat) => <div key={stat.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><div className="text-2xl font-bold text-indigo-700">{stat.value}</div><div className="text-xs text-slate-500">{t(stat.label)}</div></div>)}</div>}
    <Link href="/admin/approvals" className="inline-block text-sm text-indigo-700 hover:underline">{t("Hisobotlarni ko'rib chiqish →")}</Link>
  </div>;
}
