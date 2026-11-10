import Link from "next/link";
import { ReportsCollection } from "@/components/reports/ReportsCollection";
import { T } from "@/lib/i18n";

export default function MyWork() {
  return <div className="space-y-6"><div className="flex flex-wrap items-end justify-between gap-3"><h1 className="text-2xl font-bold"><T>Mening ishlarim</T></h1><Link href="/dashboard/reports" className="text-sm text-indigo-700 hover:underline"><T>Yangi hisobot →</T></Link></div><ReportsCollection source="mine" detailBase="/dashboard/reports" /></div>;
}