param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$BaseUrl = 'http://127.0.0.1:53115'
)

$ErrorActionPreference = 'Stop'

function Login([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    $body = @{ userName = $name; password = $Password } | ConvertTo-Json
    $response = Invoke-WebRequest -Uri "$BaseUrl/auth/login" -Method Post -WebSession $session `
        -Headers @{ 'X-CSRF-TOKEN' = $token } -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
    if ($response.StatusCode -ne 204) { throw "Login $name failed: $($response.StatusCode)" }
    $token = (Invoke-RestMethod -Uri "$BaseUrl/auth/csrf" -WebSession $session).token
    return @{ Session = $session; Token = $token }
}

function Call($who, [string]$method, [string]$path, $data) {
    $args = @{ Uri = "$BaseUrl$path"; Method = $method; SkipHttpErrorCheck = $true }
    if ($null -ne $who) { $args.WebSession = $who.Session }
    if ($null -ne $data) { $args.ContentType = 'application/json'; $args.Body = $data | ConvertTo-Json -Depth 8 }
    if ($method -ne 'Get' -and $null -ne $who) { $args.Headers = @{ 'X-CSRF-TOKEN' = $who.Token } }
    $response = Invoke-WebRequest @args
    $body = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Body = $body }
}

function Expect($result, [int]$status, [string]$step) {
    if ($result.Status -ne $status) { throw "$step expected $status, got $($result.Status)" }
}

Expect (Call $null 'Get' '/api/v1/audit/role-assignments' $null) 401 'anonymous'
$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
$ministry = Login 'demo.user'
$schoolA = Call $ministry 'Post' '/api/v1/admin/schools' @{ name = "M305-A-$suffix" }
$schoolB = Call $ministry 'Post' '/api/v1/admin/schools' @{ name = "M305-B-$suffix" }
Expect $schoolA 201 'school A'
Expect $schoolB 201 'school B'
$a = $schoolA.Body.id
$b = $schoolB.Body.id
$deptA = Call $ministry 'Post' "/api/v1/admin/schools/$a/departments" @{ name = 'Mathematics' }
$deptB = Call $ministry 'Post' "/api/v1/admin/schools/$b/departments" @{ name = 'Mathematics' }
Expect $deptA 201 'department A'
Expect $deptB 201 'department B'
$schoolAdminA = "m305.sa.a.$suffix"
$schoolAdminB = "m305.sa.b.$suffix"
Expect (Call $ministry 'Post' "/api/v1/admin/schools/$a/users" @{ userName = $schoolAdminA; password = $Password; role = 'SchoolAdmin' }) 201 'admin A'
Expect (Call $ministry 'Post' "/api/v1/admin/schools/$b/users" @{ userName = $schoolAdminB; password = $Password; role = 'SchoolAdmin' }) 201 'admin B'
$sa = Login $schoolAdminA
$sb = Login $schoolAdminB
$teacherA = "m305.teacher.a.$suffix"
$teacherB = "m305.teacher.b.$suffix"
Expect (Call $sa 'Post' "/api/v1/admin/schools/$a/users" @{ userName = $teacherA; password = $Password; role = 'Teacher'; departmentId = $deptA.Body.id }) 201 'teacher A'
Expect (Call $sb 'Post' "/api/v1/admin/schools/$b/users" @{ userName = $teacherB; password = $Password; role = 'Teacher'; departmentId = $deptB.Body.id }) 201 'teacher B'
$teacher = Login $teacherA
Expect (Call $teacher 'Get' '/api/v1/audit/role-assignments' $null) 403 'teacher denied'
Expect (Call $sa 'Get' '/api/v1/audit/role-assignments?action=INVALID' $null) 400 'invalid action'
$own = Call $sa 'Get' '/api/v1/audit/role-assignments?pageSize=100' $null
Expect $own 200 'school audit'
if (@($own.Body | Where-Object target -eq $teacherA).Count -ne 1) { throw 'Teacher A audit missing' }
if (@($own.Body | Where-Object target -eq $teacherB).Count -ne 0) { throw 'Cross-school audit visible' }
if (@($own.Body | Where-Object action -ne 'ROLE_GRANTED').Count -ne 0) { throw 'Unexpected action in school audit' }
$national = Call $ministry 'Get' '/api/v1/audit/role-assignments?pageSize=100' $null
Expect $national 200 'ministry audit'
if (@($national.Body | Where-Object target -eq $schoolAdminA).Count -ne 1) { throw 'SchoolAdmin audit missing' }
if (@($national.Body | Where-Object target -eq $teacherA).Count -ne 0) { throw 'Teacher audit visible to ministry' }
Write-Output 'M3-05 role audit smoke passed: 401/403/400, scoped school and ministry views.'
