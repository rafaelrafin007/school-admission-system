let offsetMilliseconds = 0;

export function syncServerClock(dateHeader: string | undefined): void {
  if (!dateHeader) return;

  const serverTime = Date.parse(dateHeader);
  if (!Number.isFinite(serverTime)) return;

  offsetMilliseconds = serverTime - Date.now();
}

export function getServerNow(): number {
  return Date.now() + offsetMilliseconds;
}
