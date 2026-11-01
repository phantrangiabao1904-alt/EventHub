$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'EventHub'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$app = Join-Path $tempRoot ('eventhub-web-checks-' + [Guid]::NewGuid().ToString('N'))
$database = 'EventHub_WebTests_' + [Guid]::NewGuid().ToString('N')
$baseUrl = 'http://localhost:62803'
$server = $null
$passed = 0

function Check($condition, $message) {
    if (!$condition) { throw "FAIL: $message" }
    $script:passed++
    Write-Output "PASS: $message"
}
function Page($path, $session) { Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + $path) -WebSession $session -TimeoutSec 20 }
function Token($page) { [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Post($path, $session, $body) { Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + $path) -WebSession $session -Method Post -Body $body -TimeoutSec 20 }
function Multipart($path, $session, $fields, $imageBytes) {
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object Net.Http.HttpClientHandler
    $handler.CookieContainer = $session.Cookies
    $client = New-Object Net.Http.HttpClient($handler)
    $form = New-Object Net.Http.MultipartFormDataContent
    try {
        foreach ($key in $fields.Keys) { $form.Add((New-Object Net.Http.StringContent([string]$fields[$key])), $key) }
        if ($imageBytes) {
            $file = New-Object Net.Http.ByteArrayContent(,$imageBytes)
            $file.Headers.ContentType = New-Object Net.Http.Headers.MediaTypeHeaderValue('image/png')
            $form.Add($file, 'qrFile', 'test-qr.png')
        }
        $response = $client.PostAsync(($baseUrl + $path), $form).GetAwaiter().GetResult()
        [pscustomobject]@{ Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); StatusCode = [int]$response.StatusCode }
    } finally { $form.Dispose(); $client.Dispose(); $handler.Dispose() }
}
function Login($email, $password) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Page '/Account/Login' $session
    $null = Post '/Account/Login' $session @{ __RequestVerificationToken = (Token $page); Email = $email; Password = $password }
    return $session
}

