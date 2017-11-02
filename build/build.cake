var target = Argument("target", "Run-Tests");
var configuration = Argument("configuration", "Release");
var paths = new 
{
    AssemblyOutput = MakeAbsolute(Directory("../output/assembly")),
    TestsOutput = MakeAbsolute(Directory("../output/tests")),
    PerfOutput = MakeAbsolute(Directory("../output/perf")),
    NugetOutput = MakeAbsolute(Directory("../output/nuget")),
    AssemblyProject = MakeAbsolute(File("../src/Disruptor/Disruptor.csproj")),
    TestsProject = MakeAbsolute(File("../src/Disruptor.Tests/Disruptor.Tests.csproj")),
    PerfProject = MakeAbsolute(File("../src/Disruptor.PerfTests/Disruptor.PerfTests.csproj")),
    Nuspec = MakeAbsolute(File("Disruptor-net.nuspec")),
    NUnit = MakeAbsolute(File("../tools/NUnit/nunit3-console.exe")),
    Projects = GetFiles("../src/**/*.csproj").Select(MakeAbsolute),
};

var nugetVersion = XmlPeek(paths.AssemblyProject, "//InformationalVersion/text()");
var targetFrameworks = XmlPeek(paths.AssemblyProject, "//TargetFrameworks/text()").Split(';');

Task("Restore-NuGet-Packages")
    .Does(() => 
    {
        foreach (var project in paths.Projects)
        {
            Information("Restoring {0}", project.FullPath);
            DotNetCoreRestore(project.FullPath);
        }
    });

Task("Clean-Tests")
    .Does(() => CleanDirectory(paths.TestsOutput));

Task("Build-Tests")
    .IsDependentOn("Clean-Tests")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        var settings = new DotNetCoreBuildSettings { Configuration = configuration, OutputDirectory = paths.TestsOutput.FullPath };
        DotNetCoreBuild(paths.TestsProject.FullPath, settings);
    });

Task("Run-Tests")
    .IsDependentOn("Build-Tests")
    .Does(() => NUnit(paths.TestsOutput.FullPath + "/*.Tests.dll", new NUnitSettings { NoResults = true, ToolPath = paths.NUnit }));

Task("Clean-Perf")
    .Does(() => CleanDirectory(paths.PerfOutput));

Task("Build-Perf")
    .IsDependentOn("Clean-Perf")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        var settings = new DotNetCoreBuildSettings { Configuration = configuration, OutputDirectory = paths.PerfOutput.FullPath };
        DotNetCoreBuild(paths.PerfProject.FullPath, settings);
    });

Task("Clean-Assembly")
    .Does(() => CleanDirectory(paths.AssemblyOutput));

Task("Build-Assembly")
    .IsDependentOn("Clean-Assembly")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        foreach (var targetFramework in targetFrameworks)
        {
            Information("Building {0}", targetFramework);
            var settings = new DotNetCoreBuildSettings
            {
                Framework = targetFramework,
                Configuration = configuration,
                OutputDirectory = paths.AssemblyOutput.FullPath + "/" + targetFramework,
            };
            DotNetCoreBuild(paths.AssemblyProject.FullPath, settings);
        }
    });

Task("Pack")
    .IsDependentOn("Build-Assembly")
    .Does(() => 
    {
		CreateDirectory(paths.NugetOutput);
        Information("Packing {0}", nugetVersion);
        NuGetPack(paths.Nuspec, new NuGetPackSettings
        {
            Version = nugetVersion,
            BasePath = paths.AssemblyOutput.FullPath,
            OutputDirectory = paths.NugetOutput
        });
    });

RunTarget(target);
