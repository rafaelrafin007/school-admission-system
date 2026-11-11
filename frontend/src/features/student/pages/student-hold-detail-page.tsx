import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { StudentAccessForm } from "@/features/student/components/student-access-form";
import {
  useCancelHold,
  useConfirmHold,
  useHold,
} from "@/features/student/hooks/use-holds";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import { ClassAvailabilityBadge } from "@/components/shared/class-availability";
import {
  HoldStatusBadge,
  holdStatusExplanation,
} from "@/components/shared/hold-status-badge";
import { SeatBreakdown } from "@/components/shared/seat-breakdown";
import { UpdatedAt } from "@/components/shared/updated-at";
import { Alert } from "@/components/ui/alert";
import { Button, buttonClasses } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { ErrorState } from "@/components/ui/error-state";
import {
  ArrowLeftIcon,
  CheckCircleIcon,
  ClockIcon,
  RefreshIcon,
} from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { useCountdown } from "@/hooks/use-countdown";
import { useClasses } from "@/hooks/use-classes";
import { getApiErrorMessage, isApiErrorStatus } from "@/lib/api/client";
import { formatDateTime, formatNumber } from "@/lib/utils";
import type { HoldStatus } from "@/types/api";

export function StudentHoldDetailPage() {
  const { holdId = "" } = useParams();
  const { studentId } = useStudentIdentity();

  if (!studentId) {
    return (
      <div className="site-container py-8 lg:py-10">
        <div className="mx-auto max-w-xl">
          <PageHeader
            eyebrow="Student portal"
            title="Open an application"
            description="Enter the birth registration number used to create this reservation."
          />
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 sm:p-8">
            <StudentAccessForm
              submitLabel="Open application"
              to={`/student/applications/${encodeURIComponent(holdId)}`}
            />
          </div>
        </div>
      </div>
    );
  }

  return <HoldDetailContent holdId={holdId} studentId={studentId} />;
}

