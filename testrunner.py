import sys
import os
import string
import subprocess
import argparse
from shutil import copyfile

print(sys.version)

allPlatforms = ["Editor", "StandaloneLinux", "StandaloneLinux64", "StandaloneOSX", "StandaloneWindows", "StandaloneWindows64", "Android", "iOS"]

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

def main():
    parser = argparse.ArgumentParser(description="Run logcat tests")
    parser.add_argument('runtimePlatform', nargs='*', choices=allPlatforms)
    parser.add_argument('--version')
    parser.add_argument('--uselocalversion', dest='uselocalversion', action='store_true')
    parser.set_defaults(uselocalversion=False)
    args = parser.parse_args(sys.argv[1:])

    runtimePlatforms = args.runtimePlatform
    unityVersion = args.version

    kRootRepoDirectory = os.path.dirname(os.path.realpath(__file__))
    kProjectPath = os.path.join(kRootRepoDirectory, "TestProjects/SampleProject1")
    kInstallPath = os.path.join(kRootRepoDirectory, "Editor_%s" % unityVersion)
    kEditorPath = os.path.join(kInstallPath, "Unity")
    if os.name is not "nt":
        kEditorPath = os.path.join(kInstallPath, "Unity.app/Contents/MacOS/Unity")
    kTestArtifactPath = os.path.join(kRootRepoDirectory, "TestArtifacts")
    if not os.path.isdir(kTestArtifactPath):
        os.makedirs(kTestArtifactPath)
        
    print("__file__=%s" % __file__)
    print("os.path.realpath(__file__)=%s" % os.path.realpath(__file__))
    print("Testing Platforms: %s" % ', '.join(runtimePlatforms))
    print("Unity Version: %s" % unityVersion)
    print("kRootRepoDirectory = %s" % kRootRepoDirectory)
    print("kProjectPath = %s" % kProjectPath)
    print("kTestArtifactPath = %s" % kTestArtifactPath)

    # Download Unity with the right version.
    if not args.uselocalversion:
        RunProcess(["pip", "install", "unity-downloader-cli", "--extra-index-url", "https://artifactory.eu-cph-1.unityops.net/api/pypi/common-python/simple"])
        componentsArgs = GetDownloadComponentsArgs(runtimePlatforms)
        if unityVersion == "trunk":
            RunProcess(["unity-downloader-cli", "--wait", "-b", unityVersion, "-p", kInstallPath] + componentsArgs)
        else:
            RunProcess(["unity-downloader-cli", "--wait", "--unity-version", unityVersion, "-p", kInstallPath] + componentsArgs)
    else:
        print("Using local Unity version, ensure Editor folder with AndroidSupport exists")

    # The performance testing package 0.1.50-preview which we're using for Unity 2019.1 is not compatible with trunk.
    # We have to use the manifest with the version 1.0.4-preview when we're testing against trunk.
    if unityVersion == "trunk":
        print("Copying package manifest file for trunk")
        copyfile("TestProjects/package_manifest_for_trunk.json", "TestProjects/SampleProject1/Packages/manifest.json")

    # Run tests.
    for platform in runtimePlatforms:

        flags = ["-batchmode", "-cleanTestPrefs", "-automated", "-upmNoDefaultPackages", "-enableAllModules", "-runTests" ]
        runOptions = {
            'projectPath': kProjectPath,
            'testResults': os.path.join(kTestArtifactPath, "%s_TestResults_%s.txt" % (platform, unityVersion) ),
            'logFile': os.path.join(kTestArtifactPath, "%s_EditorLog_%s.txt" % (platform, unityVersion) ),
            'testPlatform': "editmode",
            'buildTarget': platform
        }

        allArgs = [kEditorPath] + flags
        for k in runOptions:
            allArgs.append('-%s' % k)
            allArgs.append(runOptions[k])
        
        print("Running tests for platform %s" % platform)
        RunProcess(allArgs)
      
if __name__ == '__main__':
    main()

