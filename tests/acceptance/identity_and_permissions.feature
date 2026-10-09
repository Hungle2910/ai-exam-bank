# MVP acceptance cases for docs/IDENTITY_AND_PERMISSIONS.md.
# No application runner or step definitions exist yet. These are reviewable
# scenarios, not passing product tests or evidence of enforcement.

Feature: Ministry, school, department and teacher authorization
  Teachers prepare school exams manually or with AI assistance.
  DepartmentHeads choose exams in assigned school departments.
  SchoolAdmins manage their schools without ministry involvement.
  MinistryAdmins manage the system and independently prepare and confirm
  assigned ministry-level exams.

  Background:
    Given Alice is a Teacher at School A in Mathematics
    And Hanh is a DepartmentHead for Mathematics at School A
    And Sam is a SchoolAdmin for School A
    And Mai and Minh are MinistryAdmins assigned to national exam N

  Scenario: A Teacher prepares and submits a school exam
    When Alice creates a Mathematics blueprint for School A
    And AI suggests a question for the draft
    And Alice checks its content, answer, difficulty, and source
    And Alice accepts, edits, or discards that suggestion
    Then the exam is a draft owned by Alice in School A
    And AI cannot approve the draft
    And Alice's choice does not approve a shared question-bank revision
    When Alice submits the draft for review
    Then that revision is fixed and pending selection by the assigned DepartmentHead

  Scenario: A DepartmentHead selects an exam only after its questions are approved
    Given Alice submitted a school exam containing a Teacher-checked AI suggestion
    And an independent reviewer approved every QuestionRevision in that exam
    When Hanh checks the blueprint and full exam and selects Alice's current revision
    Then the exam revision is selected
    And the selection decision does not change any QuestionRevision approval state

  Scenario: A Teacher-checked AI suggestion cannot enter the final exam unapproved
    Given Alice accepted an AI suggestion in her school exam draft
    And its QuestionRevision is still pending review
    When Hanh attempts to select or finalize that exam revision
    Then the request is rejected with a state conflict
    And the event's selected revision remains unchanged

  Scenario: A Teacher cannot inspect another school's draft
    Given another Teacher owns a draft at School B
    When Alice directly requests that draft or its answer key
    Then the response status is 403
    And no draft content or answer key is returned

  Scenario: Editing after submission creates a new version
    Given Alice submitted revision 1 for selection
    When Alice edits the exam again
    Then the submitted revision 1 is unchanged
    And Alice's edits belong to a new draft revision
    When Alice submits revision 2 for the same test
    Then revision 1 is retained in the history as "SUPERSEDED"
    And only revision 2 remains an eligible candidate for selection

  Scenario: Submitting a revision does not replace an already selected exam
    Given Hanh selected Alice's revision 1 for a School A test event
    When Alice submits revision 2 for the same test event
    Then revision 1 remains unchanged and selected for that event
    And revision 2 is pending selection
    And the selected revision changes only after an explicit DepartmentHead decision

  Scenario Outline: Submitting a new revision preserves a terminal decision
    Given Alice's revision 1 has status "<status>" for a School A test event
    When Alice submits revision 2 for the same event
    Then revision 1 remains "<status>" in history
    And only revision 2 is pending selection

    Examples:
      | status       |
      | NOT_SELECTED |
      | REJECTED     |

  Scenario: A DepartmentHead chooses one of several school exams
    Given Alice and another Teacher submitted exams for the same School A Mathematics grade 9 test event
    When Hanh compares the submitted exams and chooses Alice's current revision
    Then only Alice's revision is selected for that test
    And the other exam is "NOT_SELECTED"
    And "NOT_SELECTED" does not assert whether that exam is valid
    And the other exam remains in the history
    And a ReviewDecision and AuditEvent identify Hanh and the selected revision

  Scenario: Concurrent selections for one event cannot both succeed
    Given two DepartmentHeads can select different approved exam revisions for the same School A event
    When they submit their selections concurrently using the same event version
    Then exactly one selection succeeds
    And the other receives a 409 conflict
    And the event points to exactly one selected revision
    And the decision and audit records match that selected revision

  Scenario: No submitted school exam meets the requirements
    Given Alice and another Teacher submitted exams for the same School A Mathematics grade 9 test event
    And neither exam meets the blueprint requirements
    When Hanh returns both exams with a reason for each
    Then both submitted revisions are "REJECTED"
    And no exam is selected for that test
    And each Teacher can create and submit a new revision

  Scenario: A DepartmentHead reviews a valid school exam
    Given Alice's School A Mathematics exam is pending review at revision 1
    When Hanh selects revision 1
    Then the exam is selected at revision 1
    And a ReviewDecision and AuditEvent identify Hanh and revision 1

  Scenario Outline: A DepartmentHead cannot cross school or department scope
    Given an exam is pending at "<school>" in "<department>"
    When Hanh directly requests its content or selection
    Then the response status is 403
    And no review decision is recorded

    Examples:
      | school   | department |
      | School B | Mathematics |
      | School A | Science     |

  Scenario: A Teacher cannot select an exam by calling the API
    Given a school exam is pending review
    When Alice directly sends a selection request
    Then the response status is 403
    And the exam remains pending review

  Scenario: A Teacher who is also DepartmentHead cannot select their own exam
    Given Alice is also a DepartmentHead for Mathematics at School A
    And Alice submitted her own exam revision for selection
    When Alice attempts to select that revision
    Then the response status is 403
    And no ReviewDecision is recorded
    And the event's selected revision remains unchanged

  Scenario: A SchoolAdmin cannot manage another school's users
    When Sam directly attempts to change a School B user's role
    Then the response status is 403
    And that user's role assignments remain unchanged

  Scenario: A SchoolAdmin cannot grant the MinistryAdmin role
    When Sam directly attempts to grant MinistryAdmin to a School A user
    Then the response status is 403
    And that user's role assignments remain unchanged

  Scenario: A SchoolAdmin cannot select an exam
    Given a School A exam is pending selection
    When Sam directly attempts to select it
    Then the response status is 403
    And the exam remains pending selection

  Scenario: A MinistryAdmin does not participate in school exam selection
    Given Mai has no School A business role
    And a School A exam is pending selection
    When Mai directly requests its content or attempts to select it
    Then the response status is 403
    And no school exam content or review decision is returned

  Scenario: A MinistryAdmin drafts a national exam for an assigned event
    When Mai manually prepares revision 1 of national exam N
    Then the exam is a draft
    And Mai may submit it for independent confirmation
    And AI cannot approve it

  Scenario: A second MinistryAdmin confirms the current national exam
    Given Mai authored revision 1 of national exam N
    And revision 1 is pending review
    When Minh confirms revision 1
    Then revision 1 is approved by Minh
    And the decision and audit record both identify the event and revision
    And the finalized exam snapshot is pinned to the approved revision

  Scenario: A MinistryAdmin cannot confirm their own national exam
    Given Mai authored revision 1 of national exam N
    And revision 1 is pending review
    When Mai tries to confirm revision 1
    Then the response status is 403
    And the exam remains pending review

  Scenario: An unassigned MinistryAdmin cannot confirm a national exam
    Given Mai authored revision 1 of national exam N
    And revision 1 is pending review
    And another MinistryAdmin is not assigned to national exam N
    When that unassigned MinistryAdmin attempts to confirm revision 1
    Then the response status is 403
    And revision 1 remains pending review
    And no ReviewDecision is recorded

  Scenario: A MinistryAdmin cannot read an unassigned national exam
    Given Mai is not assigned to national exam M
    When Mai directly requests the content of national exam M
    Then the response status is 403
    And no exam content is returned

  Scenario: A school role cannot access a confidential national exam
    Given national exam N is not released to schools
    When Sam directly requests its content
    Then the response status is 403
    And no exam content is returned

  Scenario: A stale revision cannot be selected
    Given Hanh opened Alice's pending exam at revision 1
    And Alice's exam changed to revision 2
    When Hanh attempts to select revision 1
    Then the response status is 409
    And revision 2 is still pending review

  Scenario: A rejection requires a reason
    Given Alice's exam is pending review
    When Hanh rejects the exam without a reason
    Then the response status is 400
    And the exam remains pending review

  Scenario Outline: Audit failure rolls back the entire decision
    Given an exam is pending review
    And writing its AuditEvent will fail
    When an authorized reviewer attempts to "<decision>" it
    Then the exam remains pending review
    And the event's selected revision remains unchanged
    And no ReviewDecision is saved

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario: Selection cannot leave a partial event decision
    Given two school exams are pending for the same test event
    And updating the event's selected revision will fail
    When Hanh selects one exam
    Then both exams remain pending
    And the event's selected revision remains unchanged
    And no ReviewDecision or AuditEvent is saved

  Scenario: Revoking a role takes effect in an existing session
    Given Hanh opened an eligible exam while authorized
    And Sam revoked Hanh's DepartmentHead role
    When Hanh tries to select the exam using the same session
    Then the response status is 403
    And the exam remains pending review

  Scenario: Revoking a department assignment takes effect in an existing session
    Given Hanh opened a School A Mathematics exam while assigned to that department
    And Sam revoked Hanh's School A Mathematics assignment but kept her DepartmentHead role
    When Hanh tries to select the exam using the same session
    Then the response status is 403
    And no ReviewDecision is recorded

  Scenario: Revoking a Ministry exam assignment takes effect in an existing session
    Given Mai opened national exam N while assigned to that event
    And Mai's assignment to national exam N was revoked
    When Mai requests its content or attempts to confirm it using the same session
    Then the response status is 403
    And no national exam content or ReviewDecision is returned
