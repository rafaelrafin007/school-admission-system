import { useQuery } from "@tanstack/react-query";
import { getClass, getClasses } from "@/lib/api/classes";
import { queryKeys } from "@/lib/query-keys";

const POLL_INTERVAL = 5_000;

export function useClasses() {
  return useQuery({
    queryKey: queryKeys.classes,
    queryFn: ({ signal }) => getClasses(signal),
    refetchInterval: POLL_INTERVAL,
    refetchIntervalInBackground: false,
  });
}

export function useClass(classId: string) {
  return useQuery({
    queryKey: queryKeys.class(classId),
    queryFn: ({ signal }) => getClass(classId, signal),
    enabled: Boolean(classId),
    refetchInterval: POLL_INTERVAL,
    refetchIntervalInBackground: false,
  });
}
