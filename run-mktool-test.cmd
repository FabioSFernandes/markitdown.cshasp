@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

REM Use Tesseract OCR for images if installed and TESSDATA_PREFIX not already set
if not defined TESSDATA_PREFIX if exist "C:\Program Files\Tesseract-OCR\tessdata" set "TESSDATA_PREFIX=C:\Program Files\Tesseract-OCR"

echo Converting all files in MkTool\test with MkTool...
echo.

for %%f in (MkTool\test\*.*) do (
    if exist "%%f" (
        echo --- %%f ---
        dotnet run --project MkTool -- "%%f"
        if errorlevel 1 echo [FAILED] %%f
        echo.
    )
)

echo Done.
endlocal
