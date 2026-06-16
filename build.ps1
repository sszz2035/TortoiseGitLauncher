$ErrorActionPreference = 'Stop'

$workspaceDotnetHome = Join-Path $PSScriptRoot '.dotnet'
$workspaceNugetHome = Join-Path $PSScriptRoot '.nuget'

New-Item -ItemType Directory -Force -Path $workspaceDotnetHome | Out-Null
New-Item -ItemType Directory -Force -Path $workspaceNugetHome | Out-Null

$env:DOTNET_CLI_HOME = $workspaceDotnetHome
$env:NUGET_PACKAGES = $workspaceNugetHome

dotnet publish .\TortoiseGitLauncher\TortoiseGitLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -o .\dist
