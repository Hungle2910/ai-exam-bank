param([string]$BaseUrl = 'http://127.0.0.1:65529')
$ErrorActionPreference = 'Stop'
# Run only against an isolated test database with its own bootstrap administrator.
$adminName = $env:AIEXAMBANK_BOOTSTRAP_USERNAME
$password = $env:AIEXAMBANK_BOOTSTRAP_PASSWORD
if (-not $adminName -or -not $password) { throw 'Provide isolated test administrator credentials via AIEXAMBANK_BOOTSTRAP_USERNAME/PASSWORD.' }
function Login([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = (Invoke-RestMethod "$BaseUrl/auth/csrf" -WebSession $session).token
    $null = Invoke-WebRequest "$BaseUrl/auth/login" -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$token} -ContentType 'application/json' -Body (@{userName=$name;password=$password} | ConvertTo-Json)
    $token = (Invoke-RestMethod "$BaseUrl/auth/csrf" -WebSession $session).token
    return @{Session=$session;Headers=@{'X-CSRF-TOKEN'=$token}}
}
function Call($actor, [string]$method, [string]$path, $body, [int]$expected) {
    $args = @{Uri="$BaseUrl$path";Method=$method;WebSession=$actor.Session;Headers=$actor.Headers;SkipHttpErrorCheck=$true}
    if ($null -ne $body) { $args.ContentType='application/json'; $args.Body=$body | ConvertTo-Json -Depth 8 }
    $r = Invoke-WebRequest @args
    if ([int]$r.StatusCode -ne $expected) { throw "$method $path expected $expected, got $($r.StatusCode): $($r.Content)" }
    Write-Host "$method $path : $expected"
    if ($r.Content) { return $r.Content | ConvertFrom-Json }
}
$suffix = [guid]::NewGuid().ToString('N')
$admin = Login $adminName
$school = Call $admin POST '/api/v1/admin/schools' @{name="Blueprint smoke $suffix"} 201
$schoolPath = "/api/v1/admin/schools/$($school.id)"
$dept = Call $admin POST "$schoolPath/departments" @{name='Math'} 201
$null = Call $admin PUT "$schoolPath/subjects/8ab2a612-3558-435f-9e60-ef3f8e24a8a8" @{departmentId=$dept.id} 201
$schoolAdminName = "bp.admin.$suffix"
$null = Call $admin POST "$schoolPath/users" @{userName=$schoolAdminName;password=$password;role='SchoolAdmin'} 201
$schoolAdmin = Login $schoolAdminName
$teacherName = "bp.teacher.$suffix"
$null = Call $schoolAdmin POST "$schoolPath/users" @{userName=$teacherName;password=$password;role='Teacher';departmentId=$dept.id} 201
$otherName = "bp.other.$suffix"
$null = Call $schoolAdmin POST "$schoolPath/users" @{userName=$otherName;password=$password;role='Teacher';departmentId=$dept.id} 201
$teacher = Login $teacherName
$catalog = @(Call $teacher GET '/api/v1/catalog' $null 200)
$matrix = @{name='Blueprint smoke';subjectId=$catalog[0].id;durationMinutes=45;totalQuestions=3;totalPoints=0.75;slots=@(@{topicId=$catalog[0].topics[0].id;difficulty='EASY';questionCount=3;pointsPerQuestion=0.25})}
$saved = Call $teacher POST '/api/v1/blueprints' $matrix 201
$path = "/api/v1/blueprints/$($saved.id)"
$null = Call $teacher GET $path $null 200
$matrix.version = $saved.version
$matrix.name = 'Saved update'
$updated = Call $teacher PUT $path $matrix 200
$null = Call $teacher PUT $path $matrix 409
$matrix.version = $updated.version; $matrix.totalPoints = 99
$null = Call $teacher PUT $path $matrix 400
$matrix.totalPoints = 0.75
$null = Call $schoolAdmin POST '/api/v1/blueprints' $matrix 403
$other = Login $otherName
$null = Call $other GET $path $null 404
$null = Call $other POST "$path/archive" @{version=$updated.version} 404
$anonymous = @{Session=(New-Object Microsoft.PowerShell.Commands.WebRequestSession);Headers=@{}}
$null = Call $anonymous GET $path $null 401
$noCsrfSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$noCsrfSession.Cookies = $teacher.Session.Cookies
$noCsrf = @{Session=$noCsrfSession;Headers=@{}}
$null = Call $noCsrf POST "$path/archive" @{version=$updated.version} 400
$archived = Call $teacher POST "$path/archive" @{version=$updated.version} 200
$active = Call $teacher GET '/api/v1/blueprints' $null 200
if ($active.items.id -contains $saved.id) { throw 'Archived item is still in active list' }
$history = Call $teacher GET '/api/v1/blueprints?archived=true' $null 200
if ($history.items.id -notcontains $saved.id) { throw 'Archived item is missing from history' }
$detail = Call $teacher GET $path $null 200
if ($detail.name -ne 'Saved update' -or $detail.slots.Count -ne 1 -or $detail.totalPoints -ne 0.75) { throw 'Archival changed matrix content' }
$matrix.version = $archived.version
$null = Call $teacher PUT $path $matrix 409
Write-Host 'Blueprint HTTP smoke passed; fixtures retained in the isolated database.'
