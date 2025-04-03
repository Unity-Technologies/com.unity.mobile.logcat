using RecipeEngine.Api.Dependencies;
using RecipeEngine.Api.Settings;
using RecipeEngine.Modules.Wrench.Models;
using RecipeEngine.Modules.Wrench.Settings;

namespace UnityMobileLogcat.Cookbook.Settings;

public class UnityMobileLogcatSettings : AnnotatedSettingsBase
{
    // Path from the root of the repository where packages are located.
    readonly string[] PackagesRootPaths = {"."};

    // update this to list all packages in this repo that you want to release.
    Dictionary<string, PackageOptions> PackageOptions = new()
    {
        {
            "com.unity.mobile.android-logcat",
            new PackageOptions() {
                ReleaseOptions = new ReleaseOptions() 
                { 
                    IsReleasing = true 
                },
                PackJobOptions = new PackJobOptions()
                { 
                    Dependencies = new List<Dependency>()
                    {
                        new("format", "check_formatting") 
                    }
                },
                CustomChecks = new HashSet<Dependency>() 
                { 
                    new Dependency("upm-ci", "test_all_trigger") 
                }
            }
        }
    };

    public UnityMobileLogcatSettings()
    {
        Wrench = new WrenchSettings(
            PackagesRootPaths,
            PackageOptions
        );
    }

    public WrenchSettings Wrench { get; private set; }
}
