<#
M4-03 upgrade check: the migrations of this branch applied to a database that already holds M3-03 review data.

  Phase Seed      run against an API built from `dev` (M3-03 migration applied, M4-03 migrations NOT applied):
                  creates a school, a teacher and a head, a question with revision 1 approved and a draft revision 2.
  (then stop that API, build this branch, `dotnet ef database update`, start this branch's API on the same database)
  Phase Continue  run against this branch's API: the question made before the upgrade must still read, show its history,
                  and go through submit and approve again.

Fixed user names (m403.*.up) are used so the second phase can sign in as the users the first phase created.
See docs/M4-03_VERSION_HISTORY.md, section 10.
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('Seed', 'Continue')][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$Id,
    [string]$BaseUrl = 'http://127.0.0.1:5000'
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


$mathId = '8ab2a612-3558-435f-9e60-ef3f8e24a8a8'
$topicId = '223f2355-059b-4195-80e0-6056ea6115f6'

if ($Phase -eq 'Seed') {
    $admin = Login 'demo.user'
    $school = Call $admin 'Post' '/api/v1/admin/schools' @{ name = 'M403 Upgrade check' }
    Expect $school 201 'create school'
    $schoolId = $school.Body.id
    $dept = Call $admin 'Post' "/api/v1/admin/schools/$schoolId/departments" @{ name = 'Mathematics' }
    Expect $dept 201 'create department'
    Expect (Call $admin 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = 'm403.sa.up'; password = $Password; role = 'SchoolAdmin'; departmentId = $null }) 201 'create school admin'
    $sa = Login 'm403.sa.up'
    Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = 'm403.teacher.up'; password = $Password; role = 'Teacher'; departmentId = $dept.Body.id }) 201 'create teacher'
    Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{ userName = 'm403.head.up'; password = $Password; role = 'DepartmentHead'; departmentId = $dept.Body.id }) 201 'create reviewer'
    Expect (Call $sa 'Put' "/api/v1/admin/schools/$schoolId/subjects/$mathId" @{ departmentId = $dept.Body.id }) 201 'assign subject'
    $teacher = Login 'm403.teacher.up'
    $head = Login 'm403.head.up'

    $body = @{ stem = 'Ham so nao co dao ham bang 2x?'; options = @('x', 'x^2', '2x', '2'); correctOption = 'B'; difficulty = 'EASY'; subjectId = $mathId; topicId = $topicId }
    $created = Call $teacher 'Post' '/api/v1/questions' $body
    Expect $created 201 'create draft'
    $id = $created.Body.id
    Expect (Call $teacher 'Post' "/api/v1/questions/$id/submit-review" @{ revision = 1; expectedVersion = 1 }) 200 'submit revision 1'
    Expect (Call $head 'Post' "/api/v1/reviews/$id/revisions/1/decision" @{ decision = 'Approve'; expectedVersion = 2 }) 200 'approve revision 1'
    $edit = $body.Clone(); $edit.stem = 'Ham so nao co dao ham bang 2x? (da sua)'; $edit.baseRevision = 1
    Expect (Call $teacher 'Put' "/api/v1/questions/$id" $edit) 200 'edit approved question'
    $id
    return
}

if (-not $Id) { throw 'Phase Continue needs -Id (the question id printed by Phase Seed)' }
$teacher = Login 'm403.teacher.up'
$head = Login 'm403.head.up'
$state = Call $teacher 'Get' "/api/v1/questions/$Id" $null
Expect $state 200 'read the question created before the upgrade'
Check ($state.Body.revision -eq 2 -and $state.Body.status -eq 'DRAFT') 'revision 2 is still a DRAFT'
Check ($state.Body.approvedRevision -eq 1) 'revision 1 is still the approved revision in use'
$history = Call $teacher 'Get' "/api/v1/questions/$Id/revisions" $null
Expect $history 200 'history of a question created before the upgrade'
Check (@($history.Body.revisions).Count -eq 2) 'two revisions'
Expect (Call $teacher 'Post' "/api/v1/questions/$Id/submit-review" @{ revision = 2; expectedVersion = 1 }) 200 'submit revision 2 after the upgrade'
Expect (Call $head 'Post' "/api/v1/reviews/$Id/revisions/2/decision" @{ decision = 'Approve'; expectedVersion = 2 }) 200 'approve revision 2 after the upgrade'
$after = Call $teacher 'Get' "/api/v1/questions/$Id/revisions" $null
Check ($after.Body.approvedRevision -eq 2) 'approved revision in use moved to 2'
[pscustomobject]@{ QuestionId = $Id; BeforeUpgrade = 'rev 1 APPROVED, rev 2 DRAFT'; HistoryRevisions = 2; ApprovedInUseAfter = $after.Body.approvedRevision } | ConvertTo-Json -Compress
