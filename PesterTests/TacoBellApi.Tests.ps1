# Black-box tests against a RUNNING API instance — the PowerShell/network twin of the
# NUnit suite. Unlike the NUnit tests (in-process TestServer, fresh DB per test, auth
# bypass), these make real HTTP requests and authenticate with a real JWT from /token.
#
# Prerequisites:
#   - API running:  cd API; dotnet run   (or set TACOBELL_API_URL to another instance)
#   - Pester 5:     Install-Module Pester -Force -Scope CurrentUser -SkipPublisherCheck
# Run:  Invoke-Pester -Path .\PesterTests
#
# The server database persists between runs, so every mutation test creates its own
# row and count assertions use ">= 83" rather than exact seed counts.

BeforeAll {
    $script:Base = if ($env:TACOBELL_API_URL) { $env:TACOBELL_API_URL } else { 'https://localhost:7016' }

    try {
        $tokenResponse = Invoke-RestMethod -Method Post -Uri "$Base/token" -SkipCertificateCheck -ContentType 'application/json' `
            -Body (@{ clientId = 'demo-client'; clientSecret = 'demo-secret' } | ConvertTo-Json)
    }
    catch {
        throw "Could not reach the API at $Base — is it running? (cd API; dotnet run). Error: $_"
    }
    $script:Headers = @{ Authorization = "Bearer $($tokenResponse.accessToken)" }

    # Helper: request that returns the response even on 4xx instead of throwing.
    function Invoke-Api {
        param([string]$Method = 'GET', [string]$Path, [hashtable]$Body, [switch]$NoAuth)
        $params = @{
            Method             = $Method
            Uri                = "$Base$Path"
            SkipHttpErrorCheck = $true
            SkipCertificateCheck = $true
        }
        if (-not $NoAuth) { $params.Headers = $script:Headers }
        if ($Body) {
            $params.ContentType = 'application/json'
            $params.Body = $Body | ConvertTo-Json
        }
        Invoke-WebRequest @params
    }

    # PowerShell treats application/problem+json as binary, so Content comes back as byte[].
    function Get-ResponseText {
        param($Response)
        if ($Response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($Response.Content) }
        else { $Response.Content }
    }

    function New-TestItem {
        param([string]$Name = "Pester Test Item $([guid]::NewGuid().ToString('N'))")
        $response = Invoke-Api -Method Post -Path '/api/items' -Body @{
            name = $Name; calories = 100; price = 1.99; proteinGrams = 5; isBreakfast = $false
        }
        $response.Content | ConvertFrom-Json
    }
}

Describe 'Authentication' {
    It 'POST /token with valid credentials returns a bearer token' {
        $response = Invoke-RestMethod -Method Post -Uri "$Base/token" -SkipCertificateCheck -ContentType 'application/json' `
            -Body (@{ clientId = 'demo-client'; clientSecret = 'demo-secret' } | ConvertTo-Json)
        $response.tokenType | Should -Be 'Bearer'
        $response.accessToken | Should -Not -BeNullOrEmpty
    }

    It 'POST /token with bad credentials returns 401' {
        $response = Invoke-Api -Method Post -Path '/token' -NoAuth -Body @{
            clientId = 'demo-client'; clientSecret = 'wrong'
        }
        $response.StatusCode | Should -Be 401
    }

    It 'GET /api/items without a token returns 401' {
        (Invoke-Api -Path '/api/items' -NoAuth).StatusCode | Should -Be 401
    }

    It 'GET /whoami echoes the client claims' {
        $response = Invoke-Api -Path '/whoami'
        $response.StatusCode | Should -Be 200
        $response.Content | Should -Match 'demo-client'
    }
}

