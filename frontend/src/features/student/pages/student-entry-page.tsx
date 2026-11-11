import { Link } from "react-router-dom";
import { ApplicationCard } from "@/features/student/components/application-card";
import { StudentAccessForm } from "@/features/student/components/student-access-form";
import { useMyHolds } from "@/features/student/hooks/use-holds";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import { Alert } from "@/components/ui/alert";
import { buttonClasses } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  ArrowRightIcon,
  IdentificationIcon,
  ShieldCheckIcon,
} from "@/components/ui/icons";
import { Skeleton } from "@/components/ui/skeleton";
import { getApiErrorMessage } from "@/lib/api/client";

const steps = [
  {
    title: "Browse available classes",
    description:
      "See live seat availability before choosing the class that suits you.",
  },
  {
    title: "Reserve a seat",
    description:
      "Your seat is held temporarily while you review the details.",
  },
  {
    title: "Confirm your admission",
    description:
      "Confirm before the countdown ends, or cancel to release the seat.",
  },
];

export function StudentEntryPage() {
  const { studentId, lastHoldId } = useStudentIdentity();
  const holdsQuery = useMyHolds(studentId);
  const latestHold = holdsQuery.data?.[0];

  if (!studentId) {
    return (
      <div className="site-container py-8 lg:py-12">
        <div className="grid grid-cols-1 items-stretch gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <section className="relative overflow-hidden rounded-2xl bg-brand-950 p-7 text-white sm:p-10">
            <div className="absolute right-0 top-0 size-56 translate-x-20 -translate-y-24 rounded-full border border-white/10" />
            <div className="relative">
              <p className="text-sm font-medium text-gold-300">
                Admissions office
              </p>
              <h1 className="mt-4 max-w-xl text-[2.5rem] leading-[1.08] sm:text-5xl">
                Your place starts with a simple reservation.
              </h1>
              <p className="mt-5 max-w-lg text-sm leading-7 text-slate-200 sm:text-base">
                Browse classes, reserve a seat while you decide, and confirm your
                admission online. Seat availability is updated live from the
                school office.
              </p>

              <ol className="mt-8 space-y-5">
                {steps.map((step, index) => (
                  <li key={step.title} className="flex gap-4">
                    <span className="flex size-8 shrink-0 items-center justify-center rounded-full border border-gold-400/60 text-sm font-semibold text-gold-300">
                      {index + 1}
                    </span>
                    <div>
                      <h2 className="text-base font-semibold text-white">
                        {step.title}
                      </h2>
                      <p className="mt-1 text-sm leading-6 text-slate-300">
                        {step.description}
                      </p>
                    </div>
                  </li>
                ))}
              </ol>

              <div className="mt-8 flex items-center gap-2 border-t border-white/10 pt-5 text-xs text-slate-300">
                <ShieldCheckIcon className="size-4 text-gold-300" />
                Your reservation is managed by the school admission office.
              </div>
            </div>
          </section>

          <Card className="p-6 sm:p-8">
            <div className="flex size-11 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
              <IdentificationIcon className="size-5" />
            </div>
            <h2 className="mt-5 text-xl font-semibold text-slate-950">
              Enter the student portal
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              Use the student’s birth registration number to reserve a seat or
              return to an existing application.
            </p>

            <div className="mt-6">
              <StudentAccessForm submitLabel="Continue to classes" />
            </div>

            <div className="mt-6 border-t border-slate-100 pt-5 text-sm text-slate-600">
              Already have an application?{" "}
              <Link
                to="/student/applications"
                className="font-semibold text-brand-800 hover:text-brand-900"
              >
                Find it with your student ID
              </Link>
              .
            </div>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="site-container py-8 lg:py-12">
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <section className="relative overflow-hidden rounded-2xl bg-brand-950 p-7 text-white sm:p-10">
          <div className="absolute right-0 top-0 size-56 translate-x-20 -translate-y-24 rounded-full border border-white/10" />
          <div className="relative">
            <p className="text-sm font-medium text-gold-300">Student portal</p>
            <h1 className="mt-4 max-w-xl text-[2.5rem] leading-[1.08] sm:text-5xl">
              Welcome back.
            </h1>
            <p className="mt-4 max-w-lg text-sm leading-7 text-slate-200">
              Your student reference is saved on this device. Reserve another
              seat or check the status of an existing application.
            </p>

            <div className="mt-6 inline-flex flex-wrap items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-4 py-3 text-sm">
              <span className="text-slate-300">Birth registration number</span>
              <span className="text-base font-semibold text-white">
                {studentId}
              </span>
            </div>

            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                to="/student/apply"
                className={buttonClasses({
                  variant: "secondary",
                  className: "bg-white",
                })}
              >
                Apply for a seat
                <ArrowRightIcon className="size-4" />
              </Link>
              <Link
                to="/student/applications"
                className={buttonClasses({
                  variant: "ghost",
                  className:
                    "border-white/25 text-white hover:bg-white/10 hover:text-white",
                })}
              >
                View all applications
              </Link>
            </div>
          </div>
        </section>

        <div className="space-y-4">
          <div className="flex items-end justify-between gap-3">
            <div>
              <p className="text-xs font-semibold text-brand-700">
                Most recent
              </p>
              <h2 className="mt-1 text-xl font-semibold text-slate-950">
                Your latest application
              </h2>
            </div>
            {holdsQuery.data && holdsQuery.data.length > 0 ? (
              <Link
                to="/student/applications"
                className="text-sm font-semibold text-brand-800 hover:text-brand-900"
              >
                See all
              </Link>
            ) : null}
          </div>

          {holdsQuery.isPending ? (
            <Skeleton className="h-56 w-full" />
          ) : holdsQuery.isError ? (
            <Alert tone="warning" title="Applications could not be loaded">
              {getApiErrorMessage(holdsQuery.error)}
            </Alert>
          ) : latestHold ? (
            <ApplicationCard hold={latestHold} />
          ) : (
            <Card className="p-6">
              <h3 className="text-lg font-semibold text-slate-950">
                No applications yet
              </h3>
              <p className="mt-2 text-sm leading-6 text-slate-600">
                Browse the classes open for admission and reserve a seat when you
                are ready.
              </p>
              <Link
                to="/student/apply"
                className={buttonClasses({ size: "sm", className: "mt-4" })}
              >
                Browse classes
              </Link>
            </Card>
          )}

          {lastHoldId ? (
            <Link
              to={`/student/applications/${encodeURIComponent(lastHoldId)}`}
              className="flex items-center justify-between gap-3 rounded-xl border border-brand-200 bg-brand-50 p-4 text-sm font-semibold text-brand-900 transition-colors hover:bg-brand-100"
            >
              Continue with your latest reservation
              <ArrowRightIcon className="size-4 shrink-0" />
            </Link>
          ) : null}
        </div>
      </div>
    </div>
  );
}
