import { cn } from "@/lib/utils";

type ProgressTone = "brand" | "success" | "warning" | "danger";

interface ProgressBarProps {
  value: number;
  label: string;
  tone?: ProgressTone;
  className?: string;
}

const toneClasses: Record<ProgressTone, string> = {
  brand: "bg-brand-600",
  success: "bg-emerald-600",
  warning: "bg-amber-500",
  danger: "bg-red-600",
};

export function ProgressBar({
  value,
  label,
  tone = "brand",
  className,
}: ProgressBarProps) {
  const boundedValue = Math.min(Math.max(Math.round(value), 0), 100);

  return (
    <div
      className={cn("h-2 w-full overflow-hidden rounded-full bg-slate-200", className)}
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={boundedValue}
    >
      <div
        className={cn("h-full rounded-full transition-[width] duration-500", toneClasses[tone])}
        style={{ width: `${boundedValue}%` }}
      />
    </div>
  );
}
