<#
M4-03 smoke: revision history on a running API, on top of the M3-03 review flow.
Needs an API started in Development against a throwaway database with LocalDemoUser enabled
(see docs/M4-03/README.md, "Smoke test"). It creates its own school, department and users.

  1. teacher creates a question, submits it, a department head approves revision 1
  2. teacher edits the approved question -> revision 2 is a new DRAFT, revision 1 stays approved and in use
  3. a second edit from the old base revision is a 409
  4. history lists both revisions, newest first, and revision 1 is unchanged
  5. revision 2 goes through review; the approved revision in use moves to 2 and revision 1 stays readable
  6. the reviewer reads the history while revision 2 is pending; a teacher of another department gets 404
#>
param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$BaseUrl = 'http://127.0.0.1:53113'
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 throws on a non-2xx answer and has no -SkipHttpErrorCheck; PowerShell 7 has it.
# Both return (status, content) so a step can assert on 403/404/409.
function Send([hashtable]$request) {
    $request.UseBasicParsing = $true
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $response = Invoke-WebRequest @request -SkipHttpErrorCheck
        return @{ Status = [int]$response.StatusCode; Content = $response.Content }
    }
    try {
        $response = Invoke-WebRequest @request
        return @{ Status = [int]$response.StatusCode; Content = $response.Content }
    } catch [System.Net.WebException] {
        $failed = $_.Exception.Response
        if ($null -eq $failed) { throw }
        $reader = New-Object System.IO.StreamReader($failed.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Dispose()
        return @{ Status = [int]$failed.StatusCode; Content = $content }
    }
}

function Login([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    $body = @{ userName = $name; password = $Password } | ConvertTo-Json
    $response = Send @{ Uri = "$BaseUrl/auth/login"; Method = 'Post'; WebSession = $session
        Headers = @{ 'X-CSRF-TOKEN' = $token }; ContentType = 'application/json'; Body = $body }
    if ($response.Status -ne 204) { throw "Login $name failed: $($response.Status)" }
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    return @{ Session = $session; Token = $token }
}

function Call($who, [string]$method, [string]$path, $data) {
    $args = @{ Uri = "$BaseUrl$path"; Method = $method; WebSession = $who.Session }
    if ($null -ne $data) { $args.ContentType = 'application/json'; $args.Body = $data | ConvertTo-Json -Depth 8 }
    if ($method -ne 'Get') { $args.Headers = @{ 'X-CSRF-TOKEN' = $who.Token } }
    $response = Send $args
    $body = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = $response.Status; Body = $body }
}

function Expect($result, [int]$status, [string]$step) {
    if ($result.Status -ne $status) { throw "$step expected $status, got $($result.Status): $($result.Body | ConvertTo-Json -Depth 4 -Compress)" }
}
function Check([bool]$condition, [string]$what) { if (-not $condition) { throw "check failed: $what" } }

$mathId = '8ab2a612-3558-435f-9e60-ef3f8e24a8a8'
$topicId = '223f2355-059b-4195-80e0-6056ea6115f6'

# --- setup: one school, two departments (only the first owns the subject) -----------------------------------------
$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
$admin = Login 'demo.user'
$school = Call $admin 'Post' '/api/v1/admin/schools' @{ name = "M403 Smoke $suffix" }
Expect $school 201 'create school'
$schoolId = $school.Body.id
$dept = Call $admin 'Post' "/api/v1/admin/schools/$schoolId/departments" @{ name = 'Mathematics' }
Expect $dept 201 'create department'
$otherDept = Call $admin 'Post' "/api/v1/admin/schools/$schoolId/departments" @{ name = 'Literature' }
Expect $otherDept 201 'create other department'
$saName = "m403.sa.$suffix"
Expect (Call $admin 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = $saName; password = $Password; role = 'SchoolAdmin'; departmentId = $null }) 201 'create school admin'
$sa = Login $saName
$names = @{ teacher = "m403.teacher.$suffix"; head = "m403.head.$suffix"; outsider = "m403.outsider.$suffix" }
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = $names.teacher; password = $Password; role = 'Teacher'; departmentId = $dept.Body.id }) 201 'create teacher'
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = $names.head; password = $Password; role = 'DepartmentHead'; departmentId = $dept.Body.id }) 201 'create reviewer'
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = $names.outsider; password = $Password; role = 'Teacher'; departmentId = $otherDept.Body.id }) 201 'create teacher of another department'
Expect (Call $sa 'Put' "/api/v1/admin/schools/$schoolId/subjects/$mathId" @{ departmentId = $dept.Body.id }) 201 'assign subject'
$teacher = Login $names.teacher
$head = Login $names.head
$outsider = Login $names.outsider

