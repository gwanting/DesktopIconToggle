param(
    [string]$OutputDirectory = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'DesktopIconToggle.cs'
$iconPath = Join-Path $PSScriptRoot 'assets\DesktopIconToggle.ico'
$outputPath = Join-Path $OutputDirectory 'DesktopIconToggle.exe'

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

if (Test-Path $outputPath) {
    Remove-Item -LiteralPath $outputPath
}

$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
[void]$parameters.ReferencedAssemblies.Add('System.dll')
[void]$parameters.ReferencedAssemblies.Add('Accessibility.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Drawing.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Windows.Forms.dll')
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.IncludeDebugInformation = $false
$parameters.OutputAssembly = $outputPath
$parameters.CompilerOptions = "/target:winexe /optimize+ /win32icon:`"$iconPath`""

$result = $provider.CompileAssemblyFromFile($parameters, $sourcePath)
$provider.Dispose()

if ($result.Errors.HasErrors) {
    $result.Errors | ForEach-Object { Write-Error $_.ToString() }
    throw 'Build failed.'
}

Write-Host "Build complete: $outputPath"
