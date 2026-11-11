import { Link, NavLink } from "react-router-dom";
import { buttonClasses } from "@/components/ui/button";
import { ArrowRightIcon, SchoolIcon } from "@/components/ui/icons";
import { cn } from "@/lib/utils";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-slate-200/90 bg-white/95 backdrop-blur">
      <div className="site-container flex min-h-16 items-center justify-between gap-2 sm:gap-4">
        <Link
          to="/"
          className="flex items-center gap-2.5 rounded-md sm:gap-3"
          aria-label="Meridian School admission home"
        >
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-brand-950 text-gold-300 shadow-sm">
            <SchoolIcon className="size-5" />
          </span>
          <span className="hidden leading-tight min-[360px]:block">
            <span className="block whitespace-nowrap text-base font-semibold tracking-tight text-brand-950 sm:text-lg">
              Meridian School
            </span>
            <span className="hidden text-xs font-medium text-slate-500 sm:block">
              Admission Office
            </span>
          </span>
        </Link>

        <nav
          className="flex items-center gap-0.5 sm:gap-1"
          aria-label="Primary navigation"
        >
          <NavLink
            to="/admin"
            className={({ isActive }) =>
              cn(
                "rounded-md px-2 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 sm:px-3",
                isActive && "bg-brand-50 text-brand-800 hover:bg-brand-50",
              )
            }
          >
            Admin
          </NavLink>
          <NavLink
            to="/student"
            className={({ isActive }) =>
              cn(
                "rounded-md px-2 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 sm:px-3",
                isActive && "bg-brand-50 text-brand-800 hover:bg-brand-50",
              )
            }
          >
            Student
          </NavLink>
          <span className="ml-1 hidden md:block">
            <Link
              to="/student/apply"
              className={buttonClasses({ size: "sm" })}
            >
              Reserve a seat
              <ArrowRightIcon className="size-4" />
            </Link>
          </span>
        </nav>
      </div>
    </header>
  );
}
