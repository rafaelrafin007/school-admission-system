import type { ReactElement } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { StudentIdentityProvider } from "@/features/student/hooks/use-student-identity";

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export function renderWithProviders(
  ui: ReactElement,
  {
    route = "/",
    queryClient = createTestQueryClient(),
  }: {
    route?: string;
    queryClient?: QueryClient;
  } = {},
) {
  const user = userEvent.setup();

  const result = render(
    <QueryClientProvider client={queryClient}>
      <StudentIdentityProvider>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </StudentIdentityProvider>
    </QueryClientProvider>,
  );

  return { ...result, user, queryClient };
}
