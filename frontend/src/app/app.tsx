import { Suspense } from "react";
import { RouterProvider } from "react-router-dom";
import { router } from "@/app/router";

function PageLoading() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface">
      <div className="text-center">
        <div className="mx-auto size-8 animate-spin rounded-full border-2 border-brand-900 border-r-transparent" />
        <p className="sr-only">Loading page</p>
      </div>
    </div>
  );
}

export function App() {
  return (
    <Suspense fallback={<PageLoading />}>
      <RouterProvider router={router} />
    </Suspense>
  );
}
