import { ReportDetailView } from "@/components/reports/ReportDetailView";

export default async function MyReportDetail({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <ReportDetailView id={id} mode="author" backHref="/dashboard/my-work" />;
}
