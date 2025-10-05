import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Tempest Tactical Map",
  description: "Live tactical awareness for Tempest Unturned survivors.",
  metadataBase: new URL("https://tempest.arcfoundation.net")
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="bg-slate-950">
      <body className="min-h-screen text-slate-100">
        <div className="flex min-h-screen flex-col">
          <header className="sticky top-0 z-50 border-b border-white/10 bg-slate-950/80 backdrop-blur">
            <div className="mx-auto flex w-full max-w-6xl items-center justify-between px-6 py-4">
              <Link href="/" className="flex items-center gap-3 text-lg font-semibold tracking-tight text-brand-200">
                <span className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-brand-500/50 bg-brand-500/10 text-brand-200 shadow-lg shadow-brand-500/20">
                  ⚡
                </span>
                Tempest Tactical Awareness
              </Link>
              <nav className="flex items-center gap-6 text-sm uppercase tracking-[0.2em] text-slate-300">
                <Link className="transition hover:text-brand-200" href="/map">
                  Map
                </Link>
                <Link className="transition hover:text-brand-200" href="/docs/setup">
                  Docs
                </Link>
              </nav>
            </div>
          </header>
          <main className="flex flex-1 flex-col">{children}</main>
          <footer className="border-t border-white/10 bg-slate-950/80 py-6 text-center text-xs text-slate-400">
            © {new Date().getFullYear()} ARC Foundation – Tempest Systems Division
          </footer>
        </div>
      </body>
    </html>
  );
}
