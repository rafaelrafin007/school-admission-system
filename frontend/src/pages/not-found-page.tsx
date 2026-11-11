import { Link } from "react-router-dom";
import { buttonClasses } from "@/components/ui/button";
import { SchoolIcon } from "@/components/ui/icons";

export function NotFoundPage() {
  return (
    <div className="site-container flex min-h-[60vh] items-center justify-center py-16">
      <div className="max-w-lg text-center">
        <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-brand-50 text-brand-700">
          <SchoolIcon className="size-7" />
        </div>
        <p className="mt-6 text-sm font-semibold text-slate-500">404</p>
        <h1 className="mt-2 text-3xl leading-tight text-slate-950 sm:text-4xl">
          Page not found
        </h1>
        <p className="mt-3 text-sm leading-6 text-slate-600">
          The page you are looking for may have moved, or the address may be
          incorrect.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <Link to="/" className={buttonClasses()}>
            Go to home
          </Link>
          <Link
            to="/student"
            className={buttonClasses({ variant: "secondary" })}
          >
            Student portal
          </Link>
        </div>
      </div>
    </div>
  );
}
