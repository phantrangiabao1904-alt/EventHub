$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'EventHub\bin'
$compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('eventhub-feature-checks-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDirectory)
try {
    Get-ChildItem -LiteralPath $bin -Filter '*.dll' -File | Copy-Item -Destination $testDirectory
    $arguments = @('/nologo', '/target:exe', ('/out:' + (Join-Path $testDirectory 'FeatureChecks.exe')),
        '/reference:System.Data.dll', '/reference:System.Xml.dll', '/reference:System.Xml.Linq.dll',
        '/reference:System.IO.Compression.dll', '/reference:System.ComponentModel.DataAnnotations.dll')
    foreach ($assembly in @('EventHub.dll', 'EntityFramework.dll', 'EntityFramework.SqlServer.dll')) {
        $arguments += '/reference:' + (Join-Path $testDirectory $assembly)
    }
    $arguments += Join-Path $PSScriptRoot 'FeatureChecks.cs'
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Feature test compilation failed.' }
    & (Join-Path $testDirectory 'FeatureChecks.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Feature checks failed.' }
} finally {
    # Remove only individual files created in this fresh temporary directory.
    Get-ChildItem -LiteralPath $testDirectory -File | Remove-Item
    Remove-Item -LiteralPath $testDirectory
}
