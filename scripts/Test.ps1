Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aetherRoot = Split-Path $PSScriptRoot -Parent
Push-Location $aetherRoot
try {
    $aetherValues = @{}
    Get-Content -LiteralPath '.env' | ForEach-Object {
        if ($_ -match '^([A-Z_]+)=(.+)$') { $aetherValues[$Matches[1]] = $Matches[2] }
    }
    docker compose --env-file .env -f infra/compose.yaml up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL local não iniciou.' }
    docker compose --env-file .env -f infra/compose.yaml exec -T postgres sh /docker-entrypoint-initdb.d/01-runtime.sh
    if ($LASTEXITCODE -ne 0) { throw 'Provisionamento de roles falhou.' }
    docker compose --env-file .env -f infra/compose.yaml exec -T rabbitmq rabbitmqctl set_policy aether-reliable '^comm.q.' '{"dead-letter-strategy":"at-least-once","overflow":"reject-publish","delivery-limit":20}' --apply-to quorum_queues
    if ($LASTEXITCODE -ne 0) { throw 'Política RabbitMQ falhou.' }
    $env:AETHER_TEST_ADMIN = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_migrator;Password=$($aetherValues['AETHER_DB_ADMIN_PASSWORD']);Include Error Detail=false"
    $env:AETHER_TEST_RUNTIME = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_runtime;Password=$($aetherValues['AETHER_DB_RUNTIME_PASSWORD']);Include Error Detail=false"
    $env:AETHER_TEST_PUBLISHER = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_publisher;Password=$($aetherValues['AETHER_DB_PUBLISHER_PASSWORD']);Include Error Detail=false"
    $env:AETHER_TEST_RABBIT_PASSWORD = $aetherValues['AETHER_RABBIT_PASSWORD']
    $env:AETHER_TEST_REDIS_PASSWORD = $aetherValues['AETHER_REDIS_PASSWORD']
    dotnet test Aether.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes.' }
}
finally {
    Remove-Item Env:AETHER_TEST_ADMIN -ErrorAction SilentlyContinue
    Remove-Item Env:AETHER_TEST_RUNTIME -ErrorAction SilentlyContinue
    Remove-Item Env:AETHER_TEST_PUBLISHER -ErrorAction SilentlyContinue
    Remove-Item Env:AETHER_TEST_RABBIT_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:AETHER_TEST_REDIS_PASSWORD -ErrorAction SilentlyContinue
    Pop-Location
}
