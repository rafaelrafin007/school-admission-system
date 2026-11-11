import { Link } from "react-router-dom";
import { CreateClassForm } from "@/features/admin/components/create-class-form";
import { buttonClasses } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ArrowLeftIcon, InfoIcon } from "@/components/ui/icons";
import { PageHeader } from "@/components/ui/page-header";

export function AdminCreateClassPage() {
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
        eyebrow="Administration"
        title="Create an admission class"
        description="Set the class name and the number of confirmed admissions it can accept."
        className="mt-5"
      />

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <Card className="p-6 sm:p-8">
          <CreateClassForm />
        </Card>

        <aside className="space-y-4">
          <Card className="p-5">
            <div className="flex items-center gap-2 text-brand-800">
              <InfoIcon className="size-5" />
              <h2 className="text-base font-semibold">Before you create</h2>
            </div>
            <ul className="mt-3 space-y-3 text-sm leading-6 text-slate-600">
              <li>
                Class names may duplicate. Choose a name that clearly identifies
                the section or shift.
              </li>
              <li>
                Total seats is the maximum number of confirmed admissions, not
                the number of temporary holds.
              </li>
              <li>
                New classes start with every seat available. Held and confirmed
                admissions are then reflected live as applications arrive.
              </li>
            </ul>
          </Card>
          <Card className="border-brand-100 bg-brand-50 p-5">
            <h2 className="text-base font-semibold text-brand-900">
              Need the student view?
            </h2>
            <p className="mt-2 text-sm leading-6 text-brand-800">
              Open the student portal to check the reservation experience using
              the same live inventory.
            </p>
            <Link
              to="/student/apply"
              className={buttonClasses({
                variant: "secondary",
                size: "sm",
                className: "mt-4 bg-white",
              })}
            >
              Open student portal
            </Link>
          </Card>
        </aside>
      </div>
    </div>
  );
}
