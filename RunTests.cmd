REM specify path to editor here
set EDITOR_PATH=D:\Projects\input-new-android\build\WindowsEditor

git clone https://github.cds.internal.unity3d.com/unity/utr.git
IF "%EDITOR_PATH%"=="Undefined" (
  echo EDITOR_PATH is not defined
) ELSE (
  utr\utr --suite=editor --editor-location=%EDITOR_PATH% --testproject=TestProjects/SampleProject1 --artifacts_path=upm-ci~/test-results/ --extra-editor-arg="-buildTarget" --extra-editor-arg="android"
)
pause