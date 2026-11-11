import { Link } from "react-router-dom";
import { UpdatedAt } from "@/components/shared/updated-at";
import { buttonClasses } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  ArrowRightIcon,
  SchoolIcon,
  SeatIcon,
  ShieldCheckIcon,
  UsersIcon,
} from "@/components/ui/icons";
import { Skeleton } from "@/components/ui/skeleton";
import { useClasses } from "@/hooks/use-classes";
import { getApiErrorMessage } from "@/lib/api/client";
import { formatNumber } from "@/lib/utils";

export function HomePage() {
  const classesQuery = useClasses();
  const classes = classesQuery.data ?? [];
  const totalAvailable = classes.reduce(
    (total, classData) => total + classData.availableSeats,
    0,
  );
  const totalConfirmed = classes.reduce(
    (total, classData) => total + classData.confirmedSeats,
    0,
  );

  return (
    <div>
      <section className="relative overflow-hidden bg-brand-950 text-white">
        <div className="absolute -right-24 -top-32 size-96 rounded-full border border-white/10" />
        <div className="absolute -bottom-40 left-1/4 size-80 rounded-full border border-gold-400/15" />
        <div className="site-container relative grid grid-cols-1 gap-10 py-12 sm:py-16 lg:grid-cols-[1.15fr_0.85fr] lg:items-center lg:py-20">
          <div>
            <p className="text-sm font-medium text-gold-300">Admissions open</p>
            <h1 className="mt-4 max-w-2xl text-[2.5rem] font-bold leading-[1.08] sm:text-5xl lg:text-[3.25rem]">
              A clearer path to the right school place.
            </h1>
            <p className="mt-5 max-w-xl text-sm leading-7 text-slate-200 sm:text-base">
              Browse live class availability, reserve a seat while you decide,
              and confirm the admission online. Families and the admission
              office see the same inventory in real time.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                to="/student/apply"
                className={buttonClasses({
                  size: "lg",
                  variant: "secondary",
                  className: "bg-white",
                })}
              >
                Reserve a seat
                <ArrowRightIcon className="size-5" />
              </Link>
              <Link
                to="/student"
                className={buttonClasses({
                  size: "lg",
                  variant: "ghost",
                  className:
                    "border-white/25 text-white hover:bg-white/10 hover:text-white",
                })}
              >
                Student portal
              </Link>
            </div>
            <p className="mt-5 flex items-center gap-2 text-xs text-slate-300">
              <ShieldCheckIcon className="size-4 text-gold-300" />
              A temporary reservation does not complete the admission until it
              is confirmed.
            </p>
          </div>

          <Card className="p-6 text-slate-900 shadow-xl sm:p-7">
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold text-brand-700">
                  Live snapshot
                </p>
                <h2 className="mt-1 text-xl font-semibold">
                  Current availability
                </h2>
              </div>
              <span className="flex size-10 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
                <SchoolIcon className="size-5" />
              </span>
            </div>

            {classesQuery.isPending ? (
              <div className="mt-6 space-y-3">
                <Skeleton className="h-20 w-full" />
                <Skeleton className="h-12 w-full" />
              </div>
            ) : classesQuery.isError ? (
              <div className="mt-5 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-900">
                {getApiErrorMessage(classesQuery.error)}
              </div>
            ) : (
              <>
                <div className="mt-6 grid grid-cols-2 gap-3">
                  <div className="rounded-lg bg-brand-50 p-4">
                    <div className="flex items-center gap-2 text-brand-700">
                      <SeatIcon className="size-4" />
                      <span className="text-xs font-medium">Available</span>
                    </div>
                    <p className="mt-2 text-3xl font-bold text-brand-950">
                      {formatNumber(totalAvailable)}
                    </p>
                    <p className="mt-1 text-xs text-brand-800">seats right now</p>
                  </div>
                  <div className="rounded-lg bg-slate-50 p-4">
                    <div className="flex items-center gap-2 text-slate-600">
                      <UsersIcon className="size-4" />
                      <span className="text-xs font-medium">Confirmed</span>
                    </div>
                    <p className="mt-2 text-3xl font-bold text-slate-950">
                      {formatNumber(totalConfirmed)}
                    </p>
                    <p className="mt-1 text-xs text-slate-500">
                      current admissions
                    </p>
                  </div>
                </div>
                <div className="mt-4 flex items-center justify-between gap-3 border-t border-slate-100 pt-4 text-sm">
                  <span className="text-slate-600">
                    {formatNumber(classes.length)}{" "}
                    {classes.length === 1 ? "class" : "classes"} open
                  </span>
                  <Link
                    to="/student/apply"
                    className="inline-flex items-center gap-1 font-semibold text-brand-800 hover:text-brand-900"
                  >
                    View classes
                    <ArrowRightIcon className="size-4" />
                  </Link>
                </div>
                <div className="mt-2 text-right">
                  <UpdatedAt
                    updatedAt={classesQuery.dataUpdatedAt}
                    isFetching={classesQuery.isFetching}
                  />
                </div>
              </>
            )}
          </Card>
        </div>
      </section>

      <section className="site-container py-12 lg:py-16">
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
          <Card className="p-6 sm:p-8">
            <div className="flex size-11 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
              <UsersIcon className="size-5" />
            </div>
            <h2 className="mt-5 text-xl font-semibold text-slate-950">
              For families
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              Enter a birth registration number, compare classes, and reserve a
              seat in a few steps. Return at any time to check the application.
            </p>
            <div className="mt-5 flex flex-wrap gap-4 text-sm font-semibold">
              <Link
                to="/student/apply"
                className="inline-flex items-center gap-1 text-brand-800 hover:text-brand-900"
              >
                Find a class
                <ArrowRightIcon className="size-4" />
              </Link>
              <Link
                to="/student/applications"
                className="inline-flex items-center gap-1 text-slate-600 hover:text-slate-900"
              >
                View my applications
              </Link>
            </div>
          </Card>

          <Card className="p-6 sm:p-8">
            <div className="flex size-11 items-center justify-center rounded-lg bg-slate-100 text-slate-700">
              <SchoolIcon className="size-5" />
            </div>
            <h2 className="mt-5 text-xl font-semibold text-slate-950">
              For the admission office
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              Create classes, set seat capacity, and monitor held and confirmed
              admissions. Availability updates automatically as reservations
              change.
            </p>
            <div className="mt-5 flex flex-wrap gap-4 text-sm font-semibold">
              <Link
                to="/admin"
                className="inline-flex items-center gap-1 text-brand-800 hover:text-brand-900"
              >
                Open administration
                <ArrowRightIcon className="size-4" />
              </Link>
              <Link
                to="/admin/classes/new"
                className="inline-flex items-center gap-1 text-slate-600 hover:text-slate-900"
              >
                Create a class
              </Link>
            </div>
          </Card>
        </div>
      </section>
    </div>
  );
}
