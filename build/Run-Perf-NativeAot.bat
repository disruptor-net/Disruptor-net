dotnet publish ../src/Disruptor.PerfTests --configuration Release --output ./bin/PerfTests-NativeAot
dotnet publish ../src/Disruptor.Tests.IpcPublisher --framework net8.0 --configuration Release --output ./bin/IpcPublisher-NativeAot
.\bin\PerfTests-NativeAot\Disruptor.PerfTests.exe  --target all --report false --ipc-publisher-path .\bin\IpcPublisher-NativeAot\Disruptor.Tests.IpcPublisher.exe
pause
