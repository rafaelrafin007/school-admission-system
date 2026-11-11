export const queryKeys = {
  classes: ["classes"] as const,
  class: (classId: string) => ["classes", classId] as const,
  hold: (holdId: string) => ["holds", holdId] as const,
  myHolds: (studentId: string) => ["holds", "me", studentId] as const,
};
