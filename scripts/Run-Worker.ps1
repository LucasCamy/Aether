param([switch]$ControlledConsumer)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aetherRoot = Split-Path $PSScriptRoot -Parent
Push-Location $aetherRoot
$aetherNames = @('DOTNET_ENVIRONMENT','ConnectionStrings__Notifications','ConnectionStrings__Publisher','Rabbit__Host','Rabbit__Port','Rabbit__Username','Rabbit__Password','Rabbit__UseTls','Worker__PublisherEnabled','Worker__ControlledConsumerEnabled')
$aetherPrevious = @{}
foreach ($aetherName in $aetherNames) { $aetherPrevious[$aetherName] = [Environment]::GetEnvironmentVariable($aetherName) }
try {
    $aetherValues = @{}
    Get-Content -LiteralPath '.env' | ForEach-Object { if ($_ -match '^([A-Z_]+)=(.+)$') { $aetherValues[$Matches[1]] = $Matches[2] } }
    docker compose --env-file .env -f infra/compose.yaml up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Serviços locais não iniciaram.' }
    docker compose --env-file .env -f infra/compose.yaml exec -T rabbitmq rabbitmqctl set_policy aether-reliable '^comm.q.' '{"dead-letter-strategy":"at-least-once","overflow":"reject-publish","delivery-limit":20}' --apply-to quorum_queues
    if ($LASTEXITCODE -ne 0) { throw 'Política RabbitMQ falhou.' }
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__Publisher = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_publisher;Password=$($aetherValues['AETHER_DB_PUBLISHER_PASSWORD']);Include Error Detail=false"
    $env:ConnectionStrings__Notifications = "Host=127.0.0.1;Port=55432;Database=aether;Username=aether_runtime;Password=$($aetherValues['AETHER_DB_RUNTIME_PASSWORD']);Include Error Detail=false"
    $env:Rabbit__Host = '127.0.0.1'
    $env:Rabbit__Port = '55672'
    $env:Rabbit__Username = 'aether'
    $env:Rabbit__Password = $aetherValues['AETHER_RABBIT_PASSWORD']
    $env:Rabbit__UseTls = 'false'
    $env:Worker__PublisherEnabled = 'true'
    $env:Worker__ControlledConsumerEnabled = $ControlledConsumer.IsPresent.ToString()
    if ($ControlledConsumer) { Write-Output 'Modo controlado: marca aceitação sintética; nenhum E-mail será enviado.' }
    dotnet run --project src/Hosts/Aether.Worker -c Release --no-build
}
finally {
    foreach ($aetherName in $aetherNames) { [Environment]::SetEnvironmentVariable($aetherName, $aetherPrevious[$aetherName]) }
    Pop-Location
}
