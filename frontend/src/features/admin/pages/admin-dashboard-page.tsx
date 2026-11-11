import { Link } from "react-router-dom";
import { ClassTable } from "@/features/admin/components/class-table";
import { useClasses } from "@/hooks/use-classes";
import { UpdatedAt } from "@/components/shared/updated-at";
import { StatCard } from "@/components/shared/stat-card";
import { Button, buttonClasses } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ErrorState } from "@/components/ui/error-state";
import {
  ClockIcon,
  RefreshIcon,
  SchoolIcon,
  SeatIcon,
  UsersIcon,
} from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { getApiErrorMessage } from "@/lib/api/client";
import { formatNumber } from "@/lib/utils";

export function AdminDashboardPage() {
  const classesQuery = useClasses();
  const classes = classesQuery.data ?? [];

  const totals = classes.reduce(
    (summary, classData) => ({
      totalSeats: summary.totalSeats + classData.totalSeats,
      heldSeats: summary.heldSeats + classData.heldSeats,
      confirmedSeats: summary.confirmedSeats + classData.confirmedSeats,
      availableSeats: summary.availableSeats + classData.availableSeats,
    }),
    {
      totalSeats: 0,
      heldSeats: 0,
      confirmedSeats: 0,
      availableSeats: 0,
    },
  );

  return (
    <div className="site-container py-8 lg:py-10">
      <PageHeader
        eyebrow="Admissions administration"
        title="Admissions overview"
        description="Monitor live seat availability and manage the classes open for admission."
        actions={
          <>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void classesQuery.refetch()}
              loading={classesQuery.isFetching}
            >
              <RefreshIcon className="size-4" />
              Refresh
            </Button>
            <Link to="/admin/classes/new" className={buttonClasses({ size: "sm" })}>
              Create class
            </Link>
          </>
        }
      />

      <div className="mt-3 flex justify-end">
        <UpdatedAt
          updatedAt={classesQuery.dataUpdatedAt}
          isFetching={classesQuery.isFetching}
        />
      </div>

      {classesQuery.isPending ? (
          <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-28 w-full" />
          ))}
          <Skeleton className="h-64 w-full sm:col-span-2 lg:col-span-4" />
        </div>
      ) : classesQuery.isError ? (
        <ErrorState
          className="mt-6"
          title="Admissions data is unavailable"
          message={getApiErrorMessage(classesQuery.error)}
          onRetry={() => void classesQuery.refetch()}
        />
      ) : (
        <>
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              label="Classes"
              value={formatNumber(classes.length)}
              hint="Open for admission"
              icon={<SchoolIcon className="size-5" />}
            />
            <StatCard
              label="Available seats"
              value={formatNumber(totals.availableSeats)}
              hint={`${formatNumber(totals.totalSeats)} seats in total`}
              icon={<SeatIcon className="size-5" />}
            />
            <StatCard
              label="Held seats"
              value={formatNumber(totals.heldSeats)}
              hint="Temporary reservations"
              icon={<ClockIcon className="size-5" />}
            />
            <StatCard
              label="Confirmed"
              value={formatNumber(totals.confirmedSeats)}
              hint="Confirmed admissions"
              icon={<UsersIcon className="size-5" />}
            />
          </div>

          <div className="mt-8">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold text-slate-950">
                  Classes at a glance
                </h2>
                <p className="mt-1 text-sm text-slate-600">
                  Updates automatically while this page is open.
                </p>
              </div>
              {classes.length > 0 ? (
                <Link
                  to="/admin/classes"
                  className="text-sm font-semibold text-brand-800 hover:text-brand-900"
                >
                  View all classes
                </Link>
              ) : null}
            </div>

            {classes.length === 0 ? (
              <EmptyState
                className="rounded-xl border border-slate-200 bg-white"
                title="No classes yet"
                description="Create the first admission class to begin accepting seat reservations."
                action={
                  <Link to="/admin/classes/new" className={buttonClasses()}>
                    Create the first class
                  </Link>
                }
              />
            ) : (
              <ClassTable classes={classes} />
            )}
          </div>
        </>
      )}
    </div>
  );
}
