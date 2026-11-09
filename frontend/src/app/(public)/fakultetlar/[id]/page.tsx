import { notFound } from "next/navigation";
import { Suspense } from "react";
import { FacultyProfile } from "./FacultyProfile";

async function FacultyProfileFromParams({ params }: { params: Promise<{ id: string }> }) {
  const { id: rawId } = await params;
  const id = Number(rawId);
  if (!Number.isInteger(id) || id < 1) notFound();
  return <FacultyProfile id={id} />;
}

export default function FacultyProfilePage({ params }: { params: Promise<{ id: string }> }) {
  return (
    <Suspense fallback={<p role="status" className="text-sm text-slate-500">Fakultet profili yuklanmoqda...</p>}>
      <FacultyProfileFromParams params={params} />
    </Suspense>
  );
}
