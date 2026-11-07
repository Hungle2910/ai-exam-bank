param([string]$BaseUrl = 'http://127.0.0.1:65529')
$ErrorActionPreference = 'Stop'
# Reuse application-provisioned isolated Teacher fixtures and cookie/CSRF sessions.
. "$PSScriptRoot/blueprint-smoke.ps1" -BaseUrl $BaseUrl
$matrix.Remove('version')
$matrix.name = 'Exam selection smoke'
$activeBlueprint = Call $teacher POST '/api/v1/blueprints' $matrix 201
$teacher.Headers['Idempotency-Key'] = [guid]::NewGuid().ToString()
$request = @{blueprintId=$activeBlueprint.id;blueprintVersion=$activeBlueprint.version;seed=42}
$exam = Call $teacher POST '/api/v1/exams/generate' $request 202
$again = Call $teacher POST '/api/v1/exams/generate' $request 202
if ($again.id -ne $exam.id) { throw 'Idempotent retry created another exam' }
$request.seed = 43
$null = Call $teacher POST '/api/v1/exams/generate' $request 409
$resultPath = "/api/v1/exams/$($exam.id)/selection"
$result = $null
for ($attempt=0; $attempt -lt 30; $attempt++) {
    $result = Call $teacher GET $resultPath $null 200
    if ($result.status -ne 'QUEUED') { break }
    if ($result.job.status -in @('Failed','Cancelled')) { throw 'Selection job failed' }
    Start-Sleep -Milliseconds 500
}
if ($result.status -ne 'PARTIAL' -or $result.gaps.Count -ne 1 -or $result.gaps[0].missingCount -ne 3 -or $result.items.Count -ne 0) {
    throw 'Empty bank did not produce the correct persisted deficit'
}
$null = Call $other GET $resultPath $null 404
$null = Call $anonymous GET $resultPath $null 401
$null = Call $teacher POST "/api/v1/blueprints/$($activeBlueprint.id)/archive" @{version=$activeBlueprint.version} 200
$teacher.Headers['Idempotency-Key'] = [guid]::NewGuid().ToString()
$null = Call $teacher POST '/api/v1/exams/generate' $request 409
Write-Host 'Exam selection cookie bridge, outbox, worker and persisted gap smoke passed.'
