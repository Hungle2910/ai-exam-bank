# Acceptance specification for docs/IDENTITY_AND_PERMISSIONS.md (PR #56).
# The PR changes a proposed contract only; no identity/review implementation,
# application test runner, or step definitions exist yet. These scenarios are
# pending acceptance cases, not executable tests or evidence of enforcement.
# Keep fixtures isolated per scenario when wiring these cases to the real API.
# Capability checks use an otherwise valid request and resource so authorization
# failures cannot be confused with missing resources or invalid input.

Feature: Teacher and Admin permissions and human exam review
  Teachers create exams. Admins assign scoped review permission to Teachers.
  A human reviewer may decide only another author's current exam revision
  within the assigned scope. Decisions and permission changes are audited.

  Background:
    Given Alice and Bob are Teachers
    And Ada is an Admin without additional exam business permissions
    And Alice has only the default Teacher permissions
    And Ada has granted Bob "exams.review" for Mathematics in assignment A
    And Alice owns a Mathematics exam in assignment A at revision 1
    And that exam is in "PENDING_REVIEW"

  # Contract sections 2-6: all ten matrix rows, including explicit denials.
  Scenario Outline: Role capabilities match the permission matrix
    Given the capability request concerns a valid eligible resource
    And requests to review another author's exam use its current revision within scope
    When Alice, Bob, and Ada each request the capability "<capability>" independently
    Then Alice's access is "<teacher>"
    And Bob's access is "<review_teacher>"
    And Ada's access is "<admin>"

    Examples:
      | capability                         | teacher | review_teacher | admin   |
      | Create an exam blueprint           | allowed | allowed        | denied  |
      | Request AI exam generation         | allowed | allowed        | denied  |
      | Read one's own exam                | allowed | allowed        | denied  |
      | Submit one's own draft for review  | allowed | allowed        | denied  |
      | List exams pending review          | denied  | allowed        | allowed |
      | Approve or reject an exam          | denied  | allowed        | denied  |
      | Review one's own exam              | denied  | denied         | denied  |
      | Manage user accounts               | denied  | denied         | allowed |
      | Grant or revoke review permission  | denied  | denied         | allowed |
      | View the entire system audit log   | denied  | denied         | allowed |

  Scenario Outline: Effective permissions follow the defined role grants
    When the backend resolves the effective permissions for "<actor>"
    Then the effective permissions are exactly "<permissions>"

    Examples:
      | actor | permissions                                            |
      | Alice | exams.create, exams.read.own, exams.submit              |
      | Bob   | exams.create, exams.read.own, exams.submit, exams.review |
      | Ada   | users.manage, permissions.assign, audit.view            |

  # Contract sections 3, 8, 10: direct API requests bypass any UI restrictions.
  Scenario Outline: A Teacher without review permission cannot decide an exam
    Given Alice is requesting a decision on another Teacher's pending exam
    When Alice sends a valid "<decision>" request directly to the review API
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario Outline: Review permission never allows self review
    Given Bob owns the pending exam instead of Alice
    When Bob sends a valid "<decision>" request for its current revision
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario Outline: Review permission is bounded by both subject and assignment
    Given Alice's pending exam has subject "<subject>" and assignment "<assignment>"
    When Bob sends a valid "<decision>" request for its current revision
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | subject     | assignment | decision |
      | Science     | A          | APPROVE  |
      | Science     | A          | REJECT   |
      | Mathematics | B          | APPROVE  |
      | Mathematics | B          | REJECT   |

  Scenario: A reviewer can inspect an assigned draft and its history
    When Bob opens Alice's exam for review
    Then Bob can read its content, metadata, AI questions, and answers
    And Bob can read its review history within assignment A

  Scenario: Review history access does not extend beyond the assigned scope
    Given Alice's pending exam is in Mathematics in assignment B
    When Bob directly requests that exam's review history
    Then access is denied
    And no review history for that exam is returned

  Scenario Outline: Teachers cannot assign or revoke review permission
    Given the target account is "<target>"
    When "<actor>" directly requests to "<operation>" the target's "exams.review" permission
    Then the permission change is denied
    And the target's effective permissions and review scope remain unchanged

    Examples:
      | actor | target | operation |
      | Alice | Alice  | grant     |
      | Alice | Bob    | revoke    |
      | Bob   | Alice  | grant     |
      | Bob   | Bob    | revoke    |

  Scenario Outline: Revocation takes effect for an already opened review
    Given Bob opened Alice's current exam revision while authorized
    And Ada subsequently revoked Bob's "exams.review" permission
    When Bob uses the same session to send a valid "<decision>" request
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded
    And Bob retains "exams.create", "exams.read.own", and "exams.submit"

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  # Contract sections 7-10: successful decisions, state transitions, and audit.
  Scenario: Generating and submitting an exam requires human review
    Given Alice has created a valid exam blueprint
    When AI and the system finish generating an exam for that blueprint
    Then the generated exam is in "DRAFT"
    When Alice submits that draft for review
    Then the generated exam is in "PENDING_REVIEW"
    And it is not approved by generation or submission

  Scenario Outline: AI cannot approve an exam
    Given the exam is in "<state>"
    When the AI generation process attempts to move the exam to "APPROVED"
    Then the transition is denied
    And the exam remains in "<state>"
    And no approval decision is recorded

    Examples:
      | state          |
      | DRAFT          |
      | PENDING_REVIEW |

  Scenario Outline: An authorized human decision records the reviewed revision
    Given Bob has opened Alice's current exam revision
    When Bob sends "<decision>" for revision 1 with reason "<reason>"
    Then the decision succeeds
    And the exam is in "<state>"
    And a review decision records Bob, Alice's exam ID, revision 1, and "<decision>"
    And a corresponding audit entry records:
      | field                  | expected value                |
      | actor ID               | Bob's ID                      |
      | exam ID                | Alice's exam ID               |
      | exam revision          | 1                             |
      | action                 | <decision>                    |
      | timestamp              | the decision time             |
      | request/correlation ID | this decision request's ID    |
    And Alice can see the outcome "<state>"

    Examples:
      | decision | reason                       | state    |
      | APPROVE  |                              | APPROVED |
      | REJECT   | The answer key is incorrect. | REJECTED |

  Scenario: Rejection preserves the reason in the decision and audit
    When Bob rejects revision 1 with reason "Đáp án câu 2 chưa đúng."
    Then the exam is in "REJECTED"
    And the review decision stores "Đáp án câu 2 chưa đúng."
    And the corresponding audit entry stores "Đáp án câu 2 chưa đúng."

  Scenario Outline: Rejection requires a reason
    When Bob rejects revision 1 with <reason_input>
    Then the response status is 400
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | reason_input                         |
      | the reason omitted from the request  |
      | an empty reason string               |
      | a reason containing only spaces      |

  Scenario Outline: An exam changed after opening cannot be decided from a stale view
    Given Bob opened Alice's exam at revision 1
    And Alice's exam has since changed to revision 2 in "PENDING_REVIEW"
    When Bob sends a valid "<decision>" request identifying revision 1
    Then the response status is 409
    And revision 2 remains in "PENDING_REVIEW"
    And no review decision is recorded for either revision

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario: Reloading after an edit permits a decision on the current revision
    Given Bob opened Alice's exam at revision 1
    And Alice's exam has since changed to revision 2 in "PENDING_REVIEW"
    When Bob reloads the exam and approves revision 2
    Then the exam is in "APPROVED"
    And the review decision and its audit entry both identify revision 2
    And no approval decision is recorded for revision 1

  Scenario Outline: A rejected exam returns through draft and submission
    Given Bob rejected Alice's exam with a reason
    When Alice "<revision_action>" the rejected exam
    Then the exam returns to "DRAFT"
    When Alice submits the revised draft for review
    Then the exam is in "PENDING_REVIEW"
    And it requires a new human decision before becoming "APPROVED"

    Examples:
      | revision_action       |
      | edits                 |
      | requests AI to remake |

  Scenario Outline: Admin permission changes are effective and audited
    Given Alice's review permission is initially "<initial_permission>"
    When Ada "<operation>" Alice's "exams.review" permission for Mathematics in assignment A
    Then Alice's review permission is "<resulting_permission>"
    And Alice retains her default Teacher permissions
    And an audit entry records:
      | field            | expected value                      |
      | admin actor      | Ada's ID                            |
      | affected Teacher | Alice's ID                          |
      | permission       | exams.review                        |
      | operation        | <operation>                         |
      | review scope     | Mathematics in assignment A         |
      | timestamp        | the permission change time          |
    And Ada can view the permission change history

    Examples:
      | initial_permission | operation | resulting_permission |
      | absent             | grants    | present              |
      | present            | revokes   | absent               |
