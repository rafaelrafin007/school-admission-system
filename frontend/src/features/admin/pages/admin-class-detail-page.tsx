import { Link, useParams } from "react-router-dom";
import { ClassAvailabilityBadge } from "@/components/shared/class-availability";
import { SeatBreakdown } from "@/components/shared/seat-breakdown";
import { UpdatedAt } from "@/components/shared/updated-at";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorState } from "@/components/ui/error-state";
import {
  ArrowLeftIcon,
  ClockIcon,
  RefreshIcon,
  UsersIcon,
} from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { useClass } from "@/hooks/use-classes";
import { getApiErrorMessage, isApiErrorStatus } from "@/lib/api/client";
import { formatNumber } from "@/lib/utils";

export function AdminClassDetailPage() {
  const { classId = "" } = useParams();
  const classQuery = useClass(classId);

  if (classQuery.isPending) {
    return (
      <div className="site-container py-8 lg:py-10">
        <Skeleton className="h-4 w-36" />
        <Skeleton className="mt-5 h-10 w-72 max-w-full" />
        <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <Skeleton className="h-72 w-full" />
          <Skeleton className="h-72 w-full" />
        </div>
      </div>
    );
  }

  if (classQuery.isError) {
    const notFound = isApiErrorStatus(classQuery.error, 404);

    return (
      <div className="site-container py-8 lg:py-10">
        <Link
          to="/admin/classes"
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-brand-800"
        >
          <ArrowLeftIcon className="size-4" />
          Back to classes
        </Link>
        <ErrorState
          className="mt-6"
          title={notFound ? "Class not found" : "Class could not be loaded"}
          message={
            notFound
              ? "This class may have been removed or the link is incorrect."
              : getApiErrorMessage(classQuery.error)
          }
          onRetry={
            notFound ? undefined : () => void classQuery.refetch()
          }
        />
      </div>
    );
  }

  const classData = classQuery.data;

  return (
    <div className="site-container py-8 lg:py-10">
      <Link
        to="/admin/classes"
        className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-brand-800"
      >
        <ArrowLeftIcon className="size-4" />
        Back to classes
      </Link>

      <PageHeader
        eyebrow="Class details"
        title={classData.name}
        description="Live capacity and seat position for this admission class."
        actions={
          <Button
            variant="secondary"
            size="sm"
            onClick={() => void classQuery.refetch()}
            loading={classQuery.isFetching}
          >
            <RefreshIcon className="size-4" />
            Refresh
          </Button>
        }
        className="mt-5"
      />

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        <ClassAvailabilityBadge availableSeats={classData.availableSeats} />
        <UpdatedAt
          updatedAt={classQuery.dataUpdatedAt}
          isFetching={classQuery.isFetching}
        />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <Card className="p-6 sm:p-8">
          <h2 className="text-xl font-semibold text-slate-950">
            Seat capacity
          </h2>
          <p className="mt-1 text-sm leading-6 text-slate-600">
            Held seats are temporary reservations. Confirmed seats are completed
            admissions.
          </p>
          <SeatBreakdown classData={classData} className="mt-6" />

          <div className="mt-8 grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
              <div className="flex items-center gap-2 text-amber-800">
                <ClockIcon className="size-4" />
                <p className="text-xs font-medium">Held</p>
              </div>
              <p className="mt-2 text-2xl font-bold text-amber-950">
                {formatNumber(classData.heldSeats)}
              </p>
            </div>
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4">
              <div className="flex items-center gap-2 text-emerald-800">
                <UsersIcon className="size-4" />
                <p className="text-xs font-medium">Confirmed</p>
              </div>
              <p className="mt-2 text-2xl font-bold text-emerald-950">
                {formatNumber(classData.confirmedSeats)}
              </p>
            </div>
            <div className="rounded-lg border border-brand-200 bg-brand-50 p-4">
              <div className="flex items-center gap-2 text-brand-800">
                <UsersIcon className="size-4" />
                <p className="text-xs font-medium">Available</p>
              </div>
              <p className="mt-2 text-2xl font-bold text-brand-950">
                {formatNumber(classData.availableSeats)}
              </p>
            </div>
          </div>
        </Card>

        <aside className="space-y-4">
          <Card className="p-5">
            <h2 className="text-base font-semibold text-slate-950">
              Class record
            </h2>
            <dl className="mt-4 space-y-3 text-sm">
              <div>
                <dt className="text-xs font-medium text-slate-500">Class ID</dt>
                <dd className="mt-1 break-all text-xs text-slate-700">
                  {classData.id}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-medium text-slate-500">
                  Total seats
                </dt>
                <dd className="mt-1 font-semibold text-slate-900">
                  {formatNumber(classData.totalSeats)}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-medium text-slate-500">
                  Availability
                </dt>
                <dd className="mt-1 text-slate-700">
                  {classData.availableSeats > 0
                    ? `${formatNumber(classData.availableSeats)} seats can still be reserved`
                    : "No seats are currently available"}
                </dd>
              </div>
            </dl>
          </Card>

          <Card className="bg-slate-50 p-5">
            <h2 className="text-base font-semibold text-slate-950">
              Live updates
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              This page refreshes every five seconds while it is open. Use
              Refresh for an immediate update.
            </p>
          </Card>
        </aside>
      </div>
    </div>
  );
}
