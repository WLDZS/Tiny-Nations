$previewRoot = $PSScriptRoot
$pythonCommand = Get-Command python -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $pythonCommand) {
    Write-Host 'Python not found. Open index.html for visuals and play the WAV files directly.'
    return
}
$previewPort = 8767
while (Get-NetTCPConnection -LocalPort $previewPort -State Listen -ErrorAction SilentlyContinue) {
    $previewPort++
}
$previewProcess = Start-Process -FilePath $pythonCommand.Source -ArgumentList @('-m', 'http.server', "$previewPort", '--bind', '127.0.0.1') -WorkingDirectory $previewRoot -WindowStyle Hidden -PassThru
Start-Process "http://127.0.0.1:$previewPort/"
Write-Host "Preview server PID: $($previewProcess.Id). To stop: Stop-Process -Id $($previewProcess.Id)"
