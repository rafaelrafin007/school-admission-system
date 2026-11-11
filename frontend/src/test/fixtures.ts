import type {
  ClassResponse,
  HoldDetailsResponse,
  MyHoldResponse,
} from "@/types/api";

export function futureIso(milliseconds = 10 * 60 * 1000): string {
  return new Date(Date.now() + milliseconds).toISOString();
}

export function makeClass(overrides: Partial<ClassResponse> = {}): ClassResponse {
  return {
    id: "class-1",
    name: "Class 6 – Morning",
    totalSeats: 40,
    heldSeats: 3,
    confirmedSeats: 10,
    availableSeats: 27,
    ...overrides,
  };
}

export function makeMyHold(
  overrides: Partial<MyHoldResponse> = {},
): MyHoldResponse {
  return {
    holdId: "hold-1",
    classId: "class-1",
    className: "Class 6 – Morning",
    status: "HELD",
    expiresAt: futureIso(),
    ...overrides,
  };
}

export function makeHoldDetails(
  overrides: Partial<HoldDetailsResponse> = {},
): HoldDetailsResponse {
  return {
    holdId: "hold-1",
    classId: "class-1",
    status: "HELD",
    expiresAt: futureIso(),
    ...overrides,
  };
}
