# Claude Code Manager - Tailscale Stop Script
Write-Host "Stopping all Tailscale services..." -ForegroundColor Red

tailscale serve reset

Write-Host ""
Write-Host "All Tailscale services stopped." -ForegroundColor Green
Write-Host "Current serve status:" -ForegroundColor Yellow
tailscale serve status