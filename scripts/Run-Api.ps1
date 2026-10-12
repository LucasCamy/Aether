Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aetherRoot = Split-Path $PSScriptRoot -Parent
Push-Location $aetherRoot
$aetherNames = @('ASPNETCORE_ENVIRONMENT','ConnectionStrings__Notifications','Auth__Issuer','Auth__Audience','Auth__PublicKeyPem')
$aetherPrevious = @{}
foreach ($aetherName in $aetherNames) { $aetherPrevious[$aetherName] = [Environment]::GetEnvironmentVariable($aetherName) }
try {
    $aetherValues = @{}
    Get-Content -LiteralPath '.env' | ForEach-Object { if ($_ -match '^([A-Z_]+)=(.+)$') { $aetherValues[$Matches[1]] = $Matches[2] } }
    docker compose --env-file .env -f infra/compose.yaml up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Serviços locais não iniciaram.' }
    docker compose --env-file .env -f infra/compose.yaml exec -T postgres sh /docker-entrypoint-initdb.d/01-runtime.sh
    if ($LASTEXITCODE -ne 0) { throw 'Provisionamento local falhou.' }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__Notifications = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_migrator;Password=$($aetherValues['AETHER_DB_ADMIN_PASSWORD']);Include Error Detail=false"
    dotnet run --project src/Hosts/Aether.Api -c Release --no-build -- --migrate
    if ($LASTEXITCODE -ne 0) { throw 'Migrations falharam.' }
    New-Item -ItemType Directory -Path '.local' -Force | Out-Null
    if (-not (Test-Path -LiteralPath '.local/tenant-id')) {
        [IO.File]::WriteAllText((Join-Path $aetherRoot '.local/tenant-id'), [Guid]::NewGuid().ToString('D'))
    }
    $aetherTenant = [Guid]::Parse((Get-Content -LiteralPath '.local/tenant-id' -Raw))
    "INSERT INTO tenancy.tenants(id,status) VALUES (:'tenant_id'::uuid,'active') ON CONFLICT DO NOTHING;" |
        docker compose --env-file .env -f infra/compose.yaml exec -T postgres psql -U aether_migrator -d aether -v ON_ERROR_STOP=1 -v "tenant_id=$aetherTenant"
    if ($LASTEXITCODE -ne 0) { throw 'Tenant local não foi provisionado.' }
    $aetherRsa = [Security.Cryptography.RSA]::Create(2048)
    function ConvertTo-AetherBase64Url([byte[]]$Bytes) { [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+','-').Replace('/','_') }
    $aetherNow = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $aetherHeader = ConvertTo-AetherBase64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"RS256","typ":"JWT"}'))
    $aetherPayload = @{ iss='https://aether.local'; aud='aether-local'; sub='local-developer'; tenant_id=$aetherTenant.ToString('D'); permission=@('notifications:send','notifications:read'); iat=$aetherNow; nbf=$aetherNow-30; exp=$aetherNow+3600 } | ConvertTo-Json -Compress
    $aetherUnsigned = $aetherHeader + '.' + (ConvertTo-AetherBase64Url ([Text.Encoding]::UTF8.GetBytes($aetherPayload)))
    $aetherSignature = $aetherRsa.SignData([Text.Encoding]::UTF8.GetBytes($aetherUnsigned), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    [IO.File]::WriteAllText((Join-Path $aetherRoot '.local/access-token'), $aetherUnsigned + '.' + (ConvertTo-AetherBase64Url $aetherSignature))
    $env:Auth__Issuer = 'https://aether.local'
    $env:Auth__Audience = 'aether-local'
    $env:Auth__PublicKeyPem = $aetherRsa.ExportSubjectPublicKeyInfoPem()
    $aetherRsa.Dispose()
    $env:ConnectionStrings__Notifications = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_runtime;Password=$($aetherValues['AETHER_DB_RUNTIME_PASSWORD']);Include Error Detail=false"
    Write-Output 'API local: http://127.0.0.1:5080. Token em .local/access-token, válido por uma hora; não compartilhar.'
    dotnet run --project src/Hosts/Aether.Api -c Release --no-build --urls http://127.0.0.1:5080
}
finally {
    foreach ($aetherName in $aetherNames) { [Environment]::SetEnvironmentVariable($aetherName, $aetherPrevious[$aetherName]) }
    Pop-Location
}
