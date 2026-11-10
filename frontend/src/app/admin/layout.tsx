"use client";

import { Suspense } from "react";
import { PrivateShell, type NavItem } from "@/components/PrivateShell";
import { type Role } from "@/lib/data";
import { T } from "@/lib/i18n";

const UL: Role[] = ["university_leader"];
const ITEMS: NavItem[] = [
  { href: "/admin", label: "Umumiy ko'rinish", roles: UL },
  { href: "/admin/faculties", label: "Fakultetlar", roles: UL },
  { href: "/admin/faculty-leaders", label: "Fakultet yetakchilari", roles: UL },
  { href: "/admin/approvals", label: "Hisobotlarni tasdiqlash", roles: UL },
  { href: "/admin/ranking", label: "Reyting nazorati", roles: UL },
  { href: "/admin/announcements", label: "E'lonlar", roles: UL },
  { href: "/admin/notifications", label: "Bildirishnomalar", roles: UL, badge: "notifications" },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={<p className="p-6 text-sm"><T>Yuklanmoqda...</T></p>}>
      <PrivateShell title="Admin — Universitet rahbari" items={ITEMS}>{children}</PrivateShell>
    </Suspense>
  );
}
