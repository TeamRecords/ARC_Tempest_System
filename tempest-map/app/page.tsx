import Link from "next/link";

export default function LandingPage() {
  return (
    <section className="relative flex flex-1 items-center justify-center overflow-hidden">
      <div className="absolute inset-0">
        <div className="pointer-events-none absolute inset-x-0 top-0 h-72 bg-gradient-to-b from-brand-500/20 via-transparent to-transparent" />
        <div className="pointer-events-none absolute inset-y-0 left-0 w-1/3 bg-gradient-to-r from-brand-500/10 via-transparent to-transparent" />
        <div className="pointer-events-none absolute inset-y-0 right-0 w-1/3 bg-gradient-to-l from-brand-500/10 via-transparent to-transparent" />
      </div>
      <div className="relative z-10 mx-auto flex w-full max-w-4xl flex-col items-center gap-10 px-6 text-center">
        <span className="rounded-full border border-brand-400/30 bg-brand-500/10 px-6 py-2 text-xs uppercase tracking-[0.4em] text-brand-200">
          Arc Foundation // Tempest Systems
        </span>
        <h1 className="text-4xl font-semibold tracking-tight text-white sm:text-6xl">
          Command-grade situational awareness for every survivor.
        </h1>
        <p className="max-w-2xl text-lg text-slate-300">
          The Tempest Tactical Map synchronises live player telemetry directly from your Unturned server. Track squads,
          secure objectives, and guide extractions in real time with a modern interface powered by Next.js 15 and Tailwind
          CSS 4.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-4">
          <Link
            href="/map"
            className="group inline-flex items-center gap-3 rounded-full border border-brand-400/40 bg-brand-500/20 px-8 py-3 text-sm font-semibold uppercase tracking-[0.3em] text-brand-50 transition hover:-translate-y-0.5 hover:border-brand-300 hover:bg-brand-400/20 hover:text-brand-100"
          >
            Enter Tactical Map
            <span className="transition-transform group-hover:translate-x-1">→</span>
          </Link>
          <Link
            href="/docs/setup"
            className="inline-flex items-center gap-2 rounded-full border border-white/10 px-8 py-3 text-sm font-semibold uppercase tracking-[0.3em] text-slate-300 transition hover:border-brand-400/40 hover:text-brand-100"
          >
            Deployment Guide
          </Link>
        </div>
      </div>
    </section>
  );
}
