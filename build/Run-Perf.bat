:: Use odd cpus to avoid scheduling threads on sibling logical processors of the same core when SMT is enabled.
:: Please adjust the values depending on your hardware.
set cpus=1,3,5,7

dotnet run --project ../src/Disruptor.PerfTests/ --configuration Release --target all --report false --cpus %cpus%
pause