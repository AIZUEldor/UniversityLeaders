"use client";

import { useT } from "@/lib/i18n";

// State flag of the Republic of Uzbekistan drawn as SVG (no external asset): proportions 1:2, stripes azure, white and
// green separated by thin red lines (80:5:80:5:80), a white crescent and twelve white stars (three rows of four).
// The flag itself is never distorted; the "wave" is a light/shade overlay plus a gentle 3D sway (globals.css).

const STAR_ROWS: number[] = [20, 40, 60];

function starPoints(cx: number, cy: number, r: number): string {
  const points: string[] = [];
  for (let i = 0; i < 10; i++) {
    const radius = i % 2 === 0 ? r : r * 0.4;
    const angle = -Math.PI / 2 + (i * Math.PI) / 5;
    points.push(`${(cx + radius * Math.cos(angle)).toFixed(2)},${(cy + radius * Math.sin(angle)).toFixed(2)}`);
  }
  return points.join(" ");
}

function FlagSvg({ label }: { label: string }) {
  return <svg viewBox="0 0 500 250" className="block h-full w-full" role="img" aria-label={label}>
    <rect width="500" height="250" fill="#1eb53a" />
    <rect width="500" height="125" fill="#0099b5" />
    <rect y="80" width="500" height="90" fill="#ce1126" />
    <rect y="85" width="500" height="80" fill="#ffffff" />
    <circle cx="70" cy="40" r="30" fill="#ffffff" />
    <circle cx="80" cy="40" r="30" fill="#0099b5" />
    {STAR_ROWS.map((y) => Array.from({ length: 4 }, (_, i) =>
      <polygon key={`${y}-${i}`} points={starPoints(130 + i * 20, y, 7)} fill="#ffffff" />))}
  </svg>;
}

/** Waving flag for hero sections. Respects prefers-reduced-motion (the animation stops, the flag stays). */
export function UzFlag({ className = "" }: { className?: string }) {
  return <div className={`flag-wave relative aspect-[2/1] overflow-hidden rounded-md ${className}`}>
    <FlagSvg label={useT()("O'zbekiston Respublikasi Davlat bayrog'i")} />
    <div className="flag-wave__shade pointer-events-none absolute inset-0" aria-hidden="true" />
  </div>;
}
