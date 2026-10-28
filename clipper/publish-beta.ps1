param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string[]]$Runtime = @('win-x64', 'win-arm64')
)

$ErrorActionPreference = 'Stop'
[xml]$project = Get-Content (Join-Path $PSScriptRoot 'clipper.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
foreach ($rid in $Runtime) {
    # A fresh directory prevents stale files from entering the release archive.
    $output = Join-Path $artifacts ("publish-{0}-{1}" -f $rid, [guid]::NewGuid())
    dotnet publish (Join-Path $PSScriptRoot 'clipper.csproj') -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $rid" }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RELEASE.md') -Destination (Join-Path $output 'README.md')
    $archive = Join-Path $artifacts "clipper-$version-$rid.zip"
    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
    Get-FileHash -LiteralPath $archive -Algorithm SHA256
}
