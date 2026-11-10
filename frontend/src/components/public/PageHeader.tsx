/** Title block for public pages: heading, short explanation and a flag-colour accent. */
export function PageHeader({ title, subtitle }: { title: string; subtitle: string }) {
  return <div className="fade-up">
    <h1 className="text-3xl font-extrabold tracking-tight text-slate-900">{title}</h1>
    <div className="mt-2 flex h-1 w-24 overflow-hidden rounded-full" aria-hidden="true">
      <span className="flex-1 bg-indigo-500" /><span className="w-1 bg-[var(--color-flag-red)]" /><span className="flex-1 bg-white ring-1 ring-slate-200" /><span className="w-1 bg-[var(--color-flag-red)]" /><span className="flex-1 bg-violet-500" />
    </div>
    <p className="mt-3 max-w-2xl text-sm text-slate-600">{subtitle}</p>
  </div>;
}
