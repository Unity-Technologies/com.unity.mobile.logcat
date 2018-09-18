try:
    # For Python 3.0 and later
    from urllib.request import urlopen
except ImportError:
    # Fall back to Python 2's urllib2
    from urllib2 import urlopen
import urllib
import json
import os
import sys
import shutil
import zipfile
import subprocess
import time

def getUnityBuildZipUrl(branch):
    print("Finding unity build zip url from katana.");
    urlopen("https://katana.bf.unity3d.com/projects/Unity/builders?unity_branch=" + branch); # open the branch to make sure that the json is available.
    url = "https://katana.bf.unity3d.com/json/builders/proj0-Build%20WindowsEditor/builds?unity_branch=" + branch + "&results=0";
    response = urlopen(url);
    data = json.loads(response.read());

    for step in data[0]["steps"]:
        if 'ZippedForTeamCity.zip' in step["urls"]:
            return (step["urls"]["ZippedForTeamCity.zip"]);
    return

def download_url(url, filename):
    print("Downloading %s to %s" % (url, filename));
    response = urlopen(url);
    with open(filename, 'wb') as f:
        shutil.copyfileobj(response, f)

def downloadUnity(url):
    path = '.\\Unity.zip';
    download_url(url, path);
    print("Unzipping unity");
    zip = zipfile.ZipFile(path);
    zip.extractall("c:\\UnityBuild");
    return "c:\\UnityBuild\\Unity.exe";

def runWithTimeout(exePath, args, timeout):
    print(exePath + " " + args);
    s = subprocess.Popen(exePath + args )
    poll_period = 0.1
    s.poll()
    timeoutPrinted = False;
    while s.returncode is None and timeout > 0:
        time.sleep(poll_period)
        timeout -= poll_period
        s.poll()
        if timeout <= 0:
            print("Running tests in unity timed out. Killing process.");
            os.kill(s.pid, 7);
            raise Exception("Running test failed.");
        else:
            pass
    return s.returncode;
    
def runTestsInUnity(unityPath, platform):
    print("Running " + platform + " tests in unity");
    exitCode = runWithTimeout(unityPath, " -projectpath " + os.environ['UNITY_TEST_PROJECT'] + " -runTests -batchmode -testPlatform " + platform + " -accept-apiupdate -automated -testResults .\\TestResult" + platform + ".xml -logFile .\\UnityLog"+ platform + ".txt", 60*15);
    print("ExitCode: " + str(exitCode));
    return exitCode;
    
def main():
    print("Unity test runner CI script.");
    unityPath = "";
    if os.environ.get('UNITY_LOCAL_BUILD') is not None:
        unityPath = os.environ['UNITY_LOCAL_BUILD'] + "\\build\\WindowsEditor\\Unity.exe";
    else:
       # Figure out a smarter way to get local build instead
       url = getUnityBuildZipUrl(os.environ['UNITY_BRANCH']);
       unityPath = downloadUnity(url);

    print("Unity at %s" % unityPath);
    editModeExitCode = runTestsInUnity(unityPath, "Editmode");
    playModeExitCode = 0;#runTestsInUnity(unityPath, "Playmode");
    if (editModeExitCode != 0 or playModeExitCode != 0):
       raise Exception("Tests failed. Exit code: " + str(editModeExitCode) + " and " + str(playModeExitCode));

if __name__ == "__main__":
    main();
