import { ReportDetailView } from "@/components/reports/ReportDetailView";

export default async function FacultyReviewDetail({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <ReportDetailView id={id} mode="reviewer" backHref="/dashboard/approvals" />;
}
