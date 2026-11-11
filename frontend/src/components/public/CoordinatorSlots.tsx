"use client";

import { PersonPhoto } from "@/components/PersonPhoto";
import { useT } from "@/lib/i18n";
import type { PublicDirectionSlot } from "@/lib/publicApi";

/** The 7 direction slots of a leader (faculty or university); a vacant slot shows "Koordinator tayinlanmagan". */
export function CoordinatorSlots({ slots }: { slots: PublicDirectionSlot[] }) {
  const t = useT();
  return <section><h2 className="mb-3 text-xl font-bold text-slate-900">{t("Yo'nalishlar va koordinatorlar")}</h2>
    <ol className="grid gap-3 sm:grid-cols-2">{slots.map((slot) => <li key={slot.directionId} style={{ animationDelay: `${slot.directionId * 50}ms` }} className="fade-up flex items-center gap-4 rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur transition hover:-translate-y-0.5 hover:shadow-md">
      <PersonPhoto photoId={slot.photoId} name={slot.coordinatorName} size="md" />
      <div className="min-w-0">
        <div className="text-xs font-semibold text-indigo-700">{slot.directionId}. {t(slot.directionName)}</div>
        <div className={`mt-1 ${slot.coordinatorName ? "font-medium text-slate-800" : "italic text-slate-400"}`}>{slot.coordinatorName ?? t("Koordinator tayinlanmagan")}</div>
        {slot.phone && <a href={`tel:${slot.phone.replace(/[^+\d]/g, "")}`} className="text-sm text-indigo-700 hover:underline">{slot.phone}</a>}
      </div>
    </li>)}</ol>
  </section>;
}
