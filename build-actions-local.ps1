param([string]$Version = "0.60.1+lcb.21.5-QuickyEnd-fix1-DF3.2")

Write-Host "=== BetterGI 本地构建脚本 ===" -ForegroundColor Cyan
Write-Host "版本: $Version" -ForegroundColor Yellow

# 计时开始
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# 1. 强制清理临时目录
Write-Host "`n[1/8] 清理临时目录..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

$cleanDirs = @(
    "Fischless.WindowsInput\bin",
    "Fischless.WindowsInput\obj",
    "Fischless.HotkeyCapture\bin",
    "Fischless.HotkeyCapture\obj",
    "Fischless.GameCapture\bin",
    "Fischless.GameCapture\obj",
    "BetterGenshinImpact\bin",
    "BetterGenshinImpact\obj"
)

foreach ($dir in $cleanDirs) {
    if (Test-Path $dir) {
        cmd /c "rmdir /s /q $dir 2>nul"
        Write-Host "  已删除: $dir" -ForegroundColor Green
    } else {
        Write-Host "  未找到: $dir" -ForegroundColor Gray
    }
}

# 2. 发布
Write-Host "`n[2/8] 开始编译..." -ForegroundColor Cyan
$compileStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# dotnet clean
# dotnet publish BetterGenshinImpact/BetterGenshinImpact.csproj `
#     -c Release `
#     -p:Version=$Version `
#     -p:PublishSingleFile=true `
#     -p:EnableCompressionInSingleFile=true `
#     -p:DebugType=none `
#     -p:DebugSymbols=false `
#     --runtime win-x64
dotnet publish BetterGenshinImpact/BetterGenshinImpact.csproj `
    -c Release `
    -p:PublishProfile=FolderProfile `
    -p:Version=$Version `

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 编译失败，退出代码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

$compileStopwatch.Stop()
$compileTime = $compileStopwatch.Elapsed
Write-Host "✅ 编译完成，耗时: $($compileTime.ToString('hh\:mm\:ss'))" -ForegroundColor Green

# 3. 查找发布目录
Write-Host "`n[3/8] 查找发布目录..." -ForegroundColor Cyan
$publishDir = Get-ChildItem -Path "BetterGenshinImpact\bin" -Recurse -Directory -Filter "publish" | 
    Where-Object { $_.FullName -like "*Release*" } |
    Select-Object -First 1 -ExpandProperty FullName
$sourceDir = Join-Path $publishDir "win-x64"
if ($sourceDir) {
    Write-Host "✅ 找到发布目录: $sourceDir" -ForegroundColor Green
} else {
    Write-Host "❌ 未找到发布目录" -ForegroundColor Red
    exit 1
}

