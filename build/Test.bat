dotnet test ../src/Disruptor-net.sln --configuration Release
dotnet test ../src/Disruptor-net.sln --configuration Release --environment DISRUPTOR_DYNAMIC_CODE_DISABLED=1
pause