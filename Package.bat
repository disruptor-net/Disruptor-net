rmdir /S /Q Target

"C:\Program Files (x86)"\MSBuild\14.0\bin\msbuild Build.xml /t:Package /p:DisruptorVersion=3.3.4 /p:NugetVersion=3.3.4

pause