# 4. 清理发布目录
Write-Host "`n[4/8] 清理发布目录..." -ForegroundColor Cyan
if ($sourceDir) {
    $beforeSize = (Get-ChildItem -Path $sourceDir -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    
    # 定义要清理的文件类型
    $libFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*.lib" 
    $pdbFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*.pdb"
    $ffmpegFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*ffmpeg*.dll"
    
    # 执行删除
    $libFiles | Remove-Item -Force -ErrorAction SilentlyContinue
    $pdbFiles | Remove-Item -Force -ErrorAction SilentlyContinue
    $ffmpegFiles | Remove-Item -Force -ErrorAction SilentlyContinue
    $cor3Files | Remove-Item -Force -ErrorAction SilentlyContinue
    
    $afterSize = (Get-ChildItem -Path $sourceDir -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    $savedSize = $beforeSize - $afterSize
    
    Write-Host "✅ 已移除: $($libFiles.Count) 个 .lib 文件" -ForegroundColor Green
    Write-Host "✅ 已移除: $($pdbFiles.Count) 个 .pdb 文件" -ForegroundColor Green
    Write-Host "✅ 已移除: $($ffmpegFiles.Count) 个 ffmpeg dll 文件" -ForegroundColor Green
    Write-Host "💾 清理节省空间: $([math]::Round($savedSize, 2)) MB" -ForegroundColor Cyan
}

# 5. 复制到 dist
Write-Host "`n[5/8] 复制到分发目录..." -ForegroundColor Cyan
$distDir = "dist\BetterGI"
if (Test-Path $distDir) { 
    Remove-Item -Path $distDir -Recurse -Force -ErrorAction SilentlyContinue 
}
New-Item -Path $distDir -ItemType Directory -Force | Out-Null
if ($sourceDir) {
    Copy-Item -Path "$sourceDir\*" -Destination $distDir -Recurse -Force
    Write-Host "✅ 已复制到: $distDir" -ForegroundColor Green
}

# 6. 清理不必要的文件
Write-Host "`n[6/8] 清理临时目录..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

$cleanDirs = @(
    "Fischless.WindowsInput\bin",
    "Fischless.WindowsInput\obj",
    "Fischless.HotkeyCapture\bin",
    "Fischless.HotkeyCapture\obj",
    "Fischless.GameCapture\bin",
    "Fischless.GameCapture\obj",
    "BetterGenshinImpact\bin",
    "BetterGenshinImpact\obj"
)

foreach ($dir in $cleanDirs) {
    if (Test-Path $dir) {
        cmd /c "rmdir /s /q $dir 2>nul"
        Write-Host "  已删除: $dir" -ForegroundColor Green
    } else {
        Write-Host "  未找到: $dir" -ForegroundColor Gray
    }
}
dotnet restore BetterGenshinImpact/BetterGenshinImpact.csproj

# 7. 打包
Write-Host "`n[7/8] 开始打包..." -ForegroundColor Cyan
$packageStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Push-Location dist
if (Get-Command 7z -ErrorAction SilentlyContinue) {
    Write-Host "使用 7z 进行压缩..." -ForegroundColor Yellow
    7z a "BetterGI_v$Version.7z" BetterGI -t7z -mx=5 -r -y
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️ 7z 压缩可能出现问题，退出代码: $LASTEXITCODE" -ForegroundColor Yellow
    }
    $archiveType = "7z"
} else {
    Write-Host "使用 PowerShell Compress-Archive 进行压缩..." -ForegroundColor Yellow
    Compress-Archive -Path "BetterGI" -DestinationPath "BetterGI_v$Version.zip" -Force
    $archiveType = "zip"
}
Pop-Location

$packageStopwatch.Stop()
$packageTime = $packageStopwatch.Elapsed
Write-Host "✅ 打包完成，耗时: $($packageTime.ToString('hh\:mm\:ss'))" -ForegroundColor Green

# 计时结束
$totalStopwatch.Stop()
$totalTime = $totalStopwatch.Elapsed

# 8. 输出统计信息
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "           构 建 统 计                  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "⏱️  编译时间: $($compileTime.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Yellow
Write-Host "⏱️  打包时间: $($packageTime.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Yellow
Write-Host "⏱️  其他操作: $($totalTime.Subtract($compileTime).Subtract($packageTime).ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Gray
Write-Host "----------------------------------------" -ForegroundColor Cyan
Write-Host "⏱️  总耗时:   $($totalTime.ToString('hh\:mm\:ss\.fff'))" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n=== 输出位置 ===" -ForegroundColor Cyan
Write-Host "📁 发布目录: $sourceDir" -ForegroundColor Yellow
Write-Host "📁 分发目录: $distDir" -ForegroundColor Yellow

# 查找生成的压缩包
$archiveFile = Get-ChildItem -Path "dist" -Filter "BetterGI_v*.$archiveType" | Select-Object -First 1
if ($archiveFile) {
    $archiveSize = [math]::Round($archiveFile.Length / 1MB, 2)
    Write-Host "📦 压缩包: $($archiveFile.FullName) ($archiveSize MB)" -ForegroundColor Green
}

# 打印 exe 文件位置
$exeFile = Get-ChildItem -Path $distDir -Filter "*.exe" | Select-Object -First 1
if ($exeFile) {
    $exeSize = [math]::Round($exeFile.Length / 1MB, 2)
    Write-Host "🚀 可执行文件: $($exeFile.FullName) ($exeSize MB)" -ForegroundColor Green
}

# 打印文件统计
$fileCount = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "📄 文件总数: $fileCount" -ForegroundColor Cyan

Write-Host "`n✅ 构建成功完成!" -ForegroundColor Green