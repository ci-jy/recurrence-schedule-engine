# Benchmarks

Run on a 4-core Linux VM with the BenchmarkDotNet ShortRun job on .NET 8.

| Measurement | Result | Source |
|---|---|---|
| Differential test vs python-dateutil: 2,000 rules, 109,002 occurrences, 11 IANA zones | 1,979 identical, 21 documented RFC differences, 0 unexplained | `difftest/report.json` |
| Expansion, 504 series over 1 year (26,776 occurrences) | 6.7 ms single-threaded (4.0 M occ/s), 4.7 ms parallel (5.7 M occ/s) | `bench/results/ExpansionBenchmarks-report.md` |
| Expansion, 5,040 series over 1 year (268,361 occurrences) | 87 ms single-threaded (3.1 M occ/s), 52 ms parallel (5.1 M occ/s) | same |
| Conflict check, proposal vs all 504 series, 6 months, in-process | 5.4 ms sequential, 3.4 ms parallel | `bench/results/ConflictBenchmarks-report.md` |
| `POST /api/conflicts/check`, 1,000 requests, same-room series, 6 months, including PostgreSQL and HTTP | p50 1.71 ms, p95 2.74 ms, p99 4.08 ms | `bench/results/latency.json` |

## Reproducing

```bash
dotnet run -c Release --project bench/Rse.Bench                                         # BenchmarkDotNet
dotnet run -c Release --project bench/Rse.Bench -- latency http://localhost:25080 1000   # against a running API
python3 scripts/plot_results.py                                                         # redraws docs/results.png
```

The seeded clinic data comes from `python3 tools/gen_clinic_schedule.py`.
