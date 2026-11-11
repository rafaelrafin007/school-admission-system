import { useState, type ReactNode } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { StudentIdentityProvider } from "@/features/student/hooks/use-student-identity";
import { createQueryClient } from "@/lib/query-client";

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <StudentIdentityProvider>{children}</StudentIdentityProvider>
    </QueryClientProvider>
  );
}
