import { Badge, type BadgeTone } from "@/components/ui/badge";
import type { HoldStatus } from "@/types/api";

const statusConfig: Record<
  HoldStatus,
  { label: string; tone: BadgeTone; explanation: string }
> = {
  HELD: {
    label: "Held",
    tone: "warning",
    explanation: "The seat is temporarily reserved.",
  },
  CONFIRMED: {
    label: "Confirmed",
    tone: "success",
    explanation: "The admission is confirmed.",
  },
  EXPIRED: {
    label: "Expired",
    tone: "neutral",
    explanation: "The reservation time ran out.",
  },
  CANCELLED: {
    label: "Cancelled",
    tone: "danger",
    explanation: "The reservation was cancelled.",
  },
};

export function HoldStatusBadge({ status }: { status: HoldStatus }) {
  const config = statusConfig[status];
  return <Badge tone={config.tone}>{config.label}</Badge>;
}

export function holdStatusExplanation(status: HoldStatus): string {
  return statusConfig[status].explanation;
}