function HoldDetailContent({
  holdId,
  studentId,
}: {
  holdId: string;
  studentId: string;
}) {
  const holdQuery = useHold(holdId);
  const classesQuery = useClasses();
  const confirmMutation = useConfirmHold(studentId);
  const cancelMutation = useCancelHold(studentId);
  const [dialog, setDialog] = useState<"confirm" | "cancel" | null>(null);
  const refreshedAfterExpiry = useRef(false);

  const hold = holdQuery.data;
  const countdown = useCountdown(hold?.expiresAt);
  const classData = classesQuery.data?.find(
    (admissionClass) => admissionClass.id === hold?.classId,
  );
  const className = classData?.name ?? "Admission application";
  const hasExpiredLocally = hold?.status === "HELD" && countdown.isExpired;
  const displayStatus: HoldStatus = hasExpiredLocally
    ? "EXPIRED"
    : (hold?.status ?? "HELD");
  const canAct = hold?.status === "HELD" && !countdown.isExpired;
  const mutationPending =
    confirmMutation.isPending || cancelMutation.isPending;

  useEffect(() => {
    refreshedAfterExpiry.current = false;
    setDialog(null);
    confirmMutation.reset();
    cancelMutation.reset();
  }, [holdId]);

  useEffect(() => {
    if (
      hold?.status !== "HELD" ||
      !countdown.isExpired ||
      refreshedAfterExpiry.current
    ) {
      return;
    }

    refreshedAfterExpiry.current = true;
    void holdQuery.refetch();
  }, [countdown.isExpired, hold?.status, holdQuery, holdQuery.refetch]);

  if (holdQuery.isPending) {
    return (
      <div className="site-container py-8 lg:py-10">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="mt-5 h-10 w-80 max-w-full" />
        <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <Skeleton className="h-80 w-full" />
          <Skeleton className="h-80 w-full" />
        </div>
      </div>
    );
  }

  if (holdQuery.isError) {
    const notFound = isApiErrorStatus(holdQuery.error, 404);

    return (
      <div className="site-container py-8 lg:py-10">
        <Link
          to="/student/applications"
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-brand-800"
        >
          <ArrowLeftIcon className="size-4" />
          Back to my applications
        </Link>
        <ErrorState
          className="mt-6"
          title={notFound ? "Application not found" : "Application could not be loaded"}
          message={
            notFound
              ? "This application link is invalid or the reservation no longer exists."
              : getApiErrorMessage(holdQuery.error)
          }
          onRetry={notFound ? undefined : () => void holdQuery.refetch()}
        />
      </div>
    );
  }

  const mutationError = confirmMutation.error ?? cancelMutation.error;

  function handleConfirm() {
    confirmMutation.mutate(hold!.holdId, {
      onSuccess: () => setDialog(null),
      onError: () => setDialog(null),
    });
  }

  function handleCancel() {
    cancelMutation.mutate(hold!.holdId, {
      onSuccess: () => setDialog(null),
      onError: () => setDialog(null),
    });
  }

  return (
    <div className="site-container py-8 lg:py-10">
      <Link
        to="/student/applications"
        className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-brand-800"
      >
        <ArrowLeftIcon className="size-4" />
        Back to my applications
      </Link>

      <PageHeader
        eyebrow="Student application"
        title={className}
        description={`Application ${hold!.holdId}`}
        actions={
          <>
            <HoldStatusBadge status={displayStatus} />
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void holdQuery.refetch()}
              loading={holdQuery.isFetching}
            >
              <RefreshIcon className="size-4" />
              Refresh
            </Button>
          </>
        }
        className="mt-5"
      />
      <div className="mt-3 flex justify-end">
        <UpdatedAt
          updatedAt={holdQuery.dataUpdatedAt}
          isFetching={holdQuery.isFetching}
        />
      </div>

      {mutationError ? (
        <Alert tone="danger" title="The action could not be completed" className="mt-5">
          {getApiErrorMessage(mutationError)}
        </Alert>
      ) : null}

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-6">
          <Card className="p-6 sm:p-8">
            {canAct ? (
              <>
                <div className="flex items-center gap-2 text-amber-800">
                  <ClockIcon className="size-5" />
                  <h2 className="text-xl font-semibold">Reservation active</h2>
                </div>
                <p className="mt-2 text-sm leading-6 text-slate-600">
                  Confirm this admission before the timer ends. When it reaches
                  zero, the held seat is released automatically.
                </p>
                <div className="mt-5 rounded-xl border border-amber-200 bg-amber-50 px-5 py-6 text-center">
                  <p className="text-xs font-medium text-amber-800">
                    Time remaining
                  </p>
                  <p
                    className="mt-2 text-4xl font-bold tabular-nums text-amber-950 sm:text-5xl"
                    role="timer"
                    aria-label="Reservation time remaining"
                  >
                    {countdown.label}
                  </p>
                  <p className="mt-2 text-xs text-amber-800">
                    Expires {formatDateTime(hold!.expiresAt)}
                  </p>
                </div>
                <div className="mt-6 flex flex-col gap-3 sm:flex-row">
                  <Button
                    type="button"
                    variant="success"
                    onClick={() => setDialog("confirm")}
                    disabled={mutationPending}
                  >
                    <CheckCircleIcon className="size-4" />
                    Confirm admission
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    className="border-red-200 text-red-700 hover:bg-red-50"
                    onClick={() => setDialog("cancel")}
                    disabled={mutationPending}
                  >
                    Cancel reservation
                  </Button>
                </div>
              </>
            ) : (
              <StatusPanel
                status={displayStatus}
                expiredLocally={hasExpiredLocally}
                expiration={hold!.expiresAt}
                onRefresh={() => void holdQuery.refetch()}
                isRefreshing={holdQuery.isFetching}
              />
            )}
          </Card>

          <Card className="p-6 sm:p-8">
            <h2 className="text-xl font-semibold text-slate-950">
              Application details
            </h2>
            <dl className="mt-5 grid grid-cols-1 gap-x-6 gap-y-5 text-sm sm:grid-cols-2">
              <DetailItem label="Class" value={className} />
              <DetailItem
                label="Student ID"
                value={studentId}
                mono
              />
              <DetailItem
                label="Application ID"
                value={hold!.holdId}
                mono
              />
              <DetailItem
                label="Reservation deadline"
                value={formatDateTime(hold!.expiresAt)}
              />
            </dl>
          </Card>
        </div>

        <aside className="space-y-4">
          <Card className="p-5">
            <h2 className="text-base font-semibold text-slate-950">
              Status
            </h2>
            <div className="mt-3">
              <HoldStatusBadge status={displayStatus} />
            </div>
            <p className="mt-3 text-sm leading-6 text-slate-600">
              {holdStatusExplanation(displayStatus)}
            </p>
          </Card>

          {classData ? (
            <Card className="p-5">
              <div className="flex items-start justify-between gap-2">
                <h2 className="text-base font-semibold text-slate-950">
                  Class availability
                </h2>
                <ClassAvailabilityBadge
                  availableSeats={classData.availableSeats}
                />
              </div>
              <p className="mt-1 text-xs text-slate-500">{classData.name}</p>
              <SeatBreakdown classData={classData} compact className="mt-4" />
              <p className="mt-3 text-xs text-slate-500">
                {formatNumber(classData.availableSeats)} seats currently
                available.
              </p>
            </Card>
          ) : classesQuery.isPending ? (
            <Skeleton className="h-40 w-full" />
          ) : null}

          <Card className="bg-slate-50 p-5">
            <h2 className="text-base font-semibold text-slate-950">
              Need a different class?
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              You can browse other classes with available seats at any time.
            </p>
            <Link
              to="/student/apply"
              className={buttonClasses({
                variant: "secondary",
                size: "sm",
                className: "mt-4 bg-white",
              })}
            >
              Browse classes
            </Link>
          </Card>
        </aside>
      </div>

      <ConfirmDialog
        open={dialog === "confirm"}
        title="Confirm this admission?"
        description="Confirmation will use one seat in this class and is final from the student portal. Please check the details before continuing."
        confirmLabel="Confirm admission"
        cancelLabel="Go back"
        loading={confirmMutation.isPending}
        onConfirm={handleConfirm}
        onCancel={() => setDialog(null)}
      >
        <dl className="space-y-2 rounded-lg bg-slate-50 p-4 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-slate-500">Class</dt>
            <dd className="font-medium text-slate-900">{className}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-slate-500">Student ID</dt>
            <dd className="font-medium text-slate-900">
              {studentId}
            </dd>
          </div>
        </dl>
      </ConfirmDialog>

      <ConfirmDialog
        open={dialog === "cancel"}
        title="Cancel this reservation?"
        description="The held seat will be released immediately for another student. Your application will be kept with a cancelled status."
        confirmLabel="Cancel reservation"
        cancelLabel="Keep reservation"
        tone="danger"
        loading={cancelMutation.isPending}
        onConfirm={handleCancel}
        onCancel={() => setDialog(null)}
      />
    </div>
  );
}

