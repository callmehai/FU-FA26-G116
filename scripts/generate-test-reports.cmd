@echo off
setlocal
set "REPORT_RUNTIME=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies"
if exist "%REPORT_RUNTIME%\python\python.exe" (
  "%REPORT_RUNTIME%\python\python.exe" -X utf8 "%~dp0generate_test_reports.py" --node "%REPORT_RUNTIME%\node\bin\node.exe" %*
) else (
  python -X utf8 "%~dp0generate_test_reports.py" %*
)
exit /b %errorlevel%
