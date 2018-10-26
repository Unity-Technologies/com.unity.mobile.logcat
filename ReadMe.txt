Requirements for making it work on Windows locally

* Download Python 2.7.9 https://www.python.org/downloads/release/python-279/ , python 2.7 is no good.
* Modify env PATH variable, add E:\Python27;E:\Python27\scripts
* Then use either run.cmd, runWithoutDownloadingUnity.cmd,runTestsDirectly.cmd
  - run.cmd basically does that Gitlab CI does - run python, which downloads Unity, and runs tests via Unity
  - runWithoutDownloadingUnity.cmd, same as run.cmd but Unity won't be downloaded, ensure Unity is installed in Editor folder with AndroidSupport
  - runTestsDirectly.cmd, directly invokes Unity with cmd arguments for runnings tests, no python is required
