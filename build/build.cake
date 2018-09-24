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
    NUnit = MakeAbsolute(File("../tools/NUnit/nunit3-console.exe")),
    Projects = GetFiles("../src/**/*.csproj").Select(MakeAbsolute),
};

var targetFrameworks = XmlPeek(paths.AssemblyProject, "//TargetFrameworks/text()").Split(';');
var testsFrameworks = XmlPeek(paths.TestsProject, "//TargetFrameworks/text()").Split(';');

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
        foreach (var framwork in testsFrameworks.Where(x => !x.Contains("core")))
        {
            Information("Building tests for  {0}", framwork);
            
            var outputPath = paths.TestsOutput.FullPath + "/" + framwork;
            var settings = new DotNetCoreBuildSettings { Configuration = configuration, OutputDirectory = outputPath, Framework = framwork };
            DotNetCoreBuild(paths.TestsProject.FullPath, settings);
        }
    });

Task("Run-Tests")
    .IsDependentOn("Build-Tests")
    .Does(() =>
    {
        foreach (var framwork in testsFrameworks.Where(x => !x.Contains("core")))
        {
            Information("Running tests for {0}", framwork);
            
            var outputPath = paths.TestsOutput.FullPath + "/" + framwork;
            NUnit(outputPath + "/*.Tests.exe", new NUnitSettings { NoResults = true, ToolPath = paths.NUnit });
        }
        
        foreach (var framwork in testsFrameworks.Where(x => x.Contains("core")))
        {
            Information("Running tests for {0}", framwork);
            
            var outputPath = paths.TestsOutput.FullPath + "/" + framwork;
            var settings = new DotNetCoreTestSettings { Configuration = configuration, OutputDirectory = outputPath, Framework = framwork };
            DotNetCoreTest(paths.TestsProject.FullPath, settings);
        }
    });

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
        MSBuild(paths.AssemblyProject, settings => settings
            .WithTarget("Pack")
            .SetConfiguration("Release")
            .SetPlatformTarget(PlatformTarget.MSIL)
            .SetVerbosity(Verbosity.Minimal)
            .WithProperty("PackageOutputPath", paths.NugetOutput.FullPath)
        );
    });

Task("AppVeyor")
    .IsDependentOn("Run-Tests")
    .IsDependentOn("Pack");

RunTarget(target);
