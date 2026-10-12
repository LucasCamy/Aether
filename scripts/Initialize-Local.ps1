Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aetherRoot = Split-Path $PSScriptRoot -Parent
$aetherEnvPath = Join-Path $aetherRoot '.env'
if (Test-Path -LiteralPath $aetherEnvPath) { throw '.env já existe; não será sobrescrito.' }
$aetherAdmin = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$aetherRuntime = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$aetherLines = @("AETHER_DB_ADMIN_PASSWORD=$aetherAdmin", "AETHER_DB_RUNTIME_PASSWORD=$aetherRuntime")
$aetherLines += "AETHER_DB_PUBLISHER_PASSWORD=$([Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))"
$aetherLines += "AETHER_RABBIT_PASSWORD=$([Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))"
$aetherLines += "AETHER_REDIS_PASSWORD=$([Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))"
[System.IO.File]::WriteAllLines($aetherEnvPath, $aetherLines)
Write-Output 'Credenciais locais geradas em .env (ignorado pelo Git).'
