@echo off
rem Serves the docs over HTTP: DocFX loads the sidebar with a request that browsers block for pages opened from disk.
where python >nul 2>&1 || (echo Python is required to serve the docs. & pause & exit /b 1)

cd /d "%~dp0documentation"
for /d %%d in (com.unity.mobile.android-logcat@*) do if /i not "%%d"=="com.unity.mobile.android-logcat@latest" set DOCS=%%d

start "" /b python -c "import time, webbrowser; time.sleep(1); webbrowser.open('http://localhost:8000/%DOCS%/manual/index.html')"
echo Serving %DOCS% at http://localhost:8000, close this window to stop.
python -m http.server 8000
