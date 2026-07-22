param(
    [string]$Suffix = (Get-Date -Format 'yyyyMMdd-HHmmss')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$safeSuffix = $Suffix -replace '[^A-Za-z0-9_-]', '-'
$assemblySuffix = $safeSuffix -replace '[^A-Za-z0-9_]', '_'
$hotRoot = Join-Path $root "dev\hot-$safeSuffix"
$provider = Join-Path $hotRoot 'provider'
$consumer = Join-Path $hotRoot 'consumer'
$coreProject = Join-Path $root 'TaiwuUi.Core.csproj'
$sampleProject = Join-Path $root 'sample\TaiwuUi.Sample.csproj'
$coreAssembly = "TaiwuUi.Core.Hot$assemblySuffix"
$sampleAssembly = "TaiwuUi.Sample.Hot$assemblySuffix"
$coreDll = Join-Path $provider "$coreAssembly.dll"

New-Item -ItemType Directory -Force -Path $provider, $consumer | Out-Null

dotnet build $coreProject -c Release `
    -p:AssemblyName=$coreAssembly `
    -p:OutputPath="$provider\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build $sampleProject -c Release `
    -p:AssemblyName=$sampleAssembly `
    -p:OutputPath="$consumer\" `
    -p:CoreDll="$coreDll"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Hot provider: $coreDll"
Write-Host "Hot consumer: $(Join-Path $consumer "$sampleAssembly.dll")"