Describe 'GET /api/items' {
    It 'returns all items as a bare array (at least the 83 seeded)' {
        $items = (Invoke-Api -Path '/api/items').Content | ConvertFrom-Json
        $items.Count | Should -BeGreaterOrEqual 83
    }

    It 'returns a paged envelope when paging params are supplied' {
        $page = (Invoke-Api -Path '/api/items?page=2&pageSize=10').Content | ConvertFrom-Json
        $page.items.Count | Should -Be 10
        $page.page | Should -Be 2
        $page.pageSize | Should -Be 10
        $page.totalCount | Should -BeGreaterOrEqual 83
        $page.totalPages | Should -Be ([math]::Ceiling($page.totalCount / 10))
    }

    It 'rejects invalid paging params with 400: <_>' -ForEach @(
        '/api/items?page=0&pageSize=10'
        '/api/items?page=1&pageSize=0'
        '/api/items?page=1&pageSize=101'
    ) {
        (Invoke-Api -Path $_).StatusCode | Should -Be 400
    }
}

Describe 'GET /api/items/{id}' {
    It 'returns a single item' {
        $created = New-TestItem
        $item = (Invoke-Api -Path "/api/items/$($created.id)").Content | ConvertFrom-Json
        $item.name | Should -Be $created.name
        $item.calories | Should -Be 100
        $item.isBreakfast | Should -BeFalse
    }

    It 'returns 404 ProblemDetails for an unknown id' {
        $response = Invoke-Api -Path '/api/items/99999999'
        $response.StatusCode | Should -Be 404
        $response.Headers.'Content-Type' | Should -Match 'application/problem\+json'
        Get-ResponseText $response | Should -Match 'Menu item not found'
    }
}

Describe 'POST /api/items' {
    It 'creates an item: 201, Location header, persisted' {
        $name = "Pester Enchirito $([guid]::NewGuid().ToString('N'))"
        $response = Invoke-Api -Method Post -Path '/api/items' -Body @{
            name = $name; calories = 360; price = 3.69; proteinGrams = 17; isBreakfast = $false
        }
        $response.StatusCode | Should -Be 201
        $location = [string]$response.Headers.Location
        $location | Should -Not -BeNullOrEmpty

        $fetched = Invoke-RestMethod -Uri $location -Headers $Headers -SkipCertificateCheck
        $fetched.name | Should -Be $name
    }

    It 'rejects an invalid body with 400 validation ProblemDetails' {
        $response = Invoke-Api -Method Post -Path '/api/items' -Body @{
            name = ''; calories = -5; price = -1; proteinGrams = -2
        }
        $response.StatusCode | Should -Be 400
        Get-ResponseText $response | Should -Match 'Name'
    }
}

Describe 'PUT /api/items/{id}' {
    It 'updates an item: 204, change persisted' {
        $created = New-TestItem
        $response = Invoke-Api -Method Put -Path "/api/items/$($created.id)" -Body @{
            name = "$($created.name) XL"; calories = 700; price = 2.99; proteinGrams = 24; isBreakfast = $true
        }
        $response.StatusCode | Should -Be 204

        $updated = (Invoke-Api -Path "/api/items/$($created.id)").Content | ConvertFrom-Json
        $updated.name | Should -Be "$($created.name) XL"
        $updated.isBreakfast | Should -BeTrue
    }

    It 'returns 404 for an unknown id' {
        $response = Invoke-Api -Method Put -Path '/api/items/99999999' -Body @{
            name = 'Ghost Item'; calories = 1; price = 1; proteinGrams = 1; isBreakfast = $false
        }
        $response.StatusCode | Should -Be 404
    }
}

Describe 'DELETE /api/items/{id}' {
    It 'deletes an item: 204, then 404 on re-fetch' {
        $created = New-TestItem
        (Invoke-Api -Method Delete -Path "/api/items/$($created.id)").StatusCode | Should -Be 204
        (Invoke-Api -Path "/api/items/$($created.id)").StatusCode | Should -Be 404
    }

    It 'returns 404 for an unknown id' {
        (Invoke-Api -Method Delete -Path '/api/items/99999999').StatusCode | Should -Be 404
    }
}

AfterAll {
    # Best-effort cleanup: remove rows created by this run (all named "Pester ...").
    $items = (Invoke-Api -Path '/api/items').Content | ConvertFrom-Json
    foreach ($leftover in $items | Where-Object { $_.name -like 'Pester*' }) {
        Invoke-Api -Method Delete -Path "/api/items/$($leftover.id)" | Out-Null
    }
}
