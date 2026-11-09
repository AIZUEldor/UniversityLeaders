import type { FacultyRanking } from "./publicApi";

export type RankingMetric = "totalScore" | "approvedReports";

export interface RankedRow extends FacultyRanking {
  /** Position in the selected ordering. `position` stays the API's official, score-based rank. */
  shownPosition: number;
}

/**
 * Official ranking = the API order (total score, then approved count, then faculty id). The approved-count view is a
 * secondary ordering (approved count, then score, then faculty id) with its own positions; it never replaces `position`.
 */
export function orderRanking(rows: FacultyRanking[], metric: RankingMetric): RankedRow[] {
  const ordered: FacultyRanking[] = [...rows].sort((a: FacultyRanking, b: FacultyRanking) => metric === "totalScore"
    ? a.position - b.position
    : b.approvedReports - a.approvedReports || b.totalScore - a.totalScore || a.facultyId - b.facultyId);
  return ordered.map((row: FacultyRanking, index: number) => ({ ...row, shownPosition: index + 1 }));
}
