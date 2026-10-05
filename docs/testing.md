# Tests

```bash
dotnet test
python3 difftest/run_difftest.py --rules 2000 --fail-on-mismatch   # needs python-dateutil (difftest/requirements.txt)
```

The unit tests in `tests/Rse.Engine.Tests` cover:

- the RFC 5545 section 3.8.5.3 examples
- DST gap, overlap and cross-zone cases, including the half-hour shift on Australia/Lord_Howe
- window queries checked against full enumeration
- parser errors
- the merge sweep checked against brute force
- parallel results checked against sequential ones

The integration tests in `tests/Rse.Api.Tests` run the real API against PostgreSQL. They use `RSE_TEST_PG` if it is set, then the compose database on `localhost:25432` if it is running. Otherwise they create a throwaway cluster with the local `initdb` and `pg_ctl` on a free port and delete it afterwards. The Docker image build is not part of `dotnet test`.

## Differential test

`difftest/rulegen.py` generates seeded random rules weighted toward hard cases. `run_difftest.py` expands each one with the engine (through `src/Rse.Cli`) and with dateutil (`oracle.py`), compares UTC instants, classifies every mismatch and writes `difftest/report.json`. A mismatch is only marked explained when it matches a specific, checked category; there is no catch-all.

Known categories:

- weekly-bysetpos-first-week: dateutil truncates the first week at DTSTART, the engine evaluates the whole WKST-aligned week and then drops occurrences before DTSTART.
- date-only-until: the engine includes the whole local day, dateutil uses midnight.
- subdaily-freq: the engine rejects HOURLY, MINUTELY and SECONDLY.

DTSTART is not added automatically when it doesn't match the rule, which follows dateutil and RFC 2445.
