import { useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ReservationCard } from "@/features/student/components/reservation-card";
import { StudentAccessForm } from "@/features/student/components/student-access-form";
import {
  useCreateHold,
  useMyHolds,
} from "@/features/student/hooks/use-holds";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import { UpdatedAt } from "@/components/shared/updated-at";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonClasses } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ErrorState } from "@/components/ui/error-state";
import { RefreshIcon, SchoolIcon } from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { useClasses } from "@/hooks/use-classes";
import { getApiErrorMessage } from "@/lib/api/client";
import type { MyHoldResponse } from "@/types/api";

export function StudentApplyPage() {
  const { studentId, rememberLastHold } = useStudentIdentity();
  const navigate = useNavigate();

  if (!studentId) {
    return (
      <div className="site-container py-8 lg:py-10">
        <div className="mx-auto max-w-xl">
          <PageHeader
            eyebrow="Student portal"
            title="Enter the student portal"
            description="Use a birth registration number before choosing an admission class."
          />
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 sm:p-8">
            <StudentAccessForm submitLabel="Continue to classes" />
          </div>
        </div>
      </div>
    );
  }

  return (
    <StudentApplyContent
      studentId={studentId}
      onHoldCreated={(holdId) => {
        rememberLastHold(holdId);
        navigate(`/student/applications/${encodeURIComponent(holdId)}`);
      }}
    />
  );
}

function StudentApplyContent({
  studentId,
  onHoldCreated,
}: {
  studentId: string;
  onHoldCreated: (holdId: string) => void;
}) {
  const classesQuery = useClasses();
  const holdsQuery = useMyHolds(studentId);
  const createHoldMutation = useCreateHold(studentId);
  const classes = classesQuery.data ?? [];

  const activeHoldsByClass = useMemo(() => {
    const map = new Map<string, MyHoldResponse>();

    for (const hold of holdsQuery.data ?? []) {
      if (hold.status === "HELD" || hold.status === "CONFIRMED") {
        map.set(hold.classId, hold);
      }
    }

    return map;
  }, [holdsQuery.data]);

  function refreshAll() {
    void Promise.all([classesQuery.refetch(), holdsQuery.refetch()]);
  }

  function handleReserve(classId: string) {
    createHoldMutation.mutate(classId, {
      onSuccess: (hold) => onHoldCreated(hold.holdId),
    });
  }

  return (
    <div className="site-container py-8 lg:py-10">
      <PageHeader
        eyebrow="Student portal"
        title="Reserve a seat"
        description="Choose a class with an available seat. Your place is held temporarily until you confirm the admission."
        actions={
          <Button
            variant="secondary"
            size="sm"
            onClick={refreshAll}
            loading={classesQuery.isFetching || holdsQuery.isFetching}
          >
            <RefreshIcon className="size-4" />
            Refresh
          </Button>
        }
      />

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        <Badge tone="brand">
          Student ID: {studentId}
        </Badge>
        <UpdatedAt
          updatedAt={classesQuery.dataUpdatedAt}
          isFetching={classesQuery.isFetching}
        />
      </div>

      {createHoldMutation.isError ? (
        <Alert
          tone="danger"
          title="The seat could not be reserved"
          className="mt-5"
        >
          {getApiErrorMessage(createHoldMutation.error)}
        </Alert>
      ) : null}

      {holdsQuery.isError ? (
        <Alert
          tone="warning"
          title="Existing reservations could not be checked"
          className="mt-5"
          action={
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void holdsQuery.refetch()}
            >
              Retry
            </Button>
          }
        >
          {getApiErrorMessage(holdsQuery.error)} Reservations are disabled until
          this is resolved.
        </Alert>
      ) : null}

      <div className="mt-5">
        <Alert tone="info">
          A reservation is temporary. Open the application before the countdown
          ends to confirm it, or cancel to release the seat immediately.
        </Alert>
      </div>

      {classesQuery.isPending || holdsQuery.isPending ? (
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-64 w-full" />
          ))}
        </div>
      ) : classesQuery.isError ? (
        <ErrorState
          className="mt-6"
          title="Available classes could not be loaded"
          message={getApiErrorMessage(classesQuery.error)}
          onRetry={() => void classesQuery.refetch()}
        />
      ) : classes.length === 0 ? (
        <EmptyState
          className="mt-6 rounded-xl border border-slate-200 bg-white"
          title="No classes are open for admission"
          description="There are no classes available yet. Please check back later or contact the admission office."
          icon={<SchoolIcon className="size-6" />}
          action={
            <Link
              to="/student/applications"
              className={buttonClasses({ variant: "secondary" })}
            >
              View my applications
            </Link>
          }
        />
      ) : (
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {classes.map((classData) => (
            <ReservationCard
              key={classData.id}
              classData={classData}
              activeHold={activeHoldsByClass.get(classData.id)}
              isReserving={
                createHoldMutation.isPending &&
                createHoldMutation.variables === classData.id
              }
              reservationsReady={holdsQuery.isSuccess}
              anyMutationPending={createHoldMutation.isPending}
              onReserve={handleReserve}
            />
          ))}
        </div>
      )}
    </div>
  );
}
