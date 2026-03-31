# build-actions-local.ps1
param([string]$Version = "0.59.1+lcb.21.2-QuickyEnd-DF")

Write-Host "=== BetterGI Actions-like Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# 1. 发布
dotnet publish BetterGenshinImpact/BetterGenshinImpact.csproj `
    -c Release `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    --runtime win-x64

# 2. 查找发布目录
$sourceDir = Get-ChildItem -Path "BetterGenshinImpact\bin" -Recurse -Directory -Filter "publish" | 
    Where-Object { $_.FullName -like "*Release*" } |
    Select-Object -First 1 -ExpandProperty FullName

# 3. 清理发布目录
if ($sourceDir) {
    Get-ChildItem -Path $sourceDir -Recurse -Filter "*.lib" | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $sourceDir -Recurse -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $sourceDir -Recurse -Filter "*ffmpeg*.dll" | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 4. 复制到 dist
$distDir = "dist\BetterGI"
if (Test-Path $distDir) { Remove-Item -Path $distDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -Path $distDir -ItemType Directory -Force | Out-Null
if ($sourceDir) {
    Copy-Item -Path "$sourceDir\*" -Destination $distDir -Recurse -Force
}

# 5. 强制清理临时目录（修正版）
Write-Host "`nCleaning temporary directories..." -ForegroundColor Cyan

# 使用 cmd /c 执行 taskkill
# cmd /c "taskkill /f /im dotnet.exe 2>nul"
Start-Sleep -Seconds 2

# 需要清理的目录列表
# $cleanDirs = @(
#     "Fischless.WindowsInput\bin",
#     "Fischless.WindowsInput\obj",
#     "Fischless.HotkeyCapture\bin",
#     "Fischless.HotkeyCapture\obj",
#     "Fischless.GameCapture\bin",
#     "Fischless.GameCapture\obj",
#     "BetterGenshinImpact\bin",
#     "BetterGenshinImpact\obj"
# )

foreach ($dir in $cleanDirs) {
    if (Test-Path $dir) {
        cmd /c "rmdir /s /q $dir 2>nul"
        Write-Host "  Removed: $dir" -ForegroundColor Green
    } else {
        Write-Host "  Not found: $dir" -ForegroundColor Gray
    }
}

Write-Host "Cleaned!" -ForegroundColor Green

# 6. 打包
Write-Host "`nCreating archive..." -ForegroundColor Cyan
Push-Location dist
if (Get-Command 7z -ErrorAction SilentlyContinue) {
    7z a "BetterGI_v$Version.7z" BetterGI -t7z -mx=5 -r -y
    $archiveType = "7z"
} else {
    Compress-Archive -Path "BetterGI/*" -DestinationPath "BetterGI_v$Version.zip" -Force
    $archiveType = "zip"
}
Pop-Location

# 7. 打印输出位置
Write-Host "`n=== Output Locations ===" -ForegroundColor Cyan
Write-Host "📁 Publish directory: $sourceDir" -ForegroundColor Yellow
Write-Host "📁 Dist directory: $distDir" -ForegroundColor Yellow

# 查找生成的压缩包
$archiveFile = Get-ChildItem -Path "dist" -Filter "BetterGI_v*.$archiveType" | Select-Object -First 1
if ($archiveFile) {
    $archiveSize = [math]::Round($archiveFile.Length / 1MB, 2)
    Write-Host "📦 Archive: $($archiveFile.FullName) ($archiveSize MB)" -ForegroundColor Green
}

# 打印 exe 文件位置
$exeFile = Get-ChildItem -Path $distDir -Filter "*.exe" | Select-Object -First 1
if ($exeFile) {
    $exeSize = [math]::Round($exeFile.Length / 1MB, 2)
    Write-Host "🚀 Executable: $($exeFile.FullName) ($exeSize MB)" -ForegroundColor Green
}

# 打印文件统计
$fileCount = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "📄 Total files in dist: $fileCount" -ForegroundColor Cyan

Write-Host "`n✅ Build completed successfully!" -ForegroundColor Green