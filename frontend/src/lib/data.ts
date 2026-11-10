// Shared UI constants: roles and the 7 coordinator directions (same order and ids 1-7 as the backend seed).
export type Role =
  | "university_leader"
  | "faculty_leader"
  | "university_coordinator"
  | "faculty_coordinator";

export const ROLE_LABELS: Record<Role, string> = {
  university_leader: "Universitet yetakchisi",
  faculty_leader: "Fakultet yetakchisi",
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
