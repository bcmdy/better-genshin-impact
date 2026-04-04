param(
    [string]$Version = "0.59.2+lcb.21.1-QuickyEnd-DF2-f2",
    [string]$KachinaChannel = "release",
    [switch]$SkipClone,
    [switch]$SkipBuild,
    [switch]$SkipPackage
)

# 颜色输出函数
function Write-Step { param([string]$Message) Write-Host "[步骤] $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "ℹ️ $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "❌ $Message" -ForegroundColor Red }

Write-Host "=== BetterGI 自动化构建脚本 ===" -ForegroundColor Cyan
Write-Host "版本: $Version" -ForegroundColor Yellow
Write-Host "Kachina 通道: $KachinaChannel" -ForegroundColor Yellow

# 计时开始
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# ============================================
# 1. 克隆资源仓库
# ============================================
if (-not $SkipClone) {
    Write-Step "开始克隆资源仓库..."

    # 临时目录
    $tempDir = ".build_temp"
    if (Test-Path $tempDir) { Remove-Item -Path $tempDir -Recurse -Force }
    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

    # 1.1 克隆 map editor web
    $mapEditorDir = "$tempDir\map-editor"
    if (-not (Test-Path $mapEditorDir)) {
        Write-Info "克隆 bettergi-map (web map editor)..."
        git clone --depth 1 https://github.com/huiyadanli/bettergi-map.git $mapEditorDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "克隆 bettergi-map 失败"
            exit 1
        }
        Push-Location $mapEditorDir
        npm install
        npm run build:single
        Pop-Location
    }

    # 1.2 克隆 scripts list web
    $scriptsListDir = "$tempDir\scripts-list"
    if (-not (Test-Path $scriptsListDir)) {
        Write-Info "克隆 bettergi-script-web (scripts list)..."
        git clone --depth 1 https://github.com/zaodonganqi/bettergi-script-web.git $scriptsListDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "克隆 bettergi-script-web 失败"
            exit 1
        }
        Push-Location $scriptsListDir
        npm install
        npm run build:single
        Pop-Location
    }

    # 1.3 克隆 publish 仓库
    $publishDir = "$tempDir\publish"
    if (-not (Test-Path $publishDir)) {
        Write-Info "克隆 bettergi-publish (资源文件)..."
        git clone --depth 1 https://github.com/babalae/bettergi-publish.git $publishDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "克隆 bettergi-publish 失败"
            exit 1
        }
    }

    # 复制资源到 dist 目录
    $distDir = "dist\BetterGI"
    $assetsDir = "$distDir\Assets"

    # 创建目录结构
    New-Item -Path "$assetsDir\Map\Editor" -ItemType Directory -Force | Out-Null
    New-Item -Path "$assetsDir\Web\ScriptRepo" -ItemType Directory -Force | Out-Null
    New-Item -Path "$assetsDir\Map" -ItemType Directory -Force | Out-Null

    # 复制 map editor
    if (Test-Path "$mapEditorDir\dist") {
        Copy-Item -Path "$mapEditorDir\dist\*" -Destination "$assetsDir\Map\Editor" -Recurse -Force
        Write-Success "已复制 Map Editor 资源"
    }

    # 复制 scripts list
    if (Test-Path "$scriptsListDir\dist") {
        Copy-Item -Path "$scriptsListDir\dist\*" -Destination "$assetsDir\Web\ScriptRepo" -Recurse -Force
        Write-Success "已复制 Scripts List 资源"
    }

    # 复制 publish 资源
    if (Test-Path $publishDir) {
        # 复制 zst 文件
        Get-ChildItem -Path $publishDir -Filter "*.zst" | ForEach-Object {
            $destFile = "$assetsDir\Map\$($_.Name)"
            if (Test-Path $_.FullName) {
                Copy-Item -Path $_.FullName -Destination $destFile -Force
            }
        }
        # 复制 zip 文件
        Get-ChildItem -Path $publishDir -Filter "*.zip" | ForEach-Object {
            $destFile = "$assetsDir\Map\$($_.Name)"
            if (Test-Path $_.FullName) {
                Copy-Item -Path $_.FullName -Destination $destFile -Force
            }
        }
        Write-Success "已复制 Publish 资源"
    }

    Write-Success "资源仓库处理完成"
} else {
    Write-Info "跳过克隆资源仓库"
    $distDir = "dist\BetterGI"
}

# ============================================
# 2. 更新版本号
# ============================================
Write-Step "更新版本号..."
$csprojPath = "BetterGenshinImpact\BetterGenshinImpact.csproj"
if (Test-Path $csprojPath) {
    $content = Get-Content $csprojPath -Raw
    if ($content -match '<Version>.*</Version>') {
        $content = $content -replace '<Version>.*</Version>', "<Version>$Version</Version>"
        Set-Content -Path $csprojPath -Value $content
        Write-Success "版本号已更新为: $Version"
    }
}

