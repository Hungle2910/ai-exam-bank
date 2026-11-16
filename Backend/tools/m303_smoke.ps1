param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$BaseUrl = 'http://127.0.0.1:53113'
)

$ErrorActionPreference = 'Stop'
function Login([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    $body = @{ userName = $name; password = $Password } | ConvertTo-Json
    $response = Invoke-WebRequest -Uri "$BaseUrl/auth/login" -Method Post -WebSession $session `
        -Headers @{ 'X-CSRF-TOKEN' = $token } -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
    if ($response.StatusCode -ne 204) { throw "Login $name failed: $($response.StatusCode)" }
    # CSRF token gắn với danh tính; sau khi login phải lấy token mới cho các lệnh ghi.
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    return @{ Session = $session; Token = $token }
}

function Call($who, [string]$method, [string]$path, $data) {
    $args = @{ Uri = "$BaseUrl$path"; Method = $method; WebSession = $who.Session; SkipHttpErrorCheck = $true }
    if ($null -ne $data) { $args.ContentType = 'application/json'; $args.Body = $data | ConvertTo-Json -Depth 8 }
    if ($method -ne 'Get') { $args.Headers = @{ 'X-CSRF-TOKEN' = $who.Token } }
    $response = Invoke-WebRequest @args
    $body = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Body = $body }
}

function Expect($result, [int]$status, [string]$step) {
    if ($result.Status -ne $status) { throw "$step expected $status, got $($result.Status)" }
}

$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
$admin = Login 'demo.user'
$school = Call $admin 'Post' '/api/v1/admin/schools' @{ name = "M303 Smoke $suffix" }
Expect $school 201 'create school'
$schoolId = $school.Body.id
$department = Call $admin 'Post' "/api/v1/admin/schools/$schoolId/departments" @{ name = 'Mathematics' }
Expect $department 201 'create department'
$departmentId = $department.Body.id
$schoolAdminName = "m303.sa.$suffix"
$schoolAdmin = Call $admin 'Post' "/api/v1/admin/schools/$schoolId/users" @{
    userName = $schoolAdminName; password = $Password; role = 'SchoolAdmin'; departmentId = $null
}
Expect $schoolAdmin 201 'create school admin'
$sa = Login $schoolAdminName
$teacherName = "m303.teacher.$suffix"
$headName = "m303.head.$suffix"
$head2Name = "m303.head2.$suffix"
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{
    userName = $teacherName; password = $Password; role = 'Teacher'; departmentId = $departmentId
}) 201 'create teacher'
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{
    userName = $headName; password = $Password; role = 'DepartmentHead'; departmentId = $departmentId
}) 201 'create reviewer'
Expect (Call $sa 'Post' "/api/v1/admin/schools/$schoolId/users" @{
    userName = $head2Name; password = $Password; role = 'DepartmentHead'; departmentId = $departmentId
}) 201 'create second reviewer'
$mathId = '8ab2a612-3558-435f-9e60-ef3f8e24a8a8'
Expect (Call $sa 'Put' "/api/v1/admin/schools/$schoolId/subjects/$mathId" @{ departmentId = $departmentId }) 201 'assign subject'
$teacher = Login $teacherName
$head = Login $headName
$head2 = Login $head2Name
$created = Call $teacher 'Post' '/api/v1/questions' @{
    stem = 'Ham so nao co dao ham bang 2x?'
    options = @('x', 'x^2', '2x', '2')
    correctOption = 'B'; difficulty = 'EASY'; subjectId = $mathId
    topicId = '223f2355-059b-4195-80e0-6056ea6115f6'
}
Expect $created 201 'create draft'
$questionId = $created.Body.id
$submitted = Call $teacher 'Post' "/api/v1/questions/$questionId/submit-review" @{ revision = 1; expectedVersion = 1 }
Expect $submitted 200 'submit review'
$pending = Call $head 'Get' '/api/v1/reviews/pending?page=1&pageSize=20' $null
Expect $pending 200 'pending queue'
if (@($pending.Body | Where-Object id -eq $questionId).Count -ne 1) { throw 'question absent from reviewer queue' }
$teacherDecision = Call $teacher 'Post' "/api/v1/reviews/$questionId/revisions/1/decision" @{ decision = 'Approve'; expectedVersion = 2 }
Expect $teacherDecision 403 'teacher cannot approve'
$approved = Call $head 'Post' "/api/v1/reviews/$questionId/revisions/1/decision" @{ decision = 'Approve'; expectedVersion = 2 }
Expect $approved 200 'approve'
$stale = Call $head 'Post' "/api/v1/reviews/$questionId/revisions/1/decision" @{ decision = 'Approve'; expectedVersion = 2 }
Expect $stale 409 'stale decision'
$detail = Call $teacher 'Get' "/api/v1/questions/$questionId" $null
Expect $detail 200 'read approved question'
if ($detail.Body.status -ne 'APPROVED') { throw "final status $($detail.Body.status)" }

# Hai phiên đăng nhập cùng quyết định một revision. DB phải chỉ chấp nhận một request.
$created2 = Call $teacher 'Post' '/api/v1/questions' @{
    stem = 'Ham so nao co dao ham bang 3x^2?'
    options = @('x', 'x^2', 'x^3', '3x'); correctOption = 'C'; difficulty = 'EASY'
    subjectId = $mathId; topicId = '223f2355-059b-4195-80e0-6056ea6115f6'
}
Expect $created2 201 'create second draft'
$question2 = $created2.Body.id
Expect (Call $teacher 'Post' "/api/v1/questions/$question2/submit-review" @{ revision = 1; expectedVersion = 1 }) 200 'submit second review'
$decisionUrl = "$BaseUrl/api/v1/reviews/$question2/revisions/1/decision"
$payload = @{ decision = 'Approve'; expectedVersion = 2 } | ConvertTo-Json
$request = {
    param($url, $session, $token, $body)
    $response = Invoke-WebRequest -Uri $url -Method Post -WebSession $session -SkipHttpErrorCheck `
        -Headers @{ 'X-CSRF-TOKEN' = $token } -ContentType 'application/json' -Body $body
    return [int]$response.StatusCode
}
$job1 = Start-ThreadJob -ScriptBlock $request -ArgumentList $decisionUrl, $head.Session, $head.Token, $payload
$job2 = Start-ThreadJob -ScriptBlock $request -ArgumentList $decisionUrl, $head2.Session, $head2.Token, $payload
$race = @((Receive-Job $job1 -Wait -AutoRemoveJob), (Receive-Job $job2 -Wait -AutoRemoveJob)) | Sort-Object
if (($race -join ',') -ne '200,409') { throw "concurrent decisions returned $($race -join ',')" }

[pscustomobject]@{
    QuestionId = $questionId; Submit = $submitted.Status; Pending = @($pending.Body).Count
    TeacherDecision = $teacherDecision.Status; Approve = $approved.Status
    StaleDecision = $stale.Status; FinalStatus = $detail.Body.status
    ConcurrentDecisions = ($race -join ',')
} | ConvertTo-Json -Compress
