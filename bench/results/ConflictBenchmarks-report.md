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
| AllSeriesSequential | 7.707 ms |  1.963 ms | 0.1076 ms | 226.5625 | 54.6875 |   2.74 MB |
| AllSeriesParallel   | 6.491 ms | 17.898 ms | 0.9810 ms | 226.5625 | 70.3125 |   2.75 MB |
