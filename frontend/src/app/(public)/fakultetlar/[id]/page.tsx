import { notFound } from "next/navigation";
import { Suspense } from "react";
import { T } from "@/lib/i18n";
import { FacultyProfile } from "./FacultyProfile";

async function FacultyProfileFromParams({ params }: { params: Promise<{ id: string }> }) {
  const { id: rawId } = await params;
  const id = Number(rawId);
  if (!Number.isInteger(id) || id < 1) notFound();
  return <FacultyProfile id={id} />;
}

export default function FacultyProfilePage({ params }: { params: Promise<{ id: string }> }) {
  return (
    <Suspense fallback={<p role="status" className="text-sm text-slate-500"><T>Fakultet profili yuklanmoqda...</T></p>}>
      <FacultyProfileFromParams params={params} />
    </Suspense>
  );
}
