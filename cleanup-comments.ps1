# Script to remove XML documentation comments from C# files

$files = Get-ChildItem -Path "." -Filter "*.cs" -Recurse | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and 
    $_.FullName -notmatch '\\bin\\' 
}

$totalFiles = 0
$modifiedFiles = 0

foreach ($file in $files) {
    $totalFiles++
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Remove XML documentation comment blocks
    $content = $content -replace '(?m)^\s*///\s*<summary>.*?^\s*///\s*</summary>\r?\n', ''
    
    # Remove single-line XML doc comments
    $content = $content -replace '(?m)^\s*///\s*<param[^>]*>.*?</param>\r?\n', ''
    $content = $content -replace '(?m)^\s*///\s*<returns>.*?</returns>\r?\n', ''
    $content = $content -replace '(?m)^\s*///\s*<exception[^>]*>.*?</exception>\r?\n', ''
    $content = $content -replace '(?m)^\s*///\s*<remarks>.*?</remarks>\r?\n', ''
    
    # Remove any remaining standalone /// lines
    $content = $content -replace '(?m)^\s*///\s*\r?\n', ''
    
    # Clean up multiple consecutive blank lines
    $content = $content -replace '(\r?\n){4,}', "`r`n`r`n`r`n"
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $modifiedFiles++
        Write-Host "Cleaned: $($file.FullName)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "Total files processed: $totalFiles"
Write-Host "Files modified: $modifiedFiles" -ForegroundColor Green
