import { isRouteErrorResponse, Link, useRouteError } from "react-router-dom";
import { Button, buttonClasses } from "@/components/ui/button";
import { AlertTriangleIcon } from "@/components/ui/icons";

export function RouteErrorPage() {
  const error = useRouteError();
  const status = isRouteErrorResponse(error) ? error.status : null;

  return (
    <div className="flex min-h-[60vh] items-center justify-center px-4 py-16">
      <div className="max-w-lg text-center">
        <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-red-50 text-red-700">
          <AlertTriangleIcon className="size-7" />
        </div>
        <p className="mt-6 text-sm font-semibold text-slate-500">
          {status ? `Error ${status}` : "Unexpected error"}
        </p>
        <h1 className="mt-2 text-3xl leading-tight text-slate-950">
          This page could not be loaded.
        </h1>
        <p className="mt-3 text-sm leading-6 text-slate-600">
          The page may have moved, or the admission service may be temporarily
          unavailable. Return home or reload the page to try again.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <Button variant="secondary" onClick={() => window.location.reload()}>
            Reload page
          </Button>
          <Link to="/" className={buttonClasses()}>
            Go to home
          </Link>
        </div>
      </div>
    </div>
  );
}
