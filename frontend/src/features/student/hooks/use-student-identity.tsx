import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";

export const STUDENT_ID_STORAGE_KEY = "school-admission.student-id";
export const LAST_HOLD_STORAGE_KEY = "school-admission.last-hold-id";

interface StoredLastHold {
  studentId: string;
  holdId: string;
}

interface StudentIdentityState {
  studentId: string | null;
  lastHoldId: string | null;
}

interface StudentIdentityContextValue extends StudentIdentityState {
  setStudentId: (studentId: string) => void;
  clearStudentIdentity: () => void;
  rememberLastHold: (holdId: string) => void;
  clearLastHold: () => void;
}

const StudentIdentityContext =
  createContext<StudentIdentityContextValue | null>(null);

export function StudentIdentityProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<StudentIdentityState>(() => {
    const studentId = readStoredStudentId();
    return {
      studentId,
      lastHoldId: readStoredLastHold(studentId),
    };
  });

  const setStudentId = useCallback(
    (value: string) => {
      const studentId = value.trim();
      if (!studentId) return;

      const previousStudentId = state.studentId;
      if (previousStudentId && previousStudentId !== studentId) {
        queryClient.removeQueries({
          queryKey: queryKeys.myHolds(previousStudentId),
        });
        removeStorageItem(LAST_HOLD_STORAGE_KEY);
      }

      writeStorageItem(STUDENT_ID_STORAGE_KEY, studentId);
      setState({
        studentId,
        lastHoldId:
          previousStudentId === studentId ? state.lastHoldId : null,
      });
    },
    [queryClient, state.lastHoldId, state.studentId],
  );

  const clearStudentIdentity = useCallback(() => {
    if (state.studentId) {
      queryClient.removeQueries({
        queryKey: queryKeys.myHolds(state.studentId),
      });
    }

    removeStorageItem(STUDENT_ID_STORAGE_KEY);
    removeStorageItem(LAST_HOLD_STORAGE_KEY);
    setState({ studentId: null, lastHoldId: null });
  }, [queryClient, state.studentId]);

  const rememberLastHold = useCallback(
    (holdId: string) => {
      if (!state.studentId) return;

      const stored: StoredLastHold = {
        studentId: state.studentId,
        holdId,
      };
      writeStorageItem(LAST_HOLD_STORAGE_KEY, JSON.stringify(stored));
      setState((current) => ({ ...current, lastHoldId: holdId }));
    },
    [state.studentId],
  );

  const clearLastHold = useCallback(() => {
    removeStorageItem(LAST_HOLD_STORAGE_KEY);
    setState((current) => ({ ...current, lastHoldId: null }));
  }, []);

  const value = useMemo<StudentIdentityContextValue>(
    () => ({
      ...state,
      setStudentId,
      clearStudentIdentity,
      rememberLastHold,
      clearLastHold,
    }),
    [
      clearLastHold,
      clearStudentIdentity,
      rememberLastHold,
      setStudentId,
      state,
    ],
  );

  return (
    <StudentIdentityContext.Provider value={value}>
      {children}
    </StudentIdentityContext.Provider>
  );
}

export function useStudentIdentity(): StudentIdentityContextValue {
  const context = useContext(StudentIdentityContext);
  if (!context) {
    throw new Error(
      "useStudentIdentity must be used inside StudentIdentityProvider.",
    );
  }

  return context;
}

function readStoredStudentId(): string | null {
  try {
    const stored = window.localStorage.getItem(STUDENT_ID_STORAGE_KEY)?.trim();
    if (!stored || stored.length > 17) {
      window.localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
      return null;
    }

    return stored;
  } catch {
    return null;
  }
}

function readStoredLastHold(studentId: string | null): string | null {
  if (!studentId) return null;

  try {
    const stored = window.localStorage.getItem(LAST_HOLD_STORAGE_KEY);
    if (!stored) return null;

    const parsed = JSON.parse(stored) as Partial<StoredLastHold>;
    if (
      parsed.studentId === studentId &&
      typeof parsed.holdId === "string" &&
      parsed.holdId.trim()
    ) {
      return parsed.holdId;
    }

    return null;
  } catch {
    return null;
  }
}

function writeStorageItem(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    return;
  }
}

function removeStorageItem(key: string): void {
  try {
    window.localStorage.removeItem(key);
  } catch {
    return;
  }
}
