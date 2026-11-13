@echo off
rem Writes docs~/index.html, a fixed entry point that redirects to the manual of the version just built.
cd /d "%~dp0..\..\docs~\documentation" || exit /b 1
for /d %%d in (com.unity.mobile.android-logcat@*) do if /i not "%%d"=="com.unity.mobile.android-logcat@latest" set DOCS=%%d
if not defined DOCS (echo No generated docs found in docs~\documentation. & exit /b 1)

set TARGET=documentation/%DOCS%/manual/index.html
(
  echo ^<!doctype html^>
  echo ^<meta charset="utf-8"^>
  echo ^<meta http-equiv="refresh" content="0; url=%TARGET%"^>
  echo ^<title^>%DOCS% documentation^</title^>
  echo ^<a href="%TARGET%"^>Open the %DOCS% documentation^</a^>
) > ..\index.html
echo Wrote docs~\index.html pointing at %TARGET%
