param([string]$Dir = "C:\Users\javie\OneDrive\Desktop\UNBOX3D_UNFOLDED_FILES")

$files = Get-ChildItem "$Dir\*.svg"
foreach($f in $files) {
    Write-Host "=== $($f.Name) ===" -ForegroundColor Cyan
    $text = [System.IO.File]::ReadAllText($f.FullName)
    
    # Read original dimensions
    if($text -match 'width="(\d+\.?\d*)mm"') { $docW = [double]$Matches[1] } else { continue }
    if($text -match 'height="(\d+\.?\d*)mm"') { $docH = [double]$Matches[1] } else { continue }
    Write-Host "  Original: ${docW}x${docH}mm"
    
    $minX = [double]::MaxValue; $minY = [double]::MaxValue
    $maxX = [double]::MinValue; $maxY = [double]::MinValue
    $found = $false
    
    foreach($m in [regex]::Matches($text, '<path[^>]*?d="([^"]*)"[^>]*?>')) {
        if($m.Value -match 'class="sticker"') { continue }
        $d = $m.Groups[1].Value
        $coords = [regex]::Matches($d, '-?\d+\.?\d*')
        for($i = 0; $i -lt $coords.Count - 1; $i += 2) {
            $x = [double]$coords[$i].Value
            $y = [double]$coords[$i+1].Value
            $found = $true
            if($x -lt $minX) { $minX = $x }
            if($y -lt $minY) { $minY = $y }
            if($x -gt $maxX) { $maxX = $x }
            if($y -gt $maxY) { $maxY = $y }
        }
    }
    
    if(-not $found) { Write-Host "  No content, skipping" -ForegroundColor Yellow; continue }
    
    $contentW = $maxX - $minX
    $contentH = $maxY - $minY
    Write-Host "  Content: ($([Math]::Round($minX)),$([Math]::Round($minY)))-($([Math]::Round($maxX)),$([Math]::Round($maxY))) = $([Math]::Round($contentW))x$([Math]::Round($contentH))mm"
    
    # 5% padding, minimum 100mm
    $padX = [Math]::Max(100, $contentW * 0.05)
    $padY = [Math]::Max(100, $contentH * 0.05)
    
    $minX = [Math]::Max(0, $minX - $padX)
    $minY = [Math]::Max(0, $minY - $padY)
    $maxX = $maxX + $padX
    $maxY = $maxY + $padY
    $w = $maxX - $minX
    $h = $maxY - $minY
    
    Write-Host "  Cropped: $([Math]::Round($w))x$([Math]::Round($h))mm (pad $([Math]::Round($padX))x$([Math]::Round($padY)))" -ForegroundColor Green
    
    # Replace viewBox and dimensions — viewBox for viewing, width/height for print scale
    $text = $text -replace 'width="[\d.]+mm"', "width=`"$($w.ToString('F2'))mm`""
    $text = $text -replace 'height="[\d.]+mm"', "height=`"$($h.ToString('F2'))mm`""
    $text = $text -replace 'viewBox="[^"]*"', "viewBox=`"$($minX.ToString('F2')) $($minY.ToString('F2')) $($w.ToString('F2')) $($h.ToString('F2'))`""
    
    [System.IO.File]::WriteAllText($f.FullName, $text)
    Write-Host "  Saved! Open in browser to see full shape." -ForegroundColor Green
}
Write-Host "`nDone! Open any SVG in Chrome/Edge to see the full unfolded shape." -ForegroundColor Cyan
Write-Host "Print at 100% scale for real-world dimensions." -ForegroundColor Cyan
