```

BenchmarkDotNet v0.14.0, Ubuntu 26.04.1 LTS (Resolute Raccoon)
13th Gen Intel Core i5-13600K, 1 CPU, 4 logical and 14 physical cores
.NET SDK 8.0.425
  [Host]   : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.31 (8.0.3126.42015), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method              | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated |
|-------------------- |---------:|----------:|----------:|---------:|--------:|----------:|
| AllSeriesSequential | 5.393 ms | 0.9813 ms | 0.0538 ms |  93.7500 | 15.6250 |   1.21 MB |
| AllSeriesParallel   | 3.431 ms | 2.6284 ms | 0.1441 ms | 101.5625 | 27.3438 |   1.22 MB |