function DetailItem({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd
        className={
          mono
            ? "mt-1 break-all text-xs text-slate-800"
            : "mt-1 text-slate-800"
        }
      >
        {value}
      </dd>
    </div>
  );
}

function StatusPanel({
  status,
  expiredLocally,
  expiration,
  onRefresh,
  isRefreshing,
}: {
  status: HoldStatus;
  expiredLocally: boolean;
  expiration: string;
  onRefresh: () => void;
  isRefreshing: boolean;
}) {
  if (status === "CONFIRMED") {
    return (
      <>
        <div className="flex items-center gap-2 text-emerald-800">
          <CheckCircleIcon className="size-5" />
          <h2 className="text-xl font-semibold">Admission confirmed</h2>
        </div>
        <p className="mt-2 text-sm leading-6 text-slate-600">
          This seat is confirmed. The admission office can provide any further
          instructions for joining the class.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          <Link to="/student/applications" className={buttonClasses()}>
            View all applications
          </Link>
          <Link
            to="/student/apply"
            className={buttonClasses({ variant: "secondary" })}
          >
            Browse other classes
          </Link>
        </div>
      </>
    );
  }

  if (status === "CANCELLED") {
    return (
      <>
        <h2 className="text-xl font-semibold text-slate-950">
          Reservation cancelled
        </h2>
        <p className="mt-2 text-sm leading-6 text-slate-600">
          This reservation was cancelled and the seat has been released. You can
          reserve a place in another class with an available seat.
        </p>
        <Link
          to="/student/apply"
          className={buttonClasses({ className: "mt-5" })}
        >
          Browse available classes
        </Link>
      </>
    );
  }

  return (
    <>
      <Alert
        tone="warning"
        title={
          expiredLocally
            ? "The reservation time has ended"
            : "This reservation has expired"
        }
      >
        {expiredLocally
          ? "Confirming or cancelling is no longer available from this page. Refresh the status to check the final record."
          : `This reservation expired on ${formatDateTime(expiration)}.`}
      </Alert>
      <div className="mt-5 flex flex-wrap gap-3">
        <Button
          variant="secondary"
          onClick={onRefresh}
          loading={isRefreshing}
        >
          <RefreshIcon className="size-4" />
          Refresh status
        </Button>
        <Link
          to="/student/apply"
          className={buttonClasses({ variant: "secondary" })}
        >
          Browse available classes
        </Link>
      </div>
    </>
  );
}