# --- 1. create, submit, approve revision 1 -------------------------------------------------------------------------
$body = @{ stem = 'Ham so nao co dao ham bang 2x?'; options = @('x', 'x^2', '2x', '2'); correctOption = 'B'; difficulty = 'EASY'; subjectId = $mathId; topicId = $topicId }
$created = Call $teacher 'Post' '/api/v1/questions' $body
Expect $created 201 'create draft'
$id = $created.Body.id
Expect (Call $teacher 'Post' "/api/v1/questions/$id/submit-review" @{ revision = 1; expectedVersion = 1 }) 200 'submit revision 1'
Expect (Call $head 'Post' "/api/v1/reviews/$id/revisions/1/decision" @{ decision = 'Approve'; expectedVersion = 2 }) 200 'approve revision 1'

# --- 2. edit the approved question: a new DRAFT, the approved revision stays in use -------------------------------
$edit = $body.Clone()
$edit.stem = 'Ham so nao co dao ham bang 2x? (da sua)'
$edit.baseRevision = 1
$edited = Call $teacher 'Put' "/api/v1/questions/$id" $edit
Expect $edited 200 'edit approved question'
Check ($edited.Body.revision -eq 2) 'edit creates revision 2'
Check ($edited.Body.status -eq 'DRAFT') 'revision 2 is a DRAFT'
Check ($edited.Body.approvedRevision -eq 1) 'revision 1 is still the approved revision in use'

# --- 3. stale update -------------------------------------------------------------------------------------------------
$stale = Call $teacher 'Put' "/api/v1/questions/$id" $edit
Expect $stale 409 'update from an old base revision'

# --- 4. history order and immutability ------------------------------------------------------------------------------
$history = Call $teacher 'Get' "/api/v1/questions/$id/revisions" $null
Expect $history 200 'history'
$revs = @($history.Body.revisions)
Check ($revs.Count -eq 2) 'two revisions'
Check ($revs[0].revision -eq 2 -and $revs[1].revision -eq 1) 'newest first'
Check ($revs[0].isLatest -and -not $revs[0].isApprovedInUse) 'revision 2 is latest, not in use'
Check ($revs[1].isApprovedInUse -and $revs[1].status -eq 'APPROVED') 'revision 1 is approved and in use'
Check ($revs[1].stem -eq $body.stem) 'revision 1 content is unchanged'
Check ($revs[0].stem -ne $revs[1].stem) 'revision 2 has the new content'
$one = Call $teacher 'Get' "/api/v1/questions/$id/revisions/1" $null
Expect $one 200 'read revision 1'

# --- 5. the new revision needs review again ------------------------------------------------------------------------
Expect (Call $teacher 'Post' "/api/v1/questions/$id/submit-review" @{ revision = 2; expectedVersion = 1 }) 200 'submit revision 2'
# A department head reads a question only while it waits for review (M3-02 policy); that is when they need the history.
$pendingHistory = Call $head 'Get' "/api/v1/questions/$id/revisions" $null
Expect $pendingHistory 200 'history for the reviewer while revision 2 is pending'
Check (@($pendingHistory.Body.revisions).Count -eq 2) 'reviewer sees both revisions to compare'
Expect (Call $head 'Post' "/api/v1/reviews/$id/revisions/2/decision" @{ decision = 'Approve'; expectedVersion = 2 }) 200 'approve revision 2'
$after = Call $teacher 'Get' "/api/v1/questions/$id/revisions" $null
Check ($after.Body.approvedRevision -eq 2) 'approved revision in use moved to 2'
$afterRevs = @($after.Body.revisions)
Check ($afterRevs[1].status -eq 'APPROVED' -and -not $afterRevs[1].isApprovedInUse) 'revision 1 stays approved, no longer in use'
Check ($afterRevs[1].stem -eq $body.stem) 'revision 1 content still unchanged'

# --- 6. history is only for users in scope ---------------------------------------------------------------------------
Expect (Call $outsider 'Get' "/api/v1/questions/$id/revisions" $null) 404 'history for another department'
Expect (Call $outsider 'Get' "/api/v1/questions/$id/revisions/1" $null) 404 'revision for another department'
Expect (Call $head 'Get' "/api/v1/questions/$id/revisions" $null) 404 'history for the department head after the decision (not pending any more)'

[pscustomobject]@{
    QuestionId = $id
    EditAfterApprove = "rev $($edited.Body.revision) $($edited.Body.status), approved in use $($edited.Body.approvedRevision)"
    StaleUpdate = $stale.Status
    HistoryOrder = ($revs | ForEach-Object { $_.revision }) -join ','
    ApprovedInUseAfterReview = $after.Body.approvedRevision
    OtherDepartmentHistory = 404
} | ConvertTo-Json -Compress
