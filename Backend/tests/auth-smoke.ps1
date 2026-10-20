param(
    [string]$BaseUrl = 'http://127.0.0.1:65527',
    [string]$UserName = 'demo.user'
)

$ErrorActionPreference = 'Stop'
$password = $env:AIEXAMBANK_SMOKE_PASSWORD
if ([string]::IsNullOrWhiteSpace($password)) {
    throw 'Set AIEXAMBANK_SMOKE_PASSWORD before running this test.'
}

$base = $BaseUrl.TrimEnd('/')
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$schoolId = '11111111-1111-1111-1111-111111111111'

function Get-Status {
    param([scriptblock]$Request)

    try {
        $response = & $Request
        return [int]$response.StatusCode
    }
    catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}

function Assert-Status {
    param([string]$Name, [int]$Actual, [int]$Expected)

    Write-Output ('{0}: {1}' -f $Name, $Actual)
    if ($Actual -ne $Expected) {
        throw ('{0}: expected HTTP {1}, got {2}.' -f $Name, $Expected, $Actual)
    }
}

Assert-Status 'Anonymous /auth/me' (Get-Status {
    Invoke-WebRequest "$base/auth/me" -WebSession $session -UseBasicParsing
}) 401

$csrfResponse = Invoke-WebRequest "$base/auth/csrf" -WebSession $session -UseBasicParsing
Assert-Status 'CSRF token' ([int]$csrfResponse.StatusCode) 200
$csrfToken = ($csrfResponse.Content | ConvertFrom-Json).token
if ([string]::IsNullOrWhiteSpace($csrfToken)) { throw 'CSRF token is empty.' }

$validBody = @{ userName = $UserName; password = $password } | ConvertTo-Json
$invalidBody = @{ userName = '__missing_demo_user__'; password = 'invalid' } | ConvertTo-Json
$headers = @{ 'X-CSRF-TOKEN' = $csrfToken }

Assert-Status 'Login without CSRF' (Get-Status {
    Invoke-WebRequest "$base/auth/login" -Method Post -WebSession $session -ContentType 'application/json' -Body $validBody -UseBasicParsing
}) 400

Assert-Status 'Unknown user' (Get-Status {
    Invoke-WebRequest "$base/auth/login" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body $invalidBody -UseBasicParsing
}) 401

Assert-Status 'Login' (Get-Status {
    Invoke-WebRequest "$base/auth/login" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body $validBody -UseBasicParsing
}) 204

$meResponse = Invoke-WebRequest "$base/auth/me" -WebSession $session -UseBasicParsing
Assert-Status 'Authenticated /auth/me' ([int]$meResponse.StatusCode) 200
$me = $meResponse.Content | ConvertFrom-Json
if ($me.userName -ne $UserName) { throw 'The authenticated user is different from the expected user.' }

$csrfResponse = Invoke-WebRequest "$base/auth/csrf" -WebSession $session -UseBasicParsing
Assert-Status 'Authenticated CSRF token' ([int]$csrfResponse.StatusCode) 200
$csrfToken = ($csrfResponse.Content | ConvertFrom-Json).token
if ([string]::IsNullOrWhiteSpace($csrfToken)) { throw 'Authenticated CSRF token is empty.' }
$headers = @{ 'X-CSRF-TOKEN' = $csrfToken }

Assert-Status 'No SchoolAdmin role' (Get-Status {
    Invoke-WebRequest "$base/api/v1/admin/schools/$schoolId/role-assignments" -WebSession $session -UseBasicParsing
}) 403

Assert-Status 'Logout without CSRF' (Get-Status {
    Invoke-WebRequest "$base/auth/logout" -Method Post -WebSession $session -UseBasicParsing
}) 400

Assert-Status 'Logout' (Get-Status {
    Invoke-WebRequest "$base/auth/logout" -Method Post -WebSession $session -Headers $headers -UseBasicParsing
}) 204

Assert-Status 'After logout /auth/me' (Get-Status {
    Invoke-WebRequest "$base/auth/me" -WebSession $session -UseBasicParsing
}) 401
