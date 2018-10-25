import sys
import os
import string
import subprocess
import argparse

print(sys.version)

kPIPDownloadName = "http://172.28.214.140/tools/unity-downloader/unity-downloader-cli-0.1.tar.gz"

allPlatforms = ["Editor", "StandaloneLinux", "StandaloneLinux64", "StandaloneOSX", "StandaloneWindows", "StandaloneWindows64", "Android", "iOS"]

editorRevisions = {
    "2019.1": "370df4c90d4b"
}

def GetDownloadComponentsArgs(platforms):
    components = set(["Editor"])
    for platform in platforms:
        if "Standalone" in platform:
            components.add("StandaloneSupport-Mono")
        if "Android" in platforms:
            components.add("Android")
    ret = []
    for c in components:
        ret.append("-c")
        ret.append(c)
    return ret

def RunProcess(args):

    desc = ' '.join(args)
    print("[%s]" % desc)
    process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)#shell=True)
    for stdout_line in iter(process.stdout.readline, ""):
        sys.stdout.write(stdout_line)
        sys.stdout.flush()
    (stdOut, stdErr) = process.communicate()
    exitCode = process.wait()
    stdErr = stdErr.decode('ascii')
    
    if len(stdErr) > 0:
        print("Standard Error Output")
        print(stdErr)
    
    print("[Process completed with Exit Code: %d]" % exitCode)
    if exitCode:
        raise subprocess.CalledProcessError(exitCode, args)
    return (stdOut, stdErr)

parser = argparse.ArgumentParser(description="Run the trace event profiler tests")
parser.add_argument('runtimePlatform', nargs='*', choices=allPlatforms)
parser.add_argument('--version', choices=editorRevisions.keys())
args = parser.parse_args(sys.argv[1:])

runtimePlatforms = args.runtimePlatform
unityVersion = args.version

kRootRepoDirectory = os.path.dirname(os.path.realpath(__file__))
kProjectPath = os.path.join(kRootRepoDirectory, "TestProjects/SampleProject1")
kTestArtifactPath = os.path.join(kRootRepoDirectory, "TestArtifacts")
kInstallPath = os.path.join(kRootRepoDirectory, "UnityInstall")
kEditorPath = os.path.join(kInstallPath, "Unity")
if os.name is not "nt":
    kEditorPath = os.path.join(kInstallPath, "Unity.app/Contents/MacOS/Unity")
    

print("__file__=%s" % __file__)
print("os.path.realpath(__file__)=%s" % os.path.realpath(__file__))
print("Testing Platforms: %s" % ', '.join(runtimePlatforms))
print("Unity Version: %s" % unityVersion)
print("kRootRepoDirectory = %s" % kRootRepoDirectory)
print("kProjectPath = %s" % kProjectPath)

if not os.path.isdir(kTestArtifactPath):
    os.makedirs(kTestArtifactPath)

RunProcess(["pip", "install", kPIPDownloadName])

componentsArgs = GetDownloadComponentsArgs(runtimePlatforms)
revision = editorRevisions[unityVersion]
RunProcess(["unity-downloader-cli", "-r", revision, "-p", kInstallPath] + componentsArgs)

for platform in runtimePlatforms:

    flags = ["-batchmode", "-cleanTestPrefs", "-automated", "-upmNoDefaultPackages", "-enableAllModules", "-runTests" ]
    runOptions = {}
    runOptions["projectPath"] = kProjectPath
    runOptions["testResults"] = os.path.join(kTestArtifactPath, "%s_TestResults.txt" % platform)
    runOptions["logFile"] = os.path.join(kTestArtifactPath, "%s_EditorLog.txt" % platform)

    runOptions["testPlatform"] = "playmode"
    runOptions["buildTarget"] = platform;

    allArgs = [kEditorPath] + flags
    for k in runOptions:
        allArgs.append('-%s' % k)
        allArgs.append(runOptions[k])
    
    print("Running tests for platform %s" % platform)
    RunProcess(allArgs)
    
