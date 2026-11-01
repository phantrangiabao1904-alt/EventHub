$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'EventHub\bin\EventHub.dll'))
Add-Type -ReferencedAssemblies @('System.Web', 'System.Web.Abstractions') -TypeDefinition @'
public class TestVenueFile : System.Web.HttpPostedFileBase
{
    private byte[] data;
    private int length;
    public TestVenueFile(byte[] bytes, int size) { data = bytes; length = size; }
    public override int ContentLength { get { return length; } }
    public override System.IO.Stream InputStream { get { return new System.IO.MemoryStream(data); } }
}
'@
$directory = Join-Path ([IO.Path]::GetTempPath()) ('eventhub-images-test-' + [Guid]::NewGuid().ToString('N'))
$savedImage = $null
$bytes = New-Object IO.MemoryStream
try {
    $bitmap = New-Object Drawing.Bitmap(2, 2)
    try { $bitmap.Save($bytes, [Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
    $file = New-Object TestVenueFile($bytes.ToArray(), [int]$bytes.Length)
    $url = [EventHub.Services.VenueImageStorage]::Save($file, $directory)
    $savedImage = Join-Path $directory ([IO.Path]::GetFileName($url))
    $data = [IO.File]::ReadAllBytes($savedImage)
    if ($data[0] -ne 255 -or $data[1] -ne 216) { throw 'PNG was not converted to JPEG.' }
    'PASS: PNG converted to JPEG with a generated filename.'
    foreach ($size in @(3, 4000000)) {
        $rejected = $false
        try { [void][EventHub.Services.VenueImageStorage]::Save((New-Object TestVenueFile([byte[]](1, 2, 3), $size)), $directory) }
        catch { $rejected = $true }
        if (!$rejected) { throw "Invalid upload was accepted: $size bytes." }
    }
    'PASS: Invalid image and upload exceeding 3 MB rejected.'
} finally {
    $bytes.Dispose()
    if ($savedImage -and (Test-Path -LiteralPath $savedImage)) { Remove-Item -LiteralPath $savedImage }
    if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory }
}
