// DEMO DATA ONLY — no real faculty or person names.

export type Role =
  | "university_leader"
  | "faculty_leader"
  | "university_coordinator"
  | "faculty_coordinator";

export const ROLE_LABELS: Record<Role, string> = {
  university_leader: "Universitet rahbari",
  faculty_leader: "Fakultet rahbari",
  university_coordinator: "Universitet koordinatori",
  faculty_coordinator: "Fakultet koordinatori",
};

// The 7 coordinator directions; shared by every faculty and the university coordinator group.
export const DIRECTIONS: string[] = [
  "“Yoshlar daftari” va metsenatlik dasturlari",
  "Harbiy-vatanparvarlik loyihalari",
  "Talaba-qizlar",
  "“Besh tashabbus” loyihalari",
  "Intellektual loyihalar",
  "Tadbirkorlik va innovatsion loyihalar",
  "“Social Media”",
];

export interface Person {
  id: string;
  name: string;
  role: Role;
  facultyId: number | null; // null for university-level roles
  directionIdx: number | null; // coordinator direction 0..6
}

export interface Faculty {
  id: number;
  name: string;
}

export type ReportStatus = "pending" | "approved" | "rejected";

export interface Report {
  id: string;
  title: string;
  authorId: string;
  facultyId: number | null;
  directionIdx: number | null;
  score: number;
  status: ReportStatus;
  comment: string;
  createdAt: string;
}

export interface Announcement {
  id: string;
  title: string;
  body: string;
  createdAt: string;
}

export const FACULTIES: Faculty[] = Array.from({ length: 13 }, (_: unknown, i: number) => ({
  id: i + 1,
  name: `${i + 1}-fakultet (demo)`,
}));

export function seedPeople(): Person[] {
  const people: Person[] = [
    { id: "ul", name: "Demo universitet rahbari", role: "university_leader", facultyId: null, directionIdx: null },
  ];
  DIRECTIONS.forEach((_: string, d: number) =>
    people.push({ id: `uc-${d}`, name: `Demo univ. koordinator ${d + 1}`, role: "university_coordinator", facultyId: null, directionIdx: d }),
  );
  FACULTIES.forEach((f: Faculty) => {
    people.push({ id: `fl-${f.id}`, name: `Demo fakultet rahbari ${f.id}`, role: "faculty_leader", facultyId: f.id, directionIdx: null });
    DIRECTIONS.forEach((_: string, d: number) =>
      people.push({ id: `fc-${f.id}-${d}`, name: `Demo koordinator ${f.id}.${d + 1}`, role: "faculty_coordinator", facultyId: f.id, directionIdx: d }),
    );
  });
  return people;
}

// Deterministic seed so the ranking is not empty on first load.
export function seedReports(): Report[] {
  const reports: Report[] = [];
  FACULTIES.forEach((f: Faculty) => {
    const count: number = ((f.id * 7) % 6) + 1;
    for (let k: number = 0; k < count; k++) {
      const d: number = (f.id + k) % 7;
      reports.push({
        id: `seed-${f.id}-${k}`,
        title: `Demo ish ${k + 1}`,
        authorId: `fc-${f.id}-${d}`,
        facultyId: f.id,
        directionIdx: d,
        score: ((f.id * 13 + k * 5) % 9) + 2,
        status: k === count - 1 && f.id % 3 === 0 ? "pending" : "approved",
        comment: "",
        createdAt: "2026-11-01",
      });
    }
  });
  return reports;
}

export interface RankRow {
  faculty: Faculty;
  position: number;
  approved: number;
  score: number;
}

/** Only approved reports count toward ranking. */
export function computeRanking(reports: Report[]): RankRow[] {
  return FACULTIES.map((faculty: Faculty) => {
    const approved: Report[] = reports.filter((r: Report) => r.facultyId === faculty.id && r.status === "approved");
    return { faculty, position: 0, approved: approved.length, score: approved.reduce((s: number, r: Report) => s + r.score, 0) };
  })
    .sort((a: RankRow, b: RankRow) => b.score - a.score || b.approved - a.approved || a.faculty.id - b.faculty.id)
    .map((row: RankRow, i: number) => ({ ...row, position: i + 1 }));
}

/** Who approves a report from this author. Assumed hierarchy — confirm with the checkpoint. */
export function approverRoleFor(author: Person): Role | null {
  if (author.role === "faculty_coordinator") return "faculty_leader";
  if (author.role === "faculty_leader" || author.role === "university_coordinator") return "university_leader";
  return null; // university leader's work is not approved by anyone
}

export function canApprove(approver: Person, author: Person): boolean {
  if (approverRoleFor(author) !== approver.role) return false;
  if (approver.role === "faculty_leader") return approver.facultyId === author.facultyId;
  return true;
}
