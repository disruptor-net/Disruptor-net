:: Use odd cpus to avoid scheduling threads on sibling logical processors of the same core when SMT is enabled.
:: Please adjust the values depending on your hardware.
set cpus=1,3,5,7
set targetFramework=net10.0

dotnet publish ../src/Disruptor.PerfTests --configuration Release --output ./bin/PerfTests-NativeAot
dotnet publish ../src/Disruptor.Tests.IpcPublisher --framework %targetFramework% --configuration Release --output ./bin/IpcPublisher-NativeAot

.\bin\PerfTests-NativeAot\Disruptor.PerfTests.exe  --target all --report false --cpus %cpus% --ipc-publisher-path .\bin\IpcPublisher-NativeAot\Disruptor.Tests.IpcPublisher.exe

pause
