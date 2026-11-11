import { Link } from "react-router-dom";
import { ApplicationCard } from "@/features/student/components/application-card";
import { StudentAccessForm } from "@/features/student/components/student-access-form";
import { useMyHolds } from "@/features/student/hooks/use-holds";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import { UpdatedAt } from "@/components/shared/updated-at";
import { Badge } from "@/components/ui/badge";
import { Button, buttonClasses } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ErrorState } from "@/components/ui/error-state";
import { ClipboardIcon, RefreshIcon } from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { getApiErrorMessage } from "@/lib/api/client";

export function StudentApplicationsPage() {
  const { studentId } = useStudentIdentity();
  const holdsQuery = useMyHolds(studentId);

  if (!studentId) {
    return (
      <div className="site-container py-8 lg:py-10">
        <div className="mx-auto max-w-xl">
          <PageHeader
            eyebrow="Student portal"
            title="Find your applications"
            description="Enter the birth registration number used for the reservation."
          />
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 sm:p-8">
            <StudentAccessForm
              submitLabel="View my applications"
              to="/student/applications"
            />
          </div>
        </div>
      </div>
    );
  }

  const holds = holdsQuery.data ?? [];

  return (
    <div className="site-container py-8 lg:py-10">
      <PageHeader
        eyebrow="Student portal"
        title="My applications"
        description="Every reservation made with this student ID, newest first."
        actions={
          <>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void holdsQuery.refetch()}
              loading={holdsQuery.isFetching}
            >
              <RefreshIcon className="size-4" />
              Refresh
            </Button>
            <Link
              to="/student/apply"
              className={buttonClasses({ size: "sm" })}
            >
              Reserve a seat
            </Link>
          </>
        }
      />

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        <Badge tone="brand">
          Student ID: {studentId}
        </Badge>
        <UpdatedAt
          updatedAt={holdsQuery.dataUpdatedAt}
          isFetching={holdsQuery.isFetching}
        />
      </div>

      {holdsQuery.isPending ? (
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-64 w-full" />
          ))}
        </div>
      ) : holdsQuery.isError ? (
        <ErrorState
          className="mt-6"
          title="Applications could not be loaded"
          message={getApiErrorMessage(holdsQuery.error)}
          onRetry={() => void holdsQuery.refetch()}
        />
      ) : holds.length === 0 ? (
        <EmptyState
          className="mt-6 rounded-xl border border-slate-200 bg-white"
          title="No applications yet"
          description="Reserve a seat in an available class and it will appear here."
          icon={<ClipboardIcon className="size-6" />}
          action={
            <Link to="/student/apply" className={buttonClasses()}>
              Browse available classes
            </Link>
          }
        />
      ) : (
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {holds.map((hold) => (
            <ApplicationCard key={hold.holdId} hold={hold} />
          ))}
        </div>
      )}
    </div>
  );
}
