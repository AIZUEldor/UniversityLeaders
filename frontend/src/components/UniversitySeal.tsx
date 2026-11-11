"use client";

import Image from "next/image";
import { useT } from "@/lib/i18n";

/**
 * National University seal (public/nuu-seal-2026.png, renamed on each artwork change so caches refresh: round, transparent outside the circle, so it sits on any background).
 * Animated in globals.css (.seal): soft glow behind, gentle float and tilt, and a light sweep across the face.
 * Respects prefers-reduced-motion (the animation stops, the seal stays).
 */
export function UniversitySeal({ className = "", priority = false }: { className?: string; priority?: boolean }) {
  const t = useT();
  return <div className={`seal relative aspect-square ${className}`}>
    <div className="seal__glow" aria-hidden="true" />
    <div className="seal__disc">
      <Image src="/nuu-seal-2026.png" alt={t("Mirzo Ulug'bek nomidagi O'zbekiston Milliy universiteti logotipi")} width={512} height={512}
        priority={priority} sizes="(min-width: 768px) 24rem, 18rem" className="h-full w-full" />
      <div className="seal__shine" aria-hidden="true" />
    </div>
  </div>;
}
