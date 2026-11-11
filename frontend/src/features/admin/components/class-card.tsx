import { Link } from "react-router-dom";
import { ClassAvailabilityBadge } from "@/components/shared/class-availability";
import { SeatBreakdown } from "@/components/shared/seat-breakdown";
import { Card } from "@/components/ui/card";
import { ChevronRightIcon } from "@/components/ui/icons";
import type { ClassResponse } from "@/types/api";

export function ClassCard({ classData }: { classData: ClassResponse }) {
  return (
    <Card className="flex h-full flex-col p-5 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-3">
        <h3 className="text-lg font-semibold text-slate-950">
          {classData.name}
        </h3>
        <ClassAvailabilityBadge availableSeats={classData.availableSeats} />
      </div>
      <SeatBreakdown classData={classData} compact className="mt-5 flex-1" />
      <Link
        to={`/admin/classes/${encodeURIComponent(classData.id)}`}
        className="mt-5 inline-flex items-center gap-1 text-sm font-semibold text-brand-800 hover:text-brand-900"
      >
        View class details
        <ChevronRightIcon className="size-4" />
      </Link>
    </Card>
  );
}
