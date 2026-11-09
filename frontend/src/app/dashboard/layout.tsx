"use client";

import { Suspense } from "react";
import { PrivateShell, type NavItem } from "@/components/PrivateShell";
import { type Role } from "@/lib/data";

const ALL: Role[] = ["faculty_leader", "faculty_coordinator", "university_coordinator"];
const FL: Role[] = ["faculty_leader"];
const ITEMS: NavItem[] = [
  { href: "/dashboard", label: "Umumiy ko'rinish", roles: ALL },
  { href: "/dashboard/my-work", label: "Mening ishlarim", roles: ALL },
  { href: "/dashboard/reports", label: "Hisobot yuborish", roles: ALL },
  { href: "/dashboard/media", label: "Foto / hujjat yuklash", roles: ALL },
  { href: "/dashboard/approvals", label: "Koordinator hisobotlarini tasdiqlash", roles: FL },
  { href: "/dashboard/coordinators", label: "Mening koordinatorlarim", roles: FL },
  { href: "/dashboard/announcements", label: "E'lonlar", roles: FL, badge: "announcements" },
  { href: "/dashboard/notifications", label: "Bildirishnomalar", roles: ALL, badge: "notifications" },
];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={<p className="p-6 text-sm">Yuklanmoqda...</p>}>
      <PrivateShell title="Shaxsiy kabinet" items={ITEMS}>{children}</PrivateShell>
    </Suspense>
  );
}
