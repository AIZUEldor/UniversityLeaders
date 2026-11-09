"use client";

import { NotificationsList } from "@/components/messaging/NotificationsList";

export default function Notifications() {
  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">Bildirishnomalar</h1><p className="text-sm text-slate-500">Hisobot yuborilishi, tasdiqlanishi va rad etilishi haqida. Izohlar faqat hisobot sahifasida ko&apos;rinadi.</p></div>
    <NotificationsList />
  </div>;
}
