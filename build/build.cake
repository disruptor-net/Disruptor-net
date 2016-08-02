#tool "nuget:?package=NUnit.Runners.Net4&version=2.6.4"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var paths = new {
    output = MakeAbsolute(Directory("./../output")),
    nugetOutput = MakeAbsolute(Directory("./../output/nuget")),
    solution = MakeAbsolute(File("./../src/Disruptor-net.sln")),
    disruptor = MakeAbsolute(File("./../src/Disruptor/Disruptor.csproj")),
    tests = MakeAbsolute(File("./../src/Disruptor.Tests/Disruptor.Tests.csproj")),
    perfTests = MakeAbsolute(File("./../src/Disruptor.PerfTests/Disruptor.PerfTests.csproj")),
    nuspec = MakeAbsolute(File("./Disruptor-net.nuspec")),
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

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() => MSBuild(paths.solution, settings => settings
                                            .SetConfiguration(configuration)
                                            .SetPlatformTarget(PlatformTarget.MSIL)
                                            .WithProperty("OutDir", paths.output.FullPath + "/build")));

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
    .Does(()=> NuGetPack(paths.nuspec, new NuGetPackSettings {
        BasePath = paths.output.FullPath + "/build",
        OutputDirectory = paths.nugetOutput
    }));

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

RunTarget(target);
