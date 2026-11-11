import { Badge } from "@/components/ui/badge";
import type { ClassResponse } from "@/types/api";

export function ClassAvailabilityBadge({ availableSeats }: { availableSeats: number }) {
  return availableSeats > 0 ? (
    <Badge tone="success">Seats available</Badge>
  ) : (
    <Badge tone="danger">Full</Badge>
  );
}

export function ClassAvailability({
  classData,
  className,
}: {
  classData: ClassResponse;
  className?: string;
}) {
  const { availableSeats, totalSeats } = classData;

  if (availableSeats <= 0) {
    return <span className={className}>All {totalSeats} seats are taken</span>;
  }

  return (
    <span className={className}>
      {availableSeats} of {totalSeats} seats available
    </span>
  );
}
