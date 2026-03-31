# clean.ps1 - 单独运行清理
Write-Host "Cleaning temporary directories..." -ForegroundColor Cyan

# 使用 cmd 命令强制删除
cmd /c "rmdir /s /q Fischless.WindowsInput\bin 2>nul"
cmd /c "rmdir /s /q Fischless.WindowsInput\obj 2>nul"
cmd /c "rmdir /s /q Fischless.HotkeyCapture\bin 2>nul"
cmd /c "rmdir /s /q Fischless.HotkeyCapture\obj 2>nul"
cmd /c "rmdir /s /q Fischless.GameCapture\bin 2>nul"
cmd /c "rmdir /s /q Fischless.GameCapture\obj 2>nul"
cmd /c "rmdir /s /q BetterGenshinImpact\bin 2>nul"
cmd /c "rmdir /s /q BetterGenshinImpact\obj 2>nul"

Write-Host "Cleaned!" -ForegroundColor Green