"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useCallback, useEffect, useState, type FormEvent } from "react";
import { describeError, homeFor, useAuth, type CurrentUser } from "@/lib/auth";
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
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-indigo-50 to-slate-100 px-4 py-10">
      <div className="w-full max-w-md space-y-5">
        <div className="text-center">
          <Link href="/" className="text-xl font-bold text-indigo-700">
            Universitet Yetakchilari
          </Link>
          <p className="text-sm text-slate-500">Tizimga kirish</p>
        </div>

        {context && (
          <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-3 text-sm text-indigo-900">
            Profil: <b>{context.name}</b>. O&apos;z login va parolingiz bilan kiring.
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" noValidate>
          {error && (
            <p role="alert" className="rounded-md bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {error}
            </p>
          )}
          <label className="block text-sm">
            Login
            <input
              name="username"
              autoComplete="username"
              required
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            />
          </label>
          <label className="block text-sm">
            Parol
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            />
          </label>
          <button
            disabled={busy || state.status === "loading" || userName.trim() === "" || password === ""}
            className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60"
          >
            {busy ? "Kirilmoqda..." : "Kirish"}
          </button>
          <p className="text-center text-xs text-slate-500">Login va vaqtinchalik parolni universitet rahbari yoki fakultet rahbari beradi.</p>
        </form>
      </div>
    </main>
  );
}
