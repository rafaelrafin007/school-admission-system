import { Link } from "react-router-dom";
import { ClassAvailabilityBadge } from "@/components/shared/class-availability";
import { HoldStatusBadge } from "@/components/shared/hold-status-badge";
import { SeatBreakdown } from "@/components/shared/seat-breakdown";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ChevronRightIcon } from "@/components/ui/icons";
import type { ClassResponse, MyHoldResponse } from "@/types/api";

interface ReservationCardProps {
  classData: ClassResponse;
  activeHold?: MyHoldResponse;
  isReserving: boolean;
  reservationsReady: boolean;
  anyMutationPending: boolean;
  onReserve: (classId: string) => void;
}

export function ReservationCard({
  classData,
  activeHold,
  isReserving,
  reservationsReady,
  anyMutationPending,
  onReserve,
}: ReservationCardProps) {
  const isFull = classData.availableSeats <= 0;

  return (
    <Card className="flex h-full flex-col p-5">
      <div className="flex items-start justify-between gap-3">
        <h3 className="text-lg font-semibold text-slate-950">
          {classData.name}
        </h3>
        <ClassAvailabilityBadge availableSeats={classData.availableSeats} />
      </div>

      <SeatBreakdown classData={classData} compact className="mt-5 flex-1" />

      {activeHold ? (
        <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-3">
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-medium text-slate-700">
              Your application
            </span>
            <HoldStatusBadge status={activeHold.status} />
          </div>
          <Link
            to={`/student/applications/${encodeURIComponent(activeHold.holdId)}`}
            className="mt-2 inline-flex items-center gap-1 text-sm font-semibold text-brand-800 hover:text-brand-900"
          >
            {activeHold.status === "HELD"
              ? "View reservation"
              : "View confirmed admission"}
            <ChevronRightIcon className="size-4" />
          </Link>
        </div>
      ) : (
        <Button
          type="button"
          className="mt-5 w-full"
          disabled={isFull || !reservationsReady || anyMutationPending}
          loading={isReserving}
          onClick={() => onReserve(classData.id)}
        >
          {isFull ? "Class full" : "Reserve a seat"}
        </Button>
      )}
    </Card>
  );
}
