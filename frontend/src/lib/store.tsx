"use client";

import { createContext, useContext, useState, type ReactNode } from "react";
import {
  type Announcement,
  type Person,
  type Report,
  type ReportStatus,
  seedPeople,
  seedReports,
} from "./data";

// Client-side demo state only; resets on page reload.
interface Store {
  people: Person[];
  reports: Report[];
  announcements: Announcement[];
  currentId: string;
  current: Person;
  setCurrentId: (id: string) => void;
  renamePerson: (id: string, name: string) => void;
  submitReport: (r: Omit<Report, "id" | "status" | "comment" | "createdAt">) => void;
  reviewReport: (id: string, status: ReportStatus, comment: string) => void;
  sendAnnouncement: (title: string, body: string) => void;
}

const StoreContext = createContext<Store | null>(null);

const today = (): string => new Date().toISOString().slice(0, 10);
const newId = (): string => Math.random().toString(36).slice(2, 10);

export function StoreProvider({ children }: { children: ReactNode }) {
  const [people, setPeople] = useState<Person[]>(seedPeople);
  const [reports, setReports] = useState<Report[]>(seedReports);
  const [announcements, setAnnouncements] = useState<Announcement[]>([]);
  const [currentId, setCurrentId] = useState<string>("fc-1-0");

  const current: Person = people.find((p: Person) => p.id === currentId) ?? people[0];

  const store: Store = {
    people,
    reports,
    announcements,
    currentId,
    current,
    setCurrentId,
    renamePerson: (id: string, name: string) =>
      setPeople((ps: Person[]) => ps.map((p: Person) => (p.id === id ? { ...p, name } : p))),
    submitReport: (r) =>
      setReports((rs: Report[]) => [{ ...r, id: newId(), status: "pending", comment: "", createdAt: today() }, ...rs]),
    reviewReport: (id: string, status: ReportStatus, comment: string) =>
      setReports((rs: Report[]) => rs.map((r: Report) => (r.id === id ? { ...r, status, comment } : r))),
    sendAnnouncement: (title: string, body: string) =>
      setAnnouncements((as: Announcement[]) => [{ id: newId(), title, body, createdAt: today() }, ...as]),
  };

  return <StoreContext.Provider value={store}>{children}</StoreContext.Provider>;
}

export function useStore(): Store {
  const s: Store | null = useContext(StoreContext);
  if (!s) throw new Error("useStore must be used inside StoreProvider");
  return s;
}