try {
    [void][IO.Directory]::CreateDirectory($app)
    foreach ($folder in @('Views', 'Areas', 'Content')) {
        $path = Join-Path $source $folder
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $app -Recurse }
    }
    [void][IO.Directory]::CreateDirectory((Join-Path $app 'bin'))
    Get-ChildItem -LiteralPath (Join-Path $source 'bin') -Filter '*.dll' -File | Copy-Item -Destination (Join-Path $app 'bin')
    Copy-Item -LiteralPath (Join-Path $source 'Global.asax') -Destination $app
    $config = [xml][IO.File]::ReadAllText((Join-Path $source 'Web.config'))
    $config.configuration.connectionStrings.add.connectionString = "Data Source=.\SQLEXPRESS;Initial Catalog=$database;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
    $config.configuration.appSettings.RemoveAttribute('file')
    foreach ($key in @('Smtp.User', 'Smtp.Password')) {
        foreach ($item in @($config.configuration.appSettings.add | Where-Object { $_.key -eq $key })) { [void]$config.configuration.appSettings.RemoveChild($item) }
        $entry = $config.CreateElement('add'); $entry.SetAttribute('key', $key); $entry.SetAttribute('value', '')
        [void]$config.configuration.appSettings.AppendChild($entry)
    }
    $config.Save((Join-Path $app 'Web.config'))
    $server = Start-Process -FilePath 'C:\Program Files\IIS Express\iisexpress.exe' -ArgumentList @('/path:"' + $app + '"', '/port:62803', '/systray:false') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $app 'iis.log') -RedirectStandardError (Join-Path $app 'iis-error.log')
    $ready = $false
    for ($i = 0; $i -lt 25; $i++) {
        try { $page = Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/Account/Login') -TimeoutSec 2; $ready = $true; break }
        catch { if ($server.HasExited) { throw 'Test IIS Express exited.' }; Start-Sleep -Milliseconds 200 }
    }
    Check $ready 'Isolated website started without SMTP credentials'
    $seedSql = [IO.File]::ReadAllText((Join-Path $root 'Database\Seed_30_Venues.sql')).Replace('USE [EventHubMvc];', ('USE [' + $database + '];'))
    $seedConnection = New-Object Data.SqlClient.SqlConnection("Data Source=.\SQLEXPRESS;Initial Catalog=$database;Integrated Security=True")
    try { $seedConnection.Open(); $seedCommand = $seedConnection.CreateCommand(); $seedCommand.CommandText = $seedSql; [void]$seedCommand.ExecuteNonQuery() } finally { $seedConnection.Dispose() }
    $client = Login 'client@eventhub.com' 'Client@123'
    $admin = Login 'admin@eventhub.com' 'Admin@123'
    Add-Type -AssemblyName System.Drawing
    $imageStream = New-Object IO.MemoryStream
    $bitmap = New-Object Drawing.Bitmap(20, 20)
    try { $bitmap.Save($imageStream, [Drawing.Imaging.ImageFormat]::Png); $qrBytes = $imageStream.ToArray() } finally { $bitmap.Dispose(); $imageStream.Dispose() }
    $dashboard = Page '/Admin/Dashboard' $admin
    Check ($dashboard.Content.Contains('defaultQrFile')) 'Dashboard renders deposit percentage and QR management'
    $settings = Multipart '/Admin/Dashboard/SaveDepositSettings' $admin @{ __RequestVerificationToken = (Token $dashboard); Percent = '40'; BankName = 'TEST-BANK'; AccountNumber = '1234567890'; AccountHolder = 'TEST RECIPIENT' } $qrBytes
    Check ($settings.StatusCode -eq 200 -and $settings.Content.Contains('/Uploads/PaymentQr/')) 'Admin uploads default QR and saves 40 percent deposit'
    Check (!$settings.Content.Contains('name="BankName"') -and !$settings.Content.Contains('name="AccountNumber"') -and !$settings.Content.Contains('name="Instructions"')) 'Default settings only ask for percentage and QR image'
    $coordinator = Login 'coordinator@eventhub.com' 'Coord@123'
    $coordinatorPage = Page '/Admin/Dashboard' $coordinator
    Check (!$coordinatorPage.Content.Contains('defaultQrFile')) 'Global QR settings are visible only to Admin'
    try { $null = Post '/Admin/Dashboard/SaveDepositSettings' $coordinator @{ __RequestVerificationToken = (Token $coordinatorPage); Percent = '10' }; throw 'Coordinator could edit global settings.' } catch {
        Check ($_.Exception.Response.StatusCode.value__ -eq 403) 'Coordinator cannot change global deposit settings'
    }
    $defaultQr = [regex]::Match($settings.Content, '/Uploads/PaymentQr/[a-f0-9]{32}\.jpg').Value
    $date = (Get-Date).Date.AddDays(20).ToString('yyyy-MM-dd')
    $page = Page ('/Bookings/Create?venueId=1&eventDate=' + $date + '&startTime=09:00&endTime=12:00') $client
    Check ($page.Content.Contains($date) -and $page.Content.Contains('service-choice')) 'Booking form renders services and preserves selected date'
    $serviceId = [regex]::Match($page.Content, 'id="Services_0__ServiceOptionId"[^>]*value="(\d+)"').Groups[1].Value
    Check (![string]::IsNullOrEmpty($serviceId)) 'Service catalog can be selected'
    $created = Post '/Bookings/Create' $client @{
        __RequestVerificationToken = (Token $page); VenueId = 1; EventDate = $date; StartTime = '09:00'; EndTime = '12:00'; GuestCount = 10;
        'Services[0].ServiceOptionId' = $serviceId; 'Services[0].Selected' = 'true'; 'Services[0].Quantity' = 2
    }
    $bookingId = [regex]::Match($created.BaseResponse.ResponseUri.AbsolutePath, '/Bookings/Details/(\d+)').Groups[1].Value
    Check (![string]::IsNullOrEmpty($bookingId) -and $created.Content.Contains('window.print()')) 'Booking creates customer detail and printable receipt'
    $page = Page ('/Venues?eventDate=' + $date + '&startTime=10:00&endTime=11:00') $client
    Check (!$page.Content.Contains('/Venues/Details/1?') -and !$page.Content.Contains('/Venues/Details/1"')) 'Overlapping venue removed from availability search'
    $page = Page ('/Venues?eventDate=' + $date + '&startTime=12:00&endTime=13:00') $client
    Check ($page.Content.Contains('/Venues/Details/1?')) 'Adjacent interval stays available'
    $page = Page ('/Admin/Bookings/Details/' + $bookingId) $admin
    Check ($page.Content.Contains('SendDepositRequest') -and !$page.Content.Contains('name="Amount"') -and !$page.Content.Contains('name="Reference"') -and !$page.Content.Contains('name="BankName"')) 'Approval form only asks for percentage and QR with no payment paperwork'
    $bypass = Post '/Admin/Bookings/ChangeStatus' $admin @{ __RequestVerificationToken = (Token $page); id = $bookingId; status = 'Confirmed'; returnUrl = '/Admin/Bookings/Details/' + $bookingId }
    Check ($bypass.Content.Contains('data-booking-status="Pending"')) 'Staff cannot directly confirm an unpaid request'
    $confirmed = Multipart '/Admin/Bookings/SendDepositRequest' $admin @{ __RequestVerificationToken = (Token $bypass); BookingId = $bookingId; Percent = '40'; BankName = 'TEST-BANK'; AccountNumber = '1234567890'; AccountHolder = 'TEST RECIPIENT'; Instructions = 'TEST-TRANSFER-INSTRUCTIONS' } $qrBytes
    Check ($confirmed.Content.Contains('data-booking-status="AwaitingDeposit"')) 'Approval plus per-booking QR changes status to awaiting deposit only'
    Check ($confirmed.Content.Contains('alert-warning')) 'Unavailable SMTP is shown as a warning instead of claiming email delivery'
    $requestQr = [regex]::Match($confirmed.Content, '/Uploads/PaymentQr/[a-f0-9]{32}\.jpg').Value
    Check ($requestQr -ne $defaultQr) 'Per-booking QR override is separate from dashboard default'
    $customerRequest = Page ('/Bookings/Details/' + $bookingId) $client
    Check ($customerRequest.Content.Contains($requestQr) -and $customerRequest.Content.Contains('EVENTHUB ' + $bookingId)) 'Customer receives QR and transfer reference on booking detail without bank API'
    $requiredDeposit = [decimal][regex]::Match($customerRequest.Content, 'data-required-deposit="([0-9.]+)"').Groups[1].Value
    $dashboard = Page '/Admin/Dashboard' $admin
    $updatedSettings = Post '/Admin/Dashboard/SaveDepositSettings' $admin @{ __RequestVerificationToken = (Token $dashboard); Percent = '45.5'; BankName = 'TEST-BANK' }
    Check ($updatedSettings.Content.Contains('value="45.50"') -or $updatedSettings.Content.Contains('value="45.5"')) 'Dashboard supports fractional deposit percentages'
    $customerRequest = Page ('/Bookings/Details/' + $bookingId) $client
    Check ($customerRequest.Content.Contains($requestQr) -and $customerRequest.Content.Contains(('data-required-deposit="' + $requiredDeposit.ToString('0', [Globalization.CultureInfo]::InvariantCulture) + '"'))) 'Updating default percentage preserves already sent QR and deposit amount'
    $paymentToken = [regex]::Match($confirmed.Content, 'name="requestId" value="([^"]+)"').Groups[1].Value
    Check ($confirmed.Content.Contains('/ConfirmDeposit') -and !$confirmed.Content.Contains('name="Amount"') -and !$confirmed.Content.Contains('name="Reference"')) 'Awaiting deposit displays one confirmation button without amount or reference inputs'
    $paid = Post '/Admin/Bookings/ConfirmDeposit' $admin @{ __RequestVerificationToken = (Token $confirmed); id = $bookingId; requestId = $paymentToken }
    Check ($paid.Content.Contains('data-booking-status="Confirmed"')) 'Single click records deposit and confirms successful booking'
    $repeated = Post '/Admin/Bookings/ConfirmDeposit' $admin @{ __RequestVerificationToken = (Token $paid); id = $bookingId; requestId = $paymentToken }
    Check (([regex]::Matches($repeated.Content, 'COC-' + $bookingId + '-')).Count -eq 1) 'Browser repost does not duplicate receipt'
    $page = Page ('/Bookings/Details/' + $bookingId) $client
    Check ($page.Content.Contains('COC-' + $bookingId + '-')) 'Customer sees recorded payment on own booking'
    try { $null = Page '/Admin/Reports' $client; throw 'Customer could access staff reports.' } catch {
        Check ($_.Exception.Response.StatusCode.value__ -eq 403) 'Customer denied access to staff reports'
    }
    $report = Page '/Admin/Reports' $admin
    Check ($report.StatusCode -eq 200) 'Staff monthly report renders'
    $exportPath = Join-Path $app 'report.xlsx'
    $now = Get-Date
    Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/Admin/Reports/Export?month=' + $now.Month + '&year=' + $now.Year) -WebSession $admin -OutFile $exportPath
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($exportPath)
    try { Check ($null -ne $zip.GetEntry('xl/worksheets/sheet2.xml')) 'HTTP export downloads a real XLSX workbook' } finally { $zip.Dispose() }
    $cancelled = Post '/Bookings/Cancel' $client @{ __RequestVerificationToken = (Token $page); id = $bookingId }
    $page = Page ('/Admin/Bookings/Details/' + $bookingId) $admin
    $paymentToken = [regex]::Match($page.Content, 'name="requestId" value="([^"]+)"').Groups[1].Value
    $refunded = Post '/Admin/Bookings/ConfirmRefund' $admin @{ __RequestVerificationToken = (Token $page); id = $bookingId; requestId = $paymentToken }
    Check ($refunded.Content.Contains('HOAN-' + $bookingId + '-')) 'One click records refund after cancellation'
    $page = Page ('/Venues?eventDate=' + $date + '&startTime=10:00&endTime=11:00') $client
    Check ($page.Content.Contains('/Venues/Details/1?')) 'Cancelled booking releases search availability'
    $page = Page ('/Bookings/Create?venueId=1&eventDate=' + $date + '&startTime=14:00&endTime=15:00') $client
    $created = Post '/Bookings/Create' $client @{ __RequestVerificationToken = (Token $page); VenueId = 1; EventDate = $date; StartTime = '14:00'; EndTime = '15:00'; GuestCount = 10 }
    $reviewBookingId = [regex]::Match($created.BaseResponse.ResponseUri.AbsolutePath, '/Bookings/Details/(\d+)').Groups[1].Value
    $page = Page ('/Admin/Bookings/Details/' + $reviewBookingId) $admin
    $approved = Multipart '/Admin/Bookings/SendDepositRequest' $admin @{ __RequestVerificationToken = (Token $page); BookingId = $reviewBookingId; Percent = '45.5'; BankName = 'TEST-BANK' } $null
    $paymentToken = [regex]::Match($approved.Content, 'name="requestId" value="([^"]+)"').Groups[1].Value
    $customerRequest = Page ('/Bookings/Details/' + $reviewBookingId) $client
    $requiredDeposit = [decimal][regex]::Match($customerRequest.Content, 'data-required-deposit="([0-9.]+)"').Groups[1].Value
    $null = Post '/Admin/Bookings/ConfirmDeposit' $admin @{ __RequestVerificationToken = (Token $approved); id = $reviewBookingId; requestId = $paymentToken }
    # Only the isolated test database is adjusted to exercise completion without waiting for an event.
    $sql = New-Object Data.SqlClient.SqlConnection("Data Source=.\SQLEXPRESS;Initial Catalog=$database;Integrated Security=True")
    try {
        $sql.Open(); $command = $sql.CreateCommand()
        $command.CommandText = 'UPDATE dbo.Bookings SET EventDate = DATEADD(day,-1,CAST(GETDATE() AS date)) WHERE Id=@id; INSERT INTO dbo.Users (FullName,Email,PasswordHash,Role,CreatedAt) SELECT N''Other Test Client'',''other@example.invalid'',PasswordHash,3,GETDATE() FROM dbo.Users WHERE Email=''client@eventhub.com'';'
        [void]$command.Parameters.AddWithValue('@id', [int]$reviewBookingId)
        [void]$command.ExecuteNonQuery()
    } finally { $sql.Dispose() }
    $page = Page ('/Admin/Bookings/Details/' + $reviewBookingId) $admin
    $null = Post '/Admin/Bookings/ChangeStatus' $admin @{ __RequestVerificationToken = (Token $page); id = $reviewBookingId; status = 'Completed'; returnUrl = '/Admin/Bookings/Details/' + $reviewBookingId }
    $page = Page ('/Bookings/Details/' + $reviewBookingId) $client
    Check ($page.Content.Contains('reviewComment')) 'Completed booking shows customer review form'
    $reviewed = Post '/Bookings/Review' $client @{ __RequestVerificationToken = (Token $page); BookingId = $reviewBookingId; Rating = 4; Comment = 'WEB-TEST-REVIEW' }
    Check ($reviewed.Content.Contains('WEB-TEST-REVIEW') -and !$reviewed.Content.Contains('id="reviewComment"')) 'Posted review appears once on customer detail'
    $public = Page '/Venues/Details/1' $client
    Check ($public.Content.Contains('WEB-TEST-REVIEW') -and !$public.Content.Contains('client@eventhub.com')) 'Public review appears without customer email'
    $other = Login 'other@example.invalid' 'Client@123'
    try { $null = Page ('/Bookings/Details/' + $reviewBookingId) $other; throw 'Another customer could read booking.' } catch {
        Check ($_.Exception.Response.StatusCode.value__ -eq 404) 'Another customer denied booking details'
    }
    try { $null = Page '/Admin/Reports?month=13&year=2026' $admin; throw 'Invalid month accepted.' } catch {
        Check ($_.Exception.Response.StatusCode.value__ -eq 400) 'Invalid report month rejected'
    }
    Write-Output "All $passed web checks passed. Application data and SMTP credentials were not used."
} finally {
    if ($server -and !$server.HasExited) { Stop-Process -Id $server.Id; [void]$server.WaitForExit(5000) }
    if ($database -match '^EventHub_WebTests_[a-f0-9]{32}$') {
        $sql = New-Object Data.SqlClient.SqlConnection('Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True')
        try {
            $sql.Open(); $command = $sql.CreateCommand()
            $command.CommandText = "IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END"
            [void]$command.Parameters.AddWithValue('@name', $database)
            [void]$command.ExecuteNonQuery()
        } finally { $sql.Dispose() }
    }
    $resolved = [IO.Path]::GetFullPath($app)
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and ([IO.Path]::GetFileName($resolved) -match '^eventhub-web-checks-[a-f0-9]{32}$')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
