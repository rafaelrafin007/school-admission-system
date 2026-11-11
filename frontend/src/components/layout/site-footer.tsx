import { Link } from "react-router-dom";
import { SchoolIcon } from "@/components/ui/icons";

export function SiteFooter() {
  return (
    <footer className="mt-auto border-t border-slate-200 bg-white">
      <div className="site-container flex flex-col gap-6 py-8 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-md bg-brand-950 text-gold-300">
            <SchoolIcon className="size-4" />
          </span>
          <div>
            <p className="text-base font-semibold tracking-tight text-brand-950">
              Meridian School
            </p>
            <p className="mt-1 max-w-sm text-xs leading-5 text-slate-500">
              A demonstration admission portal. Seat reservations are temporary
              and managed by the school admission office.
            </p>
          </div>
        </div>
        <nav
          className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-slate-600"
          aria-label="Footer navigation"
        >
          <Link className="hover:text-brand-800" to="/admin">
            Administration
          </Link>
          <Link className="hover:text-brand-800" to="/student">
            Student portal
          </Link>
          <Link className="hover:text-brand-800" to="/student/applications">
            My applications
          </Link>
        </nav>
      </div>
    </footer>
  );
}
