# Acceptance specification for docs/IDENTITY_AND_PERMISSIONS.md (PR #56).
# The PR changes a proposed contract only; no identity/review implementation,
# application test runner, or step definitions exist yet. These scenarios are
# pending acceptance cases, not executable tests or evidence of enforcement.

Feature: Teacher, DepartmentHead and Admin permissions for human exam review
  Teachers request AI-generated exams. DepartmentHeads review exams in their
  assigned departments. Admins manage accounts, roles, and review scopes.
  AI cannot approve an exam, and all decisions and role changes are audited.

  Background:
    Given Alice is a Teacher
    And Hanh is the DepartmentHead for Mathematics in assignment A
    And Ada is an Admin without exam business permissions
    And Alice owns a Mathematics exam in assignment A at revision 1
    And that exam is in "PENDING_REVIEW"

  Scenario Outline: Role capabilities match the permission matrix
    Given the capability request concerns a valid eligible resource
    And review requests use another author's current exam revision within scope
    When Alice, Hanh, and Ada each request the capability "<capability>" independently
    Then Alice's access is "<teacher>"
    And Hanh's access is "<department_head>"
    And Ada's access is "<admin>"

    Examples:
      | capability                              | teacher | department_head | admin   |
      | Create an exam blueprint                | allowed | denied          | denied  |
      | Request AI exam generation              | allowed | denied          | denied  |
      | Read one's own requested exam           | allowed | denied          | denied  |
      | Submit one's own draft for review       | allowed | denied          | denied  |
      | List exams pending review within scope  | denied  | allowed         | allowed |
      | Read exam answers for review             | denied  | allowed         | denied  |
      | Approve or reject an exam               | denied  | allowed         | denied  |
      | Review one's own exam                   | denied  | denied          | denied  |
      | Manage user accounts and roles          | denied  | denied          | allowed |
      | Assign a department review scope        | denied  | denied          | allowed |
      | View the entire system audit log        | denied  | denied          | allowed |

  Scenario Outline: Effective permissions follow the defined roles
    When the backend resolves the effective permissions for "<actor>"
    Then the effective permissions are exactly "<permissions>"

    Examples:
      | actor | permissions                                                        |
      | Alice | exams.create, exams.read.own, exams.submit                          |
      | Hanh  | exams.review.list, exams.review.read, exams.review.decide            |
      | Ada   | users.manage, roles.assign, review-scopes.assign, audit.view         |

  Scenario Outline: A Teacher cannot decide an exam
    Given Alice is requesting a decision on another Teacher's pending exam
    When Alice sends a valid "<decision>" request directly to the review API
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario Outline: A DepartmentHead can review only within the assigned scope
    Given Alice's pending exam has subject "<subject>" and assignment "<assignment>"
    When Hanh sends a valid "<decision>" request for its current revision
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | subject     | assignment | decision |
      | Science     | A          | APPROVE  |
      | Science     | A          | REJECT   |
      | Mathematics | B          | APPROVE  |
      | Mathematics | B          | REJECT   |

  Scenario: A DepartmentHead can inspect an assigned draft and its history
    When Hanh opens Alice's exam for review
    Then Hanh can read its blueprint, questions, answers, and AI metadata
    And Hanh can read its review history within assignment A

  Scenario: Review history access does not extend beyond the assigned scope
    Given Alice's pending exam is in Mathematics in assignment B
    When Hanh directly requests that exam's review history
    Then access is denied
    And no review history for that exam is returned

  Scenario Outline: Non-Admin roles cannot assign roles or review scopes
    When "<actor>" directly requests to "<operation>"
    Then the administration request is denied
    And all role assignments and review scopes remain unchanged

    Examples:
      | actor | operation                                      |
      | Alice | assign the DepartmentHead role                 |
      | Alice | change a DepartmentHead review scope           |
      | Hanh  | assign the DepartmentHead role to another user |
      | Hanh  | expand Hanh's own review scope                 |

  Scenario Outline: Revoking the DepartmentHead role stops an opened review
    Given Hanh opened Alice's current exam revision while authorized
    And Ada subsequently revoked Hanh's DepartmentHead role
    When Hanh uses the same session to send a valid "<decision>" request
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario Outline: Changing scope takes effect for an opened review
    Given Hanh opened Alice's current Mathematics exam while authorized
    And Ada subsequently changed Hanh's scope from Mathematics to Science
    When Hanh uses the same session to send a valid "<decision>" request
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

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

  Scenario Outline: An authorized DepartmentHead decision records the revision
    Given Hanh has opened Alice's current exam revision
    When Hanh sends "<decision>" for revision 1 with reason "<reason>"
    Then the decision succeeds
    And the exam is in "<state>"
    And a review decision records Hanh, Alice's exam ID, revision 1, and "<decision>"
    And a corresponding audit entry records:
      | field                  | expected value                  |
      | actor ID               | Hanh's ID                       |
      | actor role             | DepartmentHead                  |
      | review scope           | Mathematics in assignment A     |
      | exam ID                | Alice's exam ID                 |
      | exam revision          | 1                               |
      | action                 | <decision>                      |
      | timestamp              | the decision time               |
      | request/correlation ID | this decision request's ID      |
    And Alice can see the outcome "<state>"

    Examples:
      | decision | reason                                     | state    |
      | APPROVE  |                                            | APPROVED |
      | REJECT   | The difficulty matrix is not satisfied.    | REJECTED |

  Scenario Outline: Rejection requires a reason
    When Hanh rejects revision 1 with <reason_input>
    Then the response status is 400
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

    Examples:
      | reason_input                        |
      | the reason omitted from the request |
      | an empty reason string              |
      | a reason containing only spaces     |

  Scenario Outline: A stale exam revision cannot be decided
    Given Hanh opened Alice's exam at revision 1
    And Alice's exam has since changed to revision 2 in "PENDING_REVIEW"
    When Hanh sends a valid "<decision>" request identifying revision 1
    Then the response status is 409
    And revision 2 remains in "PENDING_REVIEW"
    And no review decision is recorded for either revision

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario: A user with Teacher and DepartmentHead roles cannot self review
    Given Hanh also has the Teacher role
    And Hanh owns a Mathematics exam in assignment A at revision 1
    And that exam is in "PENDING_REVIEW"
    When Hanh sends a valid approval request for revision 1
    Then the response status is 403
    And the exam remains in "PENDING_REVIEW"
    And no review decision is recorded

  Scenario Outline: A rejected exam requires a new draft and review decision
    Given Hanh rejected Alice's exam with a reason
    When Alice "<revision_action>" the rejected exam
    Then the exam returns to "DRAFT"
    When Alice submits the revised draft for review
    Then the exam is in "PENDING_REVIEW"
    And it requires a new DepartmentHead decision before becoming "APPROVED"

    Examples:
      | revision_action       |
      | edits                 |
      | requests AI to remake |

  Scenario Outline: Admin role and scope changes are effective and audited
    Given Hanh's DepartmentHead role is initially "<initial_role>"
    When Ada "<operation>" Hanh's DepartmentHead role for Mathematics in assignment A
    Then Hanh's DepartmentHead role is "<resulting_role>"
    And an audit entry records:
      | field            | expected value                      |
      | admin actor      | Ada's ID                            |
      | affected account | Hanh's ID                           |
      | role             | DepartmentHead                      |
      | operation        | <operation>                         |
      | review scope     | Mathematics in assignment A         |
      | timestamp        | the role change time                |
    And Ada can view the role change history

    Examples:
      | initial_role | operation | resulting_role |
      | absent       | assigns   | present        |
      | present      | revokes   | absent         |
