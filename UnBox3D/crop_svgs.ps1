param([string]$Dir = "C:\Users\javie\OneDrive\Desktop\UNBOX3D_UNFOLDED_FILES")

$files = Get-ChildItem "$Dir\*.svg"
foreach($f in $files) {
    Write-Host "=== $($f.Name) ===" -ForegroundColor Cyan
    
    [xml]$doc = Get-Content $f.FullName
    $ns = @{svg="http://www.w3.org/2000/svg"}
    $root = $doc.DocumentElement
    
    Write-Host "  Original: width=$($root.width) height=$($root.height) viewBox=$($root.viewBox)"
    
    # Find all path elements
    $paths = $doc.SelectNodes("//svg:path", (New-Object System.Xml.XmlNamespaceManager($doc.NameTable)))
    if(-not $paths) {
        # Try without namespace
        $paths = $doc.SelectNodes("//path")
    }
    # Fallback: regex on raw text
    $text = [System.IO.File]::ReadAllText($f.FullName)
    
    $minX = [double]::MaxValue
    $minY = [double]::MaxValue
    $maxX = [double]::MinValue
    $maxY = [double]::MinValue
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
    
    if(-not $found) {
        Write-Host "  No content found, skipping" -ForegroundColor Yellow
        continue
    }
    
    # Add 20mm padding
    $pad = 20
    $minX = [Math]::Max(0, $minX - $pad)
    $minY = [Math]::Max(0, $minY - $pad)
    $maxX = $maxX + $pad
    $maxY = $maxY + $pad
    $w = $maxX - $minX
    $h = $maxY - $minY
    
    Write-Host "  Content: ($([Math]::Round($minX)),$([Math]::Round($minY)))-($([Math]::Round($maxX)),$([Math]::Round($maxY)))" -ForegroundColor Green
    Write-Host "  Cropped: $([Math]::Round($w))x$([Math]::Round($h))mm" -ForegroundColor Green
    
    # Replace viewBox and dimensions in raw text
    $text = $text -replace 'width="\d+\.?\d*mm"', "width=`"$($w.ToString('F2'))mm`""
    $text = $text -replace 'height="\d+\.?\d*mm"', "height=`"$($h.ToString('F2'))mm`""
    $text = $text -replace 'viewBox="[^"]*"', "viewBox=`"$($minX.ToString('F2')) $($minY.ToString('F2')) $($w.ToString('F2')) $($h.ToString('F2'))`""
    
    [System.IO.File]::WriteAllText($f.FullName, $text)
    Write-Host "  Saved!" -ForegroundColor Green
}
Write-Host "`nDone! Open any SVG in your browser to verify." -ForegroundColor Cyan
