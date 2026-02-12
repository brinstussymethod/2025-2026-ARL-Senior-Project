# UnBox3D Developer Environment Setup Script
# Run this in PowerShell after cloning the repo

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  UnBox3D Developer Environment Check" -ForegroundColor Cyan  
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# 1. Check Blender 4.2
Write-Host "[1/3] Checking Blender 4.2 LTS..." -ForegroundColor Yellow
$blender42 = "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
if (Test-Path $blender42) {
    Write-Host "  OK - Blender 4.2 found" -ForegroundColor Green
} else {
    Write-Host "  MISSING - Blender 4.2 LTS not installed" -ForegroundColor Red
    Write-Host "  Download from: https://www.blender.org/download/lts/4-2/" -ForegroundColor White
    $allGood = $false
}

# 2. Check Export Paper Model addon
Write-Host "[2/3] Checking Export Paper Model addon..." -ForegroundColor Yellow
$addonPath = "$env:APPDATA\Blender Foundation\Blender\4.2\extensions\user_default\export_paper_model"
if (Test-Path $addonPath) {
    Write-Host "  OK - Addon installed" -ForegroundColor Green
    
    # 3. Check if patched
    Write-Host "[3/3] Checking addon patch..." -ForegroundColor Yellow
    $unfolderPy = Join-Path $addonPath "unfolder.py"
    if (Test-Path $unfolderPy) {
        $content = Get-Content $unfolderPy -Raw
        if ($content -match "edge\.angle or 0") {
            Write-Host "  OK - Addon is patched" -ForegroundColor Green
        } else {
            Write-Host "  PATCHING - Applying fix for flat geometry bug..." -ForegroundColor Yellow
            $content = $content.Replace(
                'balance = sum((+1 if edge.angle > 0 else -1) for edge in island_edges)',
                'balance = sum((+1 if (edge.angle or 0) > 0 else -1) for edge in island_edges)'
            )
            Set-Content $unfolderPy -Value $content -NoNewline
            Write-Host "  OK - Patch applied successfully" -ForegroundColor Green
        }
    }
} else {
    Write-Host "  MISSING - Addon not installed" -ForegroundColor Red
    Write-Host "  Open Blender 4.2 -> Edit -> Preferences -> Get Extensions -> search 'Export Paper Model' -> Install" -ForegroundColor White
    $allGood = $false
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
if ($allGood) {
    Write-Host "  All checks passed! Ready to build." -ForegroundColor Green
} else {
    Write-Host "  Some items need attention (see above)" -ForegroundColor Red
}
Write-Host "============================================" -ForegroundColor Cyan
