"use client";

import { NotificationsList } from "@/components/messaging/NotificationsList";
import { T } from "@/lib/i18n";

export default function Notifications() {
  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold"><T>Bildirishnomalar</T></h1><p className="text-sm text-slate-500"><T>{"Hisobot yuborilishi, tasdiqlanishi va rad etilishi haqida. Izohlar faqat hisobot sahifasida ko'rinadi."}</T></p></div>
    <NotificationsList />
  </div>;
}
