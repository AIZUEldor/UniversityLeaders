"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { AnimatedBackground } from "@/components/AnimatedBackground";
import { ChangePasswordForm } from "@/components/ChangePasswordForm";
import { homeFor, useAuth, type CurrentUser } from "@/lib/auth";
import { ROLE_LABELS, type Role } from "@/lib/data";
import { inboxAnnouncementsApi, notificationsApi } from "@/lib/messagingApi";
import { useMessagingHints } from "@/lib/realtime";

export interface NavItem {
  href: string;
  label: string;
  roles: Role[];
  /** Shows the signed-in user's unread count next to the link. */
  badge?: UnreadKind;
}

type UnreadKind = "notifications" | "announcements";
type UnreadCounts = Record<UnreadKind, number>;

/** Unread counts for the nav; refreshed on every realtime (re)connect, live hint and local read change. */
function useUnreadCounts(user: CurrentUser | null): UnreadCounts {
  const enabled: boolean = user !== null && !user.mustChangePassword;
  const isFacultyLeader: boolean = user?.role === "faculty_leader";
  const [counts, setCounts] = useState<UnreadCounts>({ notifications: 0, announcements: 0 });
  const refresh = useCallback(async (): Promise<void> => {
    try {
      const [notifications, announcements] = await Promise.all([
        notificationsApi.unreadCount(),
        isFacultyLeader ? inboxAnnouncementsApi.unreadCount() : Promise.resolve(0),
      ]);
      setCounts({ notifications, announcements });
    } catch {
      // Badges are a hint only; the pages themselves show load errors.
    }
  }, [isFacultyLeader]);
  useEffect(() => {
    if (!enabled) return;
    const timer = window.setTimeout(() => { void refresh(); }, 0);
    return () => window.clearTimeout(timer);
  }, [enabled, refresh]);
  useMessagingHints(enabled, () => { void refresh(); });
  return enabled ? counts : { notifications: 0, announcements: 0 };
}

/**
 * Layout for /admin and /dashboard. Identity comes from GET /api/auth/me via useAuth(); these checks only decide what to
 * render and where to navigate. Every API call is authorized by the server regardless of what this component shows.
 */
export function PrivateShell({ title, items, children }: { title: string; items: NavItem[]; children: React.ReactNode }) {
  const pathname: string = usePathname();
  const router = useRouter();
  const { state, logout } = useAuth();
  const [open, setOpen] = useState<boolean>(false);

  const user: CurrentUser | null = state.status === "authenticated" ? state.user : null;
  const unread: UnreadCounts = useUnreadCounts(user);
  const visible: NavItem[] = user ? items.filter((i: NavItem) => i.roles.includes(user.role)) : [];
  const page: NavItem | undefined = items.find((i: NavItem) => i.href === pathname);
  const allowed: boolean = user !== null && (
    (page !== undefined && page.roles.includes(user.role))
    || (pathname === "/admin/users" && user.role === "university_leader")
    || (pathname.startsWith("/dashboard/reports/") && user.role !== "university_leader")
    || (pathname.startsWith("/admin/approvals/") && user.role === "university_leader")
  );
  const wrongArea: boolean = user !== null && visible.length === 0;

  // A signed-in user in the other area (e.g. a coordinator opening /admin) is sent to their own area.
  useEffect(() => {
    if (wrongArea && user) router.replace(homeFor(user.role));
  }, [wrongArea, user, router]);

  const signOut = async (): Promise<void> => {
    await logout();
    router.push("/login");
  };

  if (state.status === "loading" || wrongArea) {
    return <CenteredMessage title="Yuklanmoqda..." />;
  }

  if (state.status === "anonymous") {
    return (
      <CenteredMessage
        title={state.expired ? "Sessiya tugadi" : "Avval tizimga kiring"}
        text={state.expired ? "Xavfsizlik uchun qaytadan kiring." : "Bu bo'lim faqat tizimga kirgan foydalanuvchilar uchun."}
        href="/login"
        linkText="Kirish sahifasi →"
      />
    );
  }

  const signedIn: CurrentUser = state.user;
  return (
    <div className="min-h-screen md:flex">
      <AnimatedBackground />
      <aside className="relative border-b border-slate-800 bg-gradient-to-b from-slate-900 to-indigo-950 text-slate-200 md:sticky md:top-0 md:h-screen md:w-64 md:shrink-0 md:overflow-y-auto md:border-b-0">
        <div className="flag-ribbon" aria-hidden="true" />
        <div className="flex items-center gap-3 px-4 py-4">
          <Link href="/" className="btn shrink-0 rounded-lg bg-white px-1.5 py-1" title="Ommaviy sayt">
            <Image src="/ozmu-logo.png" alt="O'zMU Yoshlar ittifoqi logotipi" width={291} height={128} className="h-7 w-auto" />
          </Link>
          <div className="min-w-0 text-sm font-bold leading-tight text-white">{title}</div>
          <button
            className="ml-auto rounded-md border border-slate-600 px-3 py-1 text-sm md:hidden"
            onClick={() => setOpen(!open)}
            aria-expanded={open}
            aria-label="Menyu"
          >
            ☰
          </button>
        </div>
        <nav className={`${open ? "flex" : "hidden"} flex-col gap-1 px-3 pb-4 md:flex`}>
          {!signedIn.mustChangePassword &&
            visible.map((i: NavItem) => (
              <Link
                key={i.href}
                href={i.href}
                onClick={() => setOpen(false)}
                className={`rounded-md px-3 py-2 text-sm ${pathname === i.href ? "bg-indigo-600 text-white" : "hover:bg-slate-800"}`}
              >
                {i.label}
                {i.badge && unread[i.badge] > 0 && (
                  <span className="ml-2 rounded-full bg-amber-400 px-2 py-0.5 text-xs font-semibold text-slate-900" aria-label={`${unread[i.badge]} ta o'qilmagan`}>
                    {unread[i.badge]}
                  </span>
                )}
              </Link>
            ))}
          <div className="mt-4 border-t border-slate-700 pt-4 text-xs text-slate-400">
            <div className="font-medium text-slate-200">{signedIn.fullName}</div>
            <div>
              {ROLE_LABELS[signedIn.role]} · {signedIn.userName}
            </div>
            <div className="mt-3 flex gap-3">
              <Link href="/" className="hover:text-white">
                Ommaviy sayt
              </Link>
              <button onClick={() => void signOut()} className="hover:text-white">
                Chiqish
              </button>
            </div>
          </div>
        </nav>
      </aside>

      <div className="min-w-0 flex-1">
        <main className="mx-auto max-w-5xl px-4 py-6">
          {signedIn.mustChangePassword ? (
            <ChangePasswordForm forced />
          ) : allowed ? (
            children
          ) : (
            <CenteredMessage title="Bu bo'lim sizning rolingiz uchun emas." href={homeFor(signedIn.role)} linkText="Mening bo'limimga o'tish →" />
          )}
        </main>
      </div>
    </div>
  );
}

function CenteredMessage({ title, text, href, linkText }: { title: string; text?: string; href?: string; linkText?: string }) {
  return (
    <div className="flex min-h-[50vh] items-center justify-center px-4">
      <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 text-sm shadow-sm">
        <p className="font-semibold">{title}</p>
        {text && <p className="mt-1 text-slate-500">{text}</p>}
        {href && linkText && (
          <Link href={href} className="mt-3 inline-block text-indigo-600 hover:underline">
            {linkText}
          </Link>
        )}
      </div>
    </div>
  );
}
