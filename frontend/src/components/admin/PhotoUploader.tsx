"use client";

import { useRef, useState, type ChangeEvent } from "react";
import { PersonPhoto } from "@/components/PersonPhoto";
import { describeError } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { PhotoError, preparePhoto } from "@/lib/photo";

/** Photo of one profile with upload/remove controls. The parent reloads its list in onChanged. */
export function PhotoUploader({ photoId, name, disabled, upload, remove, onChanged }: {
  photoId: string | null;
  name: string;
  disabled?: boolean;
  upload: (photo: Blob) => Promise<unknown>;
  remove: () => Promise<unknown>;
  onChanged: () => void;
}) {
  const t = useT();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (action: () => Promise<unknown>): Promise<void> => {
    setBusy(true); setError(null);
    try { await action(); onChanged(); }
    catch (failure: unknown) { setError(failure instanceof PhotoError ? failure.message : describeError(failure)); }
    finally { setBusy(false); }
  };

  const chosen = (event: ChangeEvent<HTMLInputElement>): void => {
    const file = event.target.files?.[0];
    event.target.value = ""; // allow choosing the same file again
    if (file) void run(async () => upload(await preparePhoto(file)));
  };

  return <div className="flex items-center gap-3">
    <PersonPhoto photoId={photoId} name={name} size="sm" />
    <div className="flex flex-col gap-1 text-xs">
      <input ref={input} type="file" accept="image/jpeg,image/png" className="hidden" onChange={chosen} aria-label={t("{name}: rasm tanlash", { name })} />
      <button type="button" disabled={disabled || busy} onClick={() => input.current?.click()} className="text-left text-indigo-700 hover:underline disabled:opacity-50">
        {busy ? t("Yuklanmoqda...") : photoId ? t("Rasmni almashtirish") : t("Rasm yuklash")}
      </button>
      {photoId && <button type="button" disabled={disabled || busy} onClick={() => void run(remove)} className="text-left text-slate-500 hover:underline disabled:opacity-50">{t("Rasmni olib tashlash")}</button>}
      {error && <span role="alert" className="text-red-700">{t(error)}</span>}
    </div>
  </div>;
}
