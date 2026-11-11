import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createClass } from "@/lib/api/classes";
import { queryKeys } from "@/lib/query-keys";
import type { CreateClassRequest } from "@/types/api";

export function useCreateClass() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateClassRequest) => createClass(request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.classes });
    },
  });
}
