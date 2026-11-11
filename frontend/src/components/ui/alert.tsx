import type { ReactNode } from "react";
import {
  AlertTriangleIcon,
  CheckCircleIcon,
  InfoIcon,
} from "@/components/ui/icons";
import { cn } from "@/lib/utils";

type AlertTone = "info" | "success" | "warning" | "danger";

interface AlertProps {
  tone?: AlertTone;
  title?: string;
  children: ReactNode;
  className?: string;
  action?: ReactNode;
}

const toneClasses: Record<AlertTone, string> = {
  info: "border-sky-200 bg-sky-50 text-sky-900",
  success: "border-emerald-200 bg-emerald-50 text-emerald-900",
  warning: "border-amber-200 bg-amber-50 text-amber-950",
  danger: "border-red-200 bg-red-50 text-red-950",
};

const iconClasses: Record<AlertTone, string> = {
  info: "text-sky-700",
  success: "text-emerald-700",
  warning: "text-amber-700",
  danger: "text-red-700",
};

export function Alert({
  tone = "info",
  title,
  children,
  className,
  action,
}: AlertProps) {
  const Icon =
    tone === "success"
      ? CheckCircleIcon
      : tone === "danger" || tone === "warning"
        ? AlertTriangleIcon
        : InfoIcon;

  return (
    <div
      className={cn(
        "flex gap-3 rounded-lg border p-3.5 text-sm",
        toneClasses[tone],
        className,
      )}
      role={tone === "danger" ? "alert" : "status"}
    >
      <Icon className={cn("mt-0.5 size-5 shrink-0", iconClasses[tone])} />
      <div className="min-w-0 flex-1">
        {title ? <p className="font-semibold">{title}</p> : null}
        <div className={cn(title && "mt-1", "leading-6")}>{children}</div>
      </div>
      {action ? <div className="shrink-0 self-center">{action}</div> : null}
    </div>
  );
}
