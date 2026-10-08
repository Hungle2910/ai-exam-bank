# Proposed acceptance cases for docs/IDENTITY_AND_PERMISSIONS.md.
# No application runner or step definitions exist yet. These are reviewable
# scenarios, not passing product tests or evidence of enforcement.

Feature: Ministry, school, department and teacher authorization
  Teachers request AI-generated school exams. DepartmentHeads review exams
  in assigned school departments. SchoolAdmins manage their schools.
  MinistryAdmins manage the system and independently prepare and confirm
  assigned ministry-level exams.

  Background:
    Given Alice is a Teacher at School A in Mathematics
    And Hanh is a DepartmentHead for Mathematics at School A
    And Sam is a SchoolAdmin for School A
    And Mai and Minh are MinistryAdmins assigned to national exam N

  Scenario: A Teacher requests an AI-generated school exam
    When Alice creates a Mathematics blueprint for School A
    And Alice requests AI exam generation
    Then the generated exam is a draft owned by Alice in School A
    And AI cannot approve that exam
    When Alice submits the draft for review
    Then the exam is pending review by the assigned DepartmentHead

  Scenario: A DepartmentHead reviews a valid school exam
    Given Alice's School A Mathematics exam is pending review at revision 1
    When Hanh approves revision 1
    Then the exam is approved at revision 1
    And a ReviewDecision and AuditEvent identify Hanh and revision 1

  Scenario Outline: A DepartmentHead cannot cross school or department scope
    Given an exam is pending at "<school>" in "<department>"
    When Hanh directly requests its content or an approval
    Then the response status is 403
    And no review decision is recorded

    Examples:
      | school   | department |
      | School B | Mathematics |
      | School A | Science     |

  Scenario: A Teacher cannot approve an exam by calling the API
    Given a school exam is pending review
    When Alice directly sends an approval request
    Then the response status is 403
    And the exam remains pending review

  Scenario: A SchoolAdmin cannot manage another school's users
    When Sam directly attempts to change a School B user's role
    Then the response status is 403
    And that user's role assignments remain unchanged

  Scenario: A SchoolAdmin cannot grant the MinistryAdmin role
    When Sam directly attempts to grant MinistryAdmin to a School A user
    Then the response status is 403
    And that user's role assignments remain unchanged

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

  Scenario: A stale revision cannot be approved
    Given Hanh opened Alice's pending exam at revision 1
    And Alice's exam changed to revision 2
    When Hanh attempts to approve revision 1
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
    And no ReviewDecision is saved

    Examples:
      | decision |
      | APPROVE  |
      | REJECT   |

  Scenario: Revoking a role takes effect in an existing session
    Given Hanh opened an eligible exam while authorized
    And Sam revoked Hanh's DepartmentHead role
    When Hanh tries to approve the exam using the same session
    Then the response status is 403
    And the exam remains pending review
