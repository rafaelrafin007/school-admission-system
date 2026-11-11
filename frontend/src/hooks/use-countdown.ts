import { useEffect, useState } from "react";
import { getServerNow } from "@/lib/server-clock";
import { formatCountdown } from "@/lib/utils";

export interface CountdownValue {
  remainingMilliseconds: number;
  label: string;
  isExpired: boolean;
  isValid: boolean;
}

export function useCountdown(expiresAt: string | undefined): CountdownValue {
  const target = expiresAt ? Date.parse(expiresAt) : Number.NaN;
  const isValid = Number.isFinite(target);
  const [now, setNow] = useState(() => getServerNow());

  useEffect(() => {
    setNow(getServerNow());

    if (!isValid) return;

    const interval = window.setInterval(() => {
      setNow(getServerNow());
    }, 1_000);

    return () => window.clearInterval(interval);
  }, [isValid, target]);

  const remainingMilliseconds = isValid
    ? Math.max(0, target - now)
    : 0;

  return {
    remainingMilliseconds,
    label: isValid ? formatCountdown(remainingMilliseconds) : "Unavailable",
    isExpired: !isValid || remainingMilliseconds <= 0,
    isValid,
  };
}
