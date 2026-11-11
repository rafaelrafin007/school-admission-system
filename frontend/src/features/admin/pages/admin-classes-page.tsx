import { Link } from "react-router-dom";
import { ClassCard } from "@/features/admin/components/class-card";
import { UpdatedAt } from "@/components/shared/updated-at";
import { Button, buttonClasses } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ErrorState } from "@/components/ui/error-state";
import { RefreshIcon, SchoolIcon } from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { useClasses } from "@/hooks/use-classes";
import { getApiErrorMessage } from "@/lib/api/client";

export function AdminClassesPage() {
  const classesQuery = useClasses();
  const classes = classesQuery.data ?? [];

  return (
    <div className="site-container py-8 lg:py-10">
      <PageHeader
        eyebrow="Administration"
        title="Admission classes"
        description="Every class, its total capacity, and its current seat position."
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
            <Link
              to="/admin/classes/new"
              className={buttonClasses({ size: "sm" })}
            >
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
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-56 w-full" />
          ))}
        </div>
      ) : classesQuery.isError ? (
        <ErrorState
          className="mt-6"
          title="Classes could not be loaded"
          message={getApiErrorMessage(classesQuery.error)}
          onRetry={() => void classesQuery.refetch()}
        />
      ) : classes.length === 0 ? (
        <EmptyState
          className="mt-6 rounded-xl border border-slate-200 bg-white"
          title="No classes have been created"
          description="Add a class with its seat capacity to open admissions for that class."
          icon={<SchoolIcon className="size-6" />}
          action={
            <Link to="/admin/classes/new" className={buttonClasses()}>
              Create a class
            </Link>
          }
        />
      ) : (
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {classes.map((classData) => (
            <ClassCard key={classData.id} classData={classData} />
          ))}
        </div>
      )}
    </div>
  );
}
