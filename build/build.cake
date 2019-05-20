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
    Projects = GetFiles("../src/**/*.csproj").Select(MakeAbsolute),
};

var targetFrameworks = XmlPeek(paths.AssemblyProject, "//TargetFrameworks/text()").Split(';');
var testsFrameworks = XmlPeek(paths.TestsProject, "//TargetFrameworks/text()").Split(';');

Task("Clean-Tests")
    .Does(() => CleanDirectory(paths.TestsOutput));

Task("Run-Tests")
    .IsDependentOn("Clean-Tests")
    .Does(() =>
    {
        foreach (var framwork in testsFrameworks)
        {
            Information("Running tests for {0}", framwork);
            DotNetCoreTest(paths.TestsProject.FullPath, new DotNetCoreTestSettings 
            {
                Configuration = configuration,
                OutputDirectory = paths.TestsOutput.FullPath + "/" + framwork,
                Framework = framwork,
            });
        }
    });

Task("Clean-Perf")
    .Does(() => CleanDirectory(paths.PerfOutput));

Task("Build-Perf")
    .IsDependentOn("Clean-Perf")
    .Does(() =>
    {
        DotNetCoreBuild(paths.PerfProject.FullPath, new DotNetCoreBuildSettings 
        {
            Configuration = configuration,
            OutputDirectory = paths.PerfOutput.FullPath,
        });
    });

Task("Clean-Assembly")
    .Does(() => CleanDirectory(paths.AssemblyOutput));

Task("Build-Assembly")
    .IsDependentOn("Clean-Assembly")
    .Does(() =>
    {
        foreach (var targetFramework in targetFrameworks)
        {
            Information("Building {0}", targetFramework);
            DotNetCoreBuild(paths.AssemblyProject.FullPath, new DotNetCoreBuildSettings
            {
                Framework = targetFramework,
                Configuration = configuration,
                OutputDirectory = paths.AssemblyOutput.FullPath + "/" + targetFramework,
            });
        }
    });

Task("Pack")
    .Does(() => 
    {
        DotNetCorePack(paths.AssemblyProject.FullPath, new DotNetCorePackSettings 
        {
            Configuration = configuration,
            OutputDirectory = paths.NugetOutput.FullPath,
        });
    });

Task("AppVeyor")
    .IsDependentOn("Run-Tests")
    .IsDependentOn("Pack");

RunTarget(target);
