"use client";

import Image from "next/image";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useCallback, useEffect, useState, type FormEvent } from "react";
import { AnimatedBackground } from "@/components/AnimatedBackground";
import { Preferences } from "@/components/Preferences";
import { UniversitySeal } from "@/components/UniversitySeal";
import { describeError, homeFor, useAuth, type CurrentUser } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { publicApi, type PublicFaculty } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <Login />
    </Suspense>
  );
}

function Login() {
  const router = useRouter();
  const { state, login } = useAuth();
  const t = useT();
  // From a public "Profilga kirish" link: a display hint only. It never selects an account or grants access.
  const facultyParam: string | null = useSearchParams().get("faculty");
  const directoryRequest = useCallback(() => (facultyParam === null ? Promise.resolve(null) : publicApi.directory()), [facultyParam]);
  const directory = usePublicRequest(directoryRequest);
  const context: PublicFaculty | undefined = directory.data?.faculties.find((f: PublicFaculty) => String(f.id) === facultyParam);

  const [userName, setUserName] = useState<string>("");
  const [password, setPassword] = useState<string>("");
  const [error, setError] = useState<string>("");
  const [busy, setBusy] = useState<boolean>(false);

  // Already signed in (e.g. opened /login in a new tab): go to the user's own area.
  useEffect(() => {
    if (state.status === "authenticated") router.replace(homeFor(state.user.role));
  }, [state, router]);

  const onSubmit = async (e: FormEvent<HTMLFormElement>): Promise<void> => {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      const user: CurrentUser = await login(userName.trim(), password);
      router.replace(homeFor(user.role)); // a pending password change is handled by the private layout
    } catch (err: unknown) {
      setError(describeError(err));
    } finally {
      setPassword("");
      setBusy(false);
    }
  };

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-10">
      <AnimatedBackground />
      <div className="fade-up w-full max-w-md space-y-5">
        <Preferences className="justify-end" />
        <div className="flex flex-col items-center text-center">
          <Link href="/" className="btn flex flex-col items-center gap-2 rounded-xl px-3 py-2 text-xl font-bold text-indigo-700">
            <Image src="/ozmu-logo.png" alt={t("O'zMU Yoshlar ittifoqi logotipi")} width={291} height={128} priority className="logo-img h-14 w-auto mix-blend-multiply" />
            {t("Universitet Yetakchilari")}
          </Link>
          <UniversitySeal className="mt-4 w-28" priority />
          <p className="mt-3 text-sm text-slate-600">{t("Tizimga kirish")}</p>
        </div>

        {context && (
          <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-3 text-sm text-indigo-900">
            {t("Profil:")} <b>{context.name}</b>. {t("O'z login va parolingiz bilan kiring.")}
          </div>
        )}

        <form onSubmit={onSubmit} className="relative space-y-3 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/90 p-6 pt-7 shadow-xl backdrop-blur" noValidate>
          <div className="flag-ribbon absolute inset-x-0 top-0" aria-hidden="true" />
          {error && (
            <p role="alert" className="rounded-md bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {t(error)}
            </p>
          )}
          <label className="block text-sm">
            {t("Login")}
            <input
              name="username"
              autoComplete="username"
              required
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 transition focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
            />
          </label>
          <label className="block text-sm">
            {t("Parol")}
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 transition focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
            />
          </label>
          <button
            disabled={busy || state.status === "loading" || userName.trim() === "" || password === ""}
            className="w-full rounded-lg bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2.5 text-sm font-semibold text-white shadow hover:from-indigo-700 hover:to-violet-700 disabled:opacity-50"
          >
            {busy ? t("Kirilmoqda...") : t("Kirish")}
          </button>
          <p className="text-center text-xs text-slate-500">{t("Tizimga faqat universitet yetakchisi va 13 fakultet yetakchisi kiradi. Login va vaqtinchalik parolni universitet yetakchisi beradi.")}</p>
        </form>
      </div>
    </main>
  );
}
