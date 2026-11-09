"use client";

import { PrivateShell, type NavItem } from "@/components/PrivateShell";
import { type Role } from "@/lib/data";

const UL: Role[] = ["university_leader"];
const ITEMS: NavItem[] = [
  { href: "/admin", label: "Umumiy ko'rinish", roles: UL },
  { href: "/admin/faculties", label: "Fakultetlar", roles: UL },
  { href: "/admin/users", label: "Foydalanuvchilar / koordinatorlar", roles: UL },
  { href: "/admin/approvals", label: "Hisobotlarni tasdiqlash", roles: UL },
  { href: "/admin/ranking", label: "Reyting nazorati", roles: UL },
  { href: "/admin/announcements", label: "E'lonlar", roles: UL },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <PrivateShell title="Admin — Universitet rahbari" items={ITEMS}>
      {children}
    </PrivateShell>
  );
}
