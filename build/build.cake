#tool "nuget:?package=NUnit.Runners.Net4&version=2.6.4"
#addin "Cake.Json"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var paths = new 
{
    output = MakeAbsolute(Directory("./../output")),
    nugetOutput = MakeAbsolute(Directory("./../output/nuget")),
    solution = MakeAbsolute(File("./../src/Disruptor-net.sln")),
    nuspec = MakeAbsolute(File("./Disruptor-net.nuspec")),
    assemblyInfo = MakeAbsolute(File("./../src/Version.cs")),
    versions = MakeAbsolute(File("./../versions.json"))
};

Task("Clean")
    .Does(() =>{
        CleanDirectory(paths.output);
        CleanDirectory(paths.nugetOutput);
    } );

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() => NuGetRestore(paths.solution));

/// Build tasks

Task("Create-AssemblyInfo")
    .Does(()=>{
        var versionDetails = DeserializeJsonFromFile<VersionDetails>(paths.versions.FullPath);
        CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
            Company = "https://github.com/disruptor-net/Disruptor-net",
            Product = "Disruptor",
            Copyright = "Copyright © disruptor-net",
            Version = versionDetails.AssemblyVersion,
            FileVersion = versionDetails.AssemblyVersion,
            InformationalVersion = versionDetails.NugetVersion + " (from java commit: " + versionDetails.LastJavaRevisionPortedVersion +")"
        });
    });

Task("MSBuild")
    .Does(() => MSBuild(paths.solution, settings => settings
                                            .SetConfiguration(configuration)
                                            .SetPlatformTarget(PlatformTarget.MSIL)
                                            .WithProperty("OutDir", paths.output.FullPath + "/build")));

Task("Clean-AssemblyInfo")
    .Does(()=>{
        DeleteFile(paths.assemblyInfo);
        System.IO.File.Create(paths.assemblyInfo.FullPath);
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Create-AssemblyInfo")
    .IsDependentOn("MSBuild")
    .IsDependentOn("Clean-AssemblyInfo");

/// Unit test tasks

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() => NUnit(paths.output.FullPath + "/build/*.Tests.dll", new NUnitSettings { 
        Framework = "net-4.6.1",
        NoResults = true
    }));

/// Package tasks

Task("Nuget-Pack")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() => 
    {
        var versionDetails = DeserializeJsonFromFile<VersionDetails>(paths.versions.FullPath);
        NuGetPack(paths.nuspec, new NuGetPackSettings {
            Version = versionDetails.NugetVersion,
            BasePath = paths.output.FullPath + "/build",
            OutputDirectory = paths.nugetOutput
        });
    });

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

RunTarget(target);

private class VersionDetails 
{
    public string LastJavaRevisionPortedVersion { get; set; } 
    public string AssemblyVersion { get; set; }
    public string NugetVersion { get; set; }
}