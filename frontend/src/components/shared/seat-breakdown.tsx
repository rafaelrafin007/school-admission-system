import { ProgressBar } from "@/components/ui/progress";
import { cn, formatNumber, usedSeats, utilisationPercentage } from "@/lib/utils";
import type { ClassResponse } from "@/types/api";

interface SeatBreakdownProps {
  classData: ClassResponse;
  className?: string;
  compact?: boolean;
}

export function SeatBreakdown({
  classData,
  className,
  compact = false,
}: SeatBreakdownProps) {
  const used = usedSeats(classData);
  const percentage = utilisationPercentage(used, classData.totalSeats);

  return (
    <div className={cn("space-y-3", className)}>
      <div className="flex items-center justify-between gap-4 text-sm">
        <span className="font-medium text-slate-700">
          {formatNumber(classData.availableSeats)} available
        </span>
        <span className="text-slate-500">
          {formatNumber(used)} of {formatNumber(classData.totalSeats)} seats used
        </span>
      </div>
      <ProgressBar
        value={percentage}
        label={`${percentage}% of seats used`}
        tone={
          classData.availableSeats === 0
            ? "danger"
            : percentage >= 80
              ? "warning"
              : "brand"
        }
      />
      {compact ? null : (
        <dl className="grid grid-cols-3 gap-2 rounded-lg bg-slate-50 p-3 text-center">
          <div>
            <dt className="text-xs font-medium text-slate-500">Available</dt>
            <dd className="mt-1 text-base font-semibold text-slate-900">
              {formatNumber(classData.availableSeats)}
            </dd>
          </div>
          <div>
            <dt className="text-xs font-medium text-slate-500">Held</dt>
            <dd className="mt-1 text-base font-semibold text-slate-900">
              {formatNumber(classData.heldSeats)}
            </dd>
          </div>
          <div>
            <dt className="text-xs font-medium text-slate-500">Confirmed</dt>
            <dd className="mt-1 text-base font-semibold text-slate-900">
              {formatNumber(classData.confirmedSeats)}
            </dd>
          </div>
        </dl>
      )}
    </div>
  );
}
