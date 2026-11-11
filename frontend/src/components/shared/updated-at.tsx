import { formatTime } from "@/lib/utils";

interface UpdatedAtProps {
  updatedAt: number;
  isFetching?: boolean;
}

export function UpdatedAt({ updatedAt, isFetching = false }: UpdatedAtProps) {
  if (!updatedAt) return null;

  return (
    <span className="text-xs text-slate-500" aria-live="polite">
      {isFetching ? "Refreshing…" : `Updated ${formatTime(updatedAt)}`}
    </span>
  );
}
