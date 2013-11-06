rd "%~dp0Target\Report\Unit" /s /q
md "%~dp0Target\Report\Unit"

c:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild Build.xml /t:Test

pause