# ============================================
# 3. 编译项目
# ============================================
if (-not $SkipBuild) {
    Write-Step "开始编译项目..."
    $compileStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    dotnet publish BetterGenshinImpact/BetterGenshinImpact.csproj `
        -c Release `
        -p:PublishProfile=FolderProfile `
        -p:Version=$Version

    if ($LASTEXITCODE -ne 0) {
        Write-Error "编译失败，退出代码: $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $compileStopwatch.Stop()
    Write-Success "编译完成，耗时: $($compileStopwatch.Elapsed.ToString('hh\:mm\:ss'))"
} else {
    Write-Info "跳过编译"
}

# ============================================
# 4. 整理发布文件
# ============================================
Write-Step "整理发布文件..."

# 查找发布目录
$publishDir = Get-ChildItem -Path "BetterGenshinImpact\bin" -Recurse -Directory -Filter "publish" |
    Where-Object { $_.FullName -like "*Release*" } |
    Select-Object -First 1 -ExpandProperty FullName
$sourceDir = Join-Path $publishDir "win-x64"

if (-not (Test-Path $sourceDir)) {
    Write-Error "未找到发布目录: $sourceDir"
    exit 1
}
Write-Info "发布目录: $sourceDir"

# 清理不必要的文件
$libFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*.lib"
$pdbFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*.pdb"
$ffmpegFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*ffmpeg*.dll"

$libFiles | Remove-Item -Force -ErrorAction SilentlyContinue
$pdbFiles | Remove-Item -Force -ErrorAction SilentlyContinue
$ffmpegFiles | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Success "已清理: $($libFiles.Count) 个 .lib, $($pdbFiles.Count) 个 .pdb, $($ffmpegFiles.Count) 个 ffmpeg dll"

# 复制到 dist
if (Test-Path $distDir) { Remove-Item -Path $distDir -Recurse -Force }
New-Item -Path $distDir -ItemType Directory -Force | Out-Null
Copy-Item -Path "$sourceDir\*" -Destination $distDir -Recurse -Force

# 如果有预先克隆的资源，复制进去
if (-not $SkipClone) {
    if (Test-Path "$assetsDir\Map\Editor") {
        Copy-Item -Path "$assetsDir\Map\Editor\*" -Destination "$distDir\Assets\Map\Editor" -Recurse -Force
    }
    if (Test-Path "$assetsDir\Web\ScriptRepo") {
        Copy-Item -Path "$assetsDir\Web\ScriptRepo\*" -Destination "$distDir\Assets\Web\ScriptRepo" -Recurse -Force
    }
    if (Test-Path "$assetsDir\Map\*.zst") {
        Copy-Item -Path "$assetsDir\Map\*.zst" -Destination "$distDir\Assets\Map" -Recurse -Force
    }
    if (Test-Path "$assetsDir\Map\*.zip") {
        Copy-Item -Path "$assetsDir\Map\*.zip" -Destination "$distDir\Assets\Map" -Recurse -Force
    }
}

Write-Success "文件整理完成"

# ============================================
# 5. 打包
# ============================================
if (-not $SkipPackage) {
    Write-Step "开始打包..."
    $packageStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    Push-Location dist
    if (Get-Command 7z -ErrorAction SilentlyContinue) {
        7z a "BetterGI_v$Version.7z" BetterGI -t7z -mx=5 -r -y
        if ($LASTEXITCODE -ne 0) {
            Write-Error "7z 压缩失败，退出代码: $LASTEXITCODE"
        } else {
            Write-Success "7z 打包完成"
        }
        $archiveType = "7z"
    } else {
        Compress-Archive -Path "BetterGI" -DestinationPath "BetterGI_v$Version.zip" -Force
        Write-Success "ZIP 打包完成"
        $archiveType = "zip"
    }
    Pop-Location

    $packageStopwatch.Stop()
    Write-Success "打包完成，耗时: $($packageStopwatch.Elapsed.ToString('hh\:mm\:ss'))"
} else {
    Write-Info "跳过打包"
}

# 清理临时目录
if (-not $SkipClone -and (Test-Path ".build_temp")) {
    Remove-Item -Path ".build_temp" -Recurse -Force
    Write-Info "已清理临时目录"
}

# 计时结束
$totalStopwatch.Stop()

# 输出统计
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "           构 建 统 计                  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "⏱️  总耗时: $($totalStopwatch.Elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n=== 输出文件 ===" -ForegroundColor Cyan
$archiveFile = Get-ChildItem -Path "dist" -Filter "BetterGI_v*.$archiveType" | Select-Object -First 1
if ($archiveFile) {
    $archiveSize = [math]::Round($archiveFile.Length / 1MB, 2)
    Write-Host "📦 压缩包: $($archiveFile.FullName) ($archiveSize MB)" -ForegroundColor Green
}

$exeFile = Get-ChildItem -Path $distDir -Filter "*.exe" | Select-Object -First 1
if ($exeFile) {
    $exeSize = [math]::Round($exeFile.Length / 1MB, 2)
    Write-Host "🚀 主程序: $($exeFile.FullName) ($exeSize MB)" -ForegroundColor Green
}

$fileCount = (Get-ChildItem -Path $distDir -Recurse -File).Count
Write-Host "📄 文件总数: $fileCount" -ForegroundColor Cyan

Write-Host "`n✅ 构建成功完成!" -ForegroundColor Green
