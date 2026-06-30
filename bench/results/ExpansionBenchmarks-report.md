```

BenchmarkDotNet v0.14.0, Ubuntu 26.04.1 LTS (Resolute Raccoon)
13th Gen Intel Core i5-13600K, 1 CPU, 4 logical and 14 physical cores
.NET SDK 8.0.425
  [Host]   : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method         | Series | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|--------------- |------- |-----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **SingleThreaded** | **504**    |  **10.398 ms** |  **1.5812 ms** | **0.0867 ms** |  **1.00** |    **0.01** |  **609.3750** |  **484.3750** |  **484.3750** |   **3.44 MB** |        **1.00** |
| Parallel       | 504    |   9.411 ms |  0.4922 ms | 0.0270 ms |  0.91 |    0.01 |  593.7500 |  578.1250 |  343.7500 |   4.52 MB |        1.31 |
|                |        |            |            |           |       |         |           |           |           |           |             |
| **SingleThreaded** | **5040**   | **128.257 ms** | **48.3333 ms** | **2.6493 ms** |  **1.00** |    **0.03** | **2500.0000** | **1500.0000** | **1500.0000** |  **46.35 MB** |        **1.00** |
| Parallel       | 5040   | 111.922 ms | 43.4882 ms | 2.3837 ms |  0.87 |    0.02 | 2600.0000 | 1600.0000 | 1400.0000 |  46.56 MB |        1.00 |
