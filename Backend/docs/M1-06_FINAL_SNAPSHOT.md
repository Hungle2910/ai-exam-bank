# School exam snapshot contract

Refs #26. The Exams module validates snapshot content against the stored matrix and pinned question revisions. It checks the total count, each slot's count and score, and that question IDs are unique across slots. It reads each pinned revision from `IApprovedQuestionReader` and checks school, department, subject, topic and difficulty. The result owns a copy of the stem, options and answer and preserves the selected item order.

`FinalExamSnapshotBuilder` is for server-side use within the transaction that reads the authorized selection decision and persists the snapshot. Inputs must come from the selected exam revision. The contract does not use the mutable question head or accept new question content from the client.

The next integration must read the event's selected revision and confirm the DepartmentHead decision under the same transaction as approval rechecks and persistence. The durable snapshot must have a unique identity tied to that selected revision so a repeated finalize request returns the existing snapshot. Keep the stored answer key inside the trusted boundary. A preview endpoint must project a response appropriate to the current actor and distinguish provisional content from persisted final content.

The school selection and audit contract is still pending. The Ministry path in ADR 0004 needs a separate event scope and an independent confirmer. Endpoint registration, SQL snapshot persistence, role-specific preview and staging evidence wait for those contracts and M1-05, M3-05 and M4-03.
