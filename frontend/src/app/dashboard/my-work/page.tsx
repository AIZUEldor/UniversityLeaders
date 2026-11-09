import Link from "next/link";
import { ReportsCollection } from "@/components/reports/ReportsCollection";

export default function MyWork() {
  return <div className="space-y-6"><div className="flex flex-wrap items-end justify-between gap-3"><h1 className="text-2xl font-bold">Mening ishlarim</h1><Link href="/dashboard/reports" className="text-sm text-indigo-700 hover:underline">Yangi hisobot →</Link></div><ReportsCollection source="mine" detailBase="/dashboard/reports" /></div>;
}