```

BenchmarkDotNet v0.14.0, Ubuntu 26.04.1 LTS (Resolute Raccoon)
13th Gen Intel Core i5-13600K, 1 CPU, 4 logical and 14 physical cores
.NET SDK 8.0.425
  [Host]   : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method         | Series | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|--------------- |------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **SingleThreaded** | **504**    |  **6.700 ms** |  **2.972 ms** | **0.1629 ms** |  **1.00** |    **0.03** | **1078.1250** | **1015.6250** | **1015.6250** |   **5.61 MB** |        **1.00** |
| Parallel       | 504    |  4.688 ms |  2.679 ms | 0.1468 ms |  0.70 |    0.02 | 1015.6250 |  960.9375 |  757.8125 |      6 MB |        1.07 |
|                |        |           |           |           |       |         |           |           |           |           |             |
| **SingleThreaded** | **5040**   | **87.437 ms** | **73.463 ms** | **4.0267 ms** |  **1.00** |    **0.06** | **1666.6667** | **1333.3333** | **1333.3333** |  **74.14 MB** |        **1.00** |
| Parallel       | 5040   | 52.338 ms | 43.956 ms | 2.4094 ms |  0.60 |    0.03 | 2090.9091 | 1727.2727 | 1454.5455 |  62.17 MB |        0.84 |
