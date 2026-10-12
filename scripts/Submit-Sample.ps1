Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aetherRoot = Split-Path $PSScriptRoot -Parent
$aetherTenant = [Guid]::Parse((Get-Content -LiteralPath (Join-Path $aetherRoot '.local/tenant-id') -Raw))
$aetherToken = Get-Content -LiteralPath (Join-Path $aetherRoot '.local/access-token') -Raw
$aetherHeaders = @{
    Authorization = "Bearer $aetherToken"
    'X-Tenant-ID' = $aetherTenant.ToString('D')
    'X-Correlation-ID' = [Guid]::NewGuid().ToString('D')
    'X-Idempotency-Key' = [Guid]::NewGuid().ToString('D')
}
$aetherBody = @{
    priority='transactional'; deliveryMode='fan_out'; recipient=@{email='synthetic@example.com'}
    messages=@(@{channel='email';content=@{subject='Aether local';text='Mensagem sintética de validação.'}})
    expiresAt=[DateTimeOffset]::UtcNow.AddHours(1).ToString('O')
} | ConvertTo-Json -Depth 8 -Compress
Invoke-RestMethod -Uri 'http://127.0.0.1:5080/v1/notifications' -Method Post -Headers $aetherHeaders -ContentType 'application/json' -Body $aetherBody
