# Claude Code Manager - Tailscale Start Script
Write-Host "Starting Tailscale services for Claude Code Manager..." -ForegroundColor Green

Write-Host "Publishing Web Application (HTTPS port 7073)..." -ForegroundColor Yellow
tailscale serve --bg --https=443 https+insecure://localhost:7073

Write-Host "Publishing ttyd terminals..." -ForegroundColor Yellow
tailscale serve --bg --https=7681 localhost:7681
tailscale serve --bg --https=7682 localhost:7682
tailscale serve --bg --https=7683 localhost:7683
tailscale serve --bg --https=7684 localhost:7684
tailscale serve --bg --https=7685 localhost:7685

Write-Host ""
Write-Host "Tailscale services started. Checking status:" -ForegroundColor Green
tailscale serve status

Write-Host ""
Write-Host "Getting machine URL..." -ForegroundColor Yellow
$tailscaleStatus = tailscale status --json | ConvertFrom-Json
$machineName = $tailscaleStatus.Self.DNSName
if ($machineName) {
    Write-Host "Access your application at: https://$machineName" -ForegroundColor Cyan
} else {
    Write-Host "Could not determine machine URL. Check 'tailscale status'" -ForegroundColor Red
}

Write-Host ""
Write-Host "Remote access URLs:" -ForegroundColor Green
Write-Host "  Web App: https://$machineName" -ForegroundColor Cyan
Write-Host "  Terminal 1: https://$machineName:7681" -ForegroundColor Cyan
Write-Host "  Terminal 2: https://$machineName:7682" -ForegroundColor Cyan
Write-Host "  Terminal 3: https://$machineName:7683" -ForegroundColor Cyan
Write-Host "  Terminal 4: https://$machineName:7684" -ForegroundColor Cyan
Write-Host "  Terminal 5: https://$machineName:7685" -ForegroundColor Cyan