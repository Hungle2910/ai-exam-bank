param(
    [string]$BaseUrl = 'http://127.0.0.1:65527',
    [string]$UserName = 'demo.user',
    [int]$WaitSeconds = 4
)

$ErrorActionPreference = 'Stop'
$password = $env:AIEXAMBANK_SMOKE_PASSWORD
if ([string]::IsNullOrWhiteSpace($password)) {
    throw 'Set AIEXAMBANK_SMOKE_PASSWORD before running this test.'
}

$base = $BaseUrl.TrimEnd('/')
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf = Invoke-WebRequest "$base/auth/csrf" -WebSession $session -UseBasicParsing
$token = ($csrf.Content | ConvertFrom-Json).token
if ([string]::IsNullOrWhiteSpace($token)) { throw 'CSRF token is empty.' }

$body = @{ userName = $UserName; password = $password } | ConvertTo-Json
$login = Invoke-WebRequest "$base/auth/login" -Method Post -WebSession $session `
    -Headers @{ 'X-CSRF-TOKEN' = $token } -ContentType 'application/json' `
    -Body $body -UseBasicParsing
if ([int]$login.StatusCode -ne 204) { throw "Login returned HTTP $($login.StatusCode)." }

$me = Invoke-WebRequest "$base/auth/me" -WebSession $session -UseBasicParsing
if ([int]$me.StatusCode -ne 200) { throw "Authenticated /auth/me returned HTTP $($me.StatusCode)." }
Write-Output 'Login and authenticated /auth/me: 204, 200'

Start-Sleep -Seconds $WaitSeconds
try {
    $expired = Invoke-WebRequest "$base/auth/me" -WebSession $session -UseBasicParsing
    $status = [int]$expired.StatusCode
}
catch [System.Net.WebException] {
    if ($null -eq $_.Exception.Response) { throw }
    $status = [int]$_.Exception.Response.StatusCode
}
Write-Output "After $WaitSeconds seconds /auth/me: $status"
if ($status -ne 401) { throw "Expected an expired session (HTTP 401), got $status." }
