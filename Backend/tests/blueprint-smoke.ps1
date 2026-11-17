param([string]$BaseUrl = 'http://127.0.0.1:65529')
$ErrorActionPreference = 'Stop'
# Run only against an isolated test database with its own bootstrap administrator.
$adminName = $env:AIEXAMBANK_BOOTSTRAP_USERNAME
$password = $env:AIEXAMBANK_BOOTSTRAP_PASSWORD
if (-not $adminName -or -not $password) { throw 'Provide isolated test administrator credentials via AIEXAMBANK_BOOTSTRAP_USERNAME/PASSWORD.' }
function Login([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = (Invoke-RestMethod "$BaseUrl/auth/csrf" -WebSession $session).token
    $loggedIn = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        $response = Invoke-WebRequest "$BaseUrl/auth/login" -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$token} -ContentType 'application/json' -Body (@{userName=$name;password=$password} | ConvertTo-Json) -SkipHttpErrorCheck
        if ([int]$response.StatusCode -eq 204) { $loggedIn = $true; break }
        if ([int]$response.StatusCode -ne 429) { throw "Login for $name failed with HTTP $($response.StatusCode)." }
        Start-Sleep -Seconds 5
    }
    if (-not $loggedIn) { throw "Login rate limit did not clear for $name." }
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
$otherDept = Call $admin POST "$schoolPath/departments" @{name='Other department'} 201
$foreignSchool = Call $admin POST '/api/v1/admin/schools' @{name="Foreign school $suffix"} 201
$foreignPath = "/api/v1/admin/schools/$($foreignSchool.id)"
$foreignDept = Call $admin POST "$foreignPath/departments" @{name='Math'} 201
$null = Call $admin PUT "$schoolPath/subjects/8ab2a612-3558-435f-9e60-ef3f8e24a8a8" @{departmentId=$dept.id} 201
$schoolAdminName = "bp.admin.$suffix"
$null = Call $admin POST "$schoolPath/users" @{userName=$schoolAdminName;password=$password;role='SchoolAdmin'} 201
$schoolAdmin = Login $schoolAdminName
$teacherName = "bp.teacher.$suffix"
$teacherUser = Call $schoolAdmin POST "$schoolPath/users" @{userName=$teacherName;password=$password;role='Teacher';departmentId=$dept.id} 201
$otherName = "bp.other.$suffix"
$null = Call $schoolAdmin POST "$schoolPath/users" @{userName=$otherName;password=$password;role='Teacher';departmentId=$dept.id} 201
$otherDeptName = "bp.otherdept.$suffix"
$null = Call $schoolAdmin POST "$schoolPath/users" @{userName=$otherDeptName;password=$password;role='Teacher';departmentId=$otherDept.id} 201
$foreignAdminName = "bp.foreignadmin.$suffix"
$null = Call $admin POST "$foreignPath/users" @{userName=$foreignAdminName;password=$password;role='SchoolAdmin'} 201
$foreignAdmin = Login $foreignAdminName
$foreignTeacherName = "bp.foreignteacher.$suffix"
$null = Call $foreignAdmin POST "$foreignPath/users" @{userName=$foreignTeacherName;password=$password;role='Teacher';departmentId=$foreignDept.id} 201
$teacher = Login $teacherName
$catalog = @(Call $teacher GET '/api/v1/catalog' $null 200)
$matrix = @{name='Blueprint smoke';subjectId=$catalog[0].id;durationMinutes=45;totalQuestions=3;totalPoints=0.75;slots=@(@{topicId=$catalog[0].topics[0].id;difficulty='EASY';questionCount=3;pointsPerQuestion=0.25})}
$saved = Call $teacher POST '/api/v1/blueprints' $matrix 201
$path = "/api/v1/blueprints/$($saved.id)"
$null = Call $teacher GET $path $null 200
$questionBody = @{stem="M3 week 2 scope check $suffix";options=@('A1','B1','C1','D1');correctOption='A';difficulty='EASY';subjectId=$catalog[0].id;topicId=$catalog[0].topics[0].id;source='M3 isolated smoke'}
$question = Call $teacher POST '/api/v1/questions' $questionBody 201
if ($question.status -ne 'DRAFT') { throw 'New question did not start in DRAFT.' }
$questionPath = "/api/v1/questions/$($question.id)"
$null = Call $teacher GET $questionPath $null 200
$matrix.version = $saved.version
$matrix.name = 'Saved update'
$secondEditor = Login $teacherName
$body = $matrix | ConvertTo-Json -Depth 8
$editors = @($teacher, $secondEditor) | ForEach-Object {
    Start-ThreadJob -ThrottleLimit 2 -ArgumentList $_, "$BaseUrl$path", $body -ScriptBlock {
        param($editor, $uri, $payload)
        $ErrorActionPreference = 'Stop'
        $response = Invoke-WebRequest -Uri $uri -Method Put -WebSession $editor.Session -Headers $editor.Headers `
            -ContentType 'application/json' -Body $payload -SkipHttpErrorCheck
        [pscustomobject]@{ Status = [int]$response.StatusCode; Content = $response.Content }
    }
}
try {
    $writes = @($editors | Receive-Job -Wait -ErrorAction Stop)
    if (($writes.Status | Sort-Object) -join ',' -ne '200,409') {
        throw "Concurrent saves expected HTTP 200 and 409, got $($writes.Status -join ', ')."
    }
    $updated = ($writes | Where-Object Status -eq 200).Content | ConvertFrom-Json
} finally {
    $editors | Remove-Job -Force
}
$null = Call $teacher PUT $path $matrix 409
$matrix.version = $updated.version; $matrix.totalPoints = 99
$null = Call $teacher PUT $path $matrix 400
$matrix.totalPoints = 0.75
$null = Call $schoolAdmin POST '/api/v1/blueprints' $matrix 403
$other = Login $otherName
$null = Call $other GET $path $null 404
$null = Call $other POST "$path/archive" @{version=$updated.version} 404
$otherDeptTeacher = Login $otherDeptName
$foreignTeacher = Login $foreignTeacherName
foreach ($outsider in @($other, $otherDeptTeacher, $foreignTeacher)) {
    $null = Call $outsider GET $path $null 404
    $null = Call $outsider PUT $path $matrix 404
    $null = Call $outsider GET $questionPath $null 404
    $null = Call $outsider PUT $questionPath ($questionBody + @{baseRevision=1}) 404
    $blueprints = Call $outsider GET '/api/v1/blueprints' $null 200
    if ($blueprints.items.id -contains $saved.id) { throw 'Another actor can list the blueprint.' }
    $questions = Call $outsider GET '/api/v1/questions' $null 200
    if ($questions.items.id -contains $question.id) { throw 'Another actor can list the question.' }
}
$null = Call $otherDeptTeacher POST '/api/v1/blueprints' $matrix 400
$null = Call $otherDeptTeacher POST '/api/v1/questions' $questionBody 403
$null = Call $foreignTeacher POST '/api/v1/questions' $questionBody 400
$anonymous = @{Session=(New-Object Microsoft.PowerShell.Commands.WebRequestSession);Headers=@{}}
$null = Call $anonymous GET $path $null 401
$null = Call $anonymous GET $questionPath $null 401
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
$null = Call $schoolAdmin DELETE "/api/v1/admin/role-assignments/$($teacherUser.assignmentId)" $null 204
$null = Call $teacher GET $path $null 404
$null = Call $teacher PUT $path $matrix 404
$null = Call $teacher POST '/api/v1/blueprints' $matrix 403
$null = Call $teacher GET $questionPath $null 404
$null = Call $teacher PUT $questionPath ($questionBody + @{baseRevision=1}) 404
$null = Call $teacher POST '/api/v1/questions' $questionBody 403
Write-Host 'M3 week 2 Blueprint and Question HTTP scope smoke passed; fixtures retained in the isolated database.'
