"use client";

import { apiBaseUrl } from "@/lib/api";
import { useT } from "@/lib/i18n";

const SIZES = { sm: "h-12 w-12", md: "h-20 w-20", lg: "h-28 w-28", card: "h-16 w-16 sm:h-28 sm:w-28" } as const;

/** Public profile photo, or the default avatar when none is set (or the profile is vacant). */
export function PersonPhoto({ photoId, name, size = "md" }: { photoId: string | null; name: string | null; size?: keyof typeof SIZES }) {
  const t = useT();
  const box = `${SIZES[size]} shrink-0 overflow-hidden rounded-full border-2 border-white bg-indigo-50 shadow ring-1 ring-slate-200`;
  if (!photoId) {
    return <div className={`${box} flex items-center justify-center text-indigo-300`} role="img" aria-label={name ? t("{name}: rasm yo'q", { name }) : t("Standart avatar")}>
      <svg viewBox="0 0 24 24" className="h-3/5 w-3/5" fill="currentColor" aria-hidden="true">
        <path d="M12 12a5 5 0 1 0 0-10 5 5 0 0 0 0 10Zm0 2c-4.42 0-8 2.24-8 5v1a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-1c0-2.76-3.58-5-8-5Z" />
      </svg>
    </div>;
  }

  // Plain <img>: photos are already downsized and immutable (cached for a year), so Next's optimizer adds nothing.
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={`${apiBaseUrl()}/api/public/photos/${photoId}`} alt={name ?? t("Profil rasmi")} loading="lazy" className={`${box} object-cover`} />;
}
