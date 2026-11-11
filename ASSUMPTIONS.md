# API assumptions

## Hold lifecycle

- Hold details are readable without `X-Student-Id`, matching the assignment's GET example.
  The response excludes student identity. Confirmation, cancellation and student history
  require the previously approved trimmed, nonempty string identifier of at most 17 characters.
- Ownership mismatches on confirmation/cancellation return a generic 403; unknown hold IDs
  return 404. Ownership is checked even when the requested confirmation is already complete.
- Repeating confirmation as the owner returns 200 with the original confirmation timestamp
  retained in persistence. Repeating cancellation returns 409, consistently with other invalid
  cancellation states; cancellation never deletes history or reverses confirmed admission.
- GET responses represent overdue HELD records as EXPIRED without persisting changes. This
  keeps reads free of mutation locks while ensuring accurate UI status between background
  expiration cycles. An owner's expired confirmation/cancellation attempt persists EXPIRED and
  commits that transition before returning 409.
- Student history is newest first by creation time, then descending hold ID for deterministic
  ordering when timestamps match. All statuses are included, using a single database statement.
- A commit-time connection failure can leave the client uncertain about success. Transactions
  remain atomic; callers can retrieve the hold or their history to reconcile its current state.
