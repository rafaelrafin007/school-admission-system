import { Link } from "react-router-dom";
import { HoldStatusBadge, holdStatusExplanation } from "@/components/shared/hold-status-badge";
import { Card } from "@/components/ui/card";
import { ChevronRightIcon } from "@/components/ui/icons";
import { useCountdown } from "@/hooks/use-countdown";
import { formatDateTime } from "@/lib/utils";
import type { MyHoldResponse } from "@/types/api";

export function ApplicationCard({ hold }: { hold: MyHoldResponse }) {
  const countdown = useCountdown(hold.expiresAt);
  const isActive = hold.status === "HELD" && !countdown.isExpired;

  return (
    <Card className="flex flex-col p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="break-words text-lg font-semibold text-slate-950">
            {hold.className}
          </h3>
          <p className="mt-1 break-all text-xs text-slate-500">
            {hold.holdId}
          </p>
        </div>
        <HoldStatusBadge status={hold.status} />
      </div>

      <div className="mt-4 flex-1 space-y-3 text-sm">
        <p className="leading-6 text-slate-600">
          {holdStatusExplanation(hold.status)}
        </p>
        {isActive ? (
          <div className="rounded-lg bg-amber-50 px-3 py-2.5 text-amber-900">
            <p className="text-xs font-medium">Time remaining</p>
            <p
              className="mt-0.5 text-lg font-bold tabular-nums"
              role="timer"
            >
              {countdown.label}
            </p>
          </div>
        ) : (
          <p className="text-xs text-slate-500">
            {hold.status === "HELD"
              ? "Reservation expires at "
              : "Reservation deadline "}
            <time dateTime={hold.expiresAt}>
              {formatDateTime(hold.expiresAt)}
            </time>
          </p>
        )}
      </div>

      <Link
        to={`/student/applications/${encodeURIComponent(hold.holdId)}`}
        className="mt-5 inline-flex items-center gap-1 text-sm font-semibold text-brand-800 hover:text-brand-900"
      >
        View application
        <ChevronRightIcon className="size-4" />
      </Link>
    </Card>
  );
}
