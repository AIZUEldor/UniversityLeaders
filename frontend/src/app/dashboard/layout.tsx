"use client";

import { Suspense } from "react";
import { PrivateShell, type NavItem } from "@/components/PrivateShell";
import { type Role } from "@/lib/data";

// Only the 13 faculty leaders sign in to this area; coordinators are profiles without a login.
const FL: Role[] = ["faculty_leader"];
const ITEMS: NavItem[] = [
  { href: "/dashboard", label: "Umumiy ko'rinish", roles: FL },
  { href: "/dashboard/coordinators", label: "Mening koordinatorlarim", roles: FL },
  { href: "/dashboard/reports", label: "Hisobot yuborish", roles: FL },
  { href: "/dashboard/my-work", label: "Mening hisobotlarim", roles: FL },
  { href: "/dashboard/media", label: "Foto / hujjat yuklash", roles: FL },
  { href: "/dashboard/ranking", label: "Fakultetlar reytingi", roles: FL },
  { href: "/dashboard/announcements", label: "Admin xabarlari", roles: FL, badge: "announcements" },
  { href: "/dashboard/notifications", label: "Bildirishnomalar", roles: FL, badge: "notifications" },
];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={<p className="p-6 text-sm">Yuklanmoqda...</p>}>
      <PrivateShell title="Shaxsiy kabinet" items={ITEMS}>{children}</PrivateShell>
    </Suspense>
  );
}
