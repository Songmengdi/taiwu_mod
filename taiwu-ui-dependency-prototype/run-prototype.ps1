$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameModRoot = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod'
$providerProject = Join-Path $root 'provider\TaiwuUiDependencyProvider.csproj'
$consumerProject = Join-Path $root 'consumer\TaiwuUiDependencyConsumer.csproj'
$harnessProject = Join-Path $root 'harness\TaiwuUiDependencyHarness.csproj'

dotnet build $providerProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $consumerProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $harnessProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$providerTarget = Join-Path $gameModRoot 'ZZZ_TaiwuUiDependencyProvider'
$consumerTarget = Join-Path $gameModRoot 'AAA_TaiwuUiDependencyConsumer'
$providerPlugins = Join-Path $providerTarget 'Plugins'
$consumerPlugins = Join-Path $consumerTarget 'Plugins'

New-Item -ItemType Directory -Force -Path $providerPlugins | Out-Null
New-Item -ItemType Directory -Force -Path $consumerPlugins | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'provider\Config.lua') -Destination (Join-Path $providerTarget 'Config.lua') -Force
Copy-Item -LiteralPath (Join-Path $root 'consumer\Config.lua') -Destination (Join-Path $consumerTarget 'Config.lua') -Force
Copy-Item -LiteralPath (Join-Path $root 'provider\mod\Plugins\TaiwuUi.DependencyPrototype.Provider.dll') -Destination $providerPlugins -Force
Copy-Item -LiteralPath (Join-Path $root 'consumer\mod\Plugins\TaiwuUi.DependencyPrototype.Consumer.dll') -Destination $consumerPlugins -Force

$unexpectedProvider = Join-Path $consumerPlugins 'TaiwuUi.DependencyPrototype.Provider.dll'
if (Test-Path -LiteralPath $unexpectedProvider) {
    Remove-Item -LiteralPath $unexpectedProvider -Force
}

Write-Host 'Dependency prototype built and installed.'
Write-Host "Provider: $providerTarget"
Write-Host "Consumer: $consumerTarget"
Write-Host 'Enable both MODs. The consumer dependency should force the provider to load first.'

