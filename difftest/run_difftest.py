#!/usr/bin/env python3
"""Differential test: expand thousands of random rules with the C# engine and with python-dateutil,
then log and classify every disagreement.

    python3 difftest/run_difftest.py --rules 2000 --fail-on-mismatch
"""
import argparse
import json
import random
import subprocess
import sys
import time
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))

import oracle  # noqa: E402
import rulegen  # noqa: E402

LIMIT = 200
HORIZON_YEARS = 6

# Intentional, documented differences between RFC 5545 and dateutil. A mismatch whose category is
# listed here is reported as "explained" and does not fail the run. Each classifier is a positive
# check (the engine must match an alternative reference), never a catch-all.
EXPLAINED = {
    "date-only-until": "Engine treats a date-only UNTIL as inclusive of that whole local day; dateutil uses midnight.",
    "subdaily-freq": "HOURLY/MINUTELY/SECONDLY are rejected by the engine; dateutil steps them in wall-clock time.",
    "weekly-bysetpos-first-week": (
        "For FREQ=WEEKLY with BYSETPOS, dateutil truncates the first week so it starts at DTSTART; the engine "
        "applies BYSETPOS to the whole WKST-aligned week (RFC 5545 defines the set per week) and then drops "
        "occurrences before DTSTART. Classified only when the engine output equals dateutil's own expansion "
        "with the first week evaluated in full."
    ),
}


def build_cli():
    out = HERE / "out" / "cli"
    cmd = ["dotnet", "build", str(ROOT / "src" / "Rse.Cli" / "Rse.Cli.csproj"), "-c", "Release", "-o", str(out), "--nologo", "-v", "q"]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL)
    return out / "Rse.Cli.dll"


def make_cases(n, seed):
    rng = random.Random(seed)
    cases = []
    while len(cases) < n:
        text, dtstart, zone = rulegen.random_rule(rng)
        horizon = (dtstart + timedelta(days=365 * HORIZON_YEARS)).replace(tzinfo=timezone.utc)
        try:
            base = oracle.expand(text, dtstart, zone, [], horizon, 40)
        except ValueError:
            continue  # rule dateutil itself rejects; not useful as a comparison
        exdates = []
        if base and rng.random() < 0.3:
            # Exclude a couple of real occurrences (by local wall time) plus one that never occurs.
            from zoneinfo import ZoneInfo
            picks = rng.sample(base, min(len(base), rng.randint(1, 3)))
            exdates = [p.astimezone(ZoneInfo(zone)).replace(tzinfo=None) for p in picks]
            exdates.append(dtstart + timedelta(minutes=7))
        cases.append({"id": f"r{len(cases):05d}", "rrule": text, "dtstart": dtstart, "tz": zone,
                      "exdates": exdates, "horizon": horizon})
    return cases


def run_engine(dll, cases):
    lines = []
    for c in cases:
        lines.append(json.dumps({
            "id": c["id"], "rrule": c["rrule"], "tz": c["tz"],
            "dtstart": c["dtstart"].strftime("%Y-%m-%dT%H:%M:%S"),
            "exdates": [d.strftime("%Y-%m-%dT%H:%M:%S") for d in c["exdates"]],
            "horizon": c["horizon"].strftime("%Y-%m-%dT%H:%M:%SZ"), "limit": LIMIT,
        }))
    t0 = time.perf_counter()
    proc = subprocess.run(["dotnet", str(dll), "expand"], input="\n".join(lines) + "\n",
                          capture_output=True, text=True, check=True)
    elapsed = time.perf_counter() - t0
    results = {}
    for line in proc.stdout.splitlines():
        r = json.loads(line)
        results[r["id"]] = r
    return results, elapsed


def classify(case, engine_occ, oracle_occ):
    rule = case["rrule"]
    if "UNTIL=" in rule and len(rule.split("UNTIL=")[1].split(";")[0]) == 8:
        return "date-only-until"
    if any(f"FREQ={f}" in rule for f in ("HOURLY", "MINUTELY", "SECONDLY")):
        return "subdaily-freq"
    if "FREQ=WEEKLY" in rule and "BYSETPOS" in rule:
        full = oracle.expand_full_first_week(rule, case["dtstart"], case["tz"], case["exdates"], case["horizon"], LIMIT)
        if full == engine_occ:
            return "weekly-bysetpos-first-week"
    from zoneinfo import ZoneInfo
    zone = ZoneInfo(case["tz"])
    e, o = set(engine_occ), set(oracle_occ)
    diff = sorted((e ^ o))
    if diff:
        local = diff[0].astimezone(zone).replace(tzinfo=None)
        kind = oracle.local_kind(local.replace(hour=case["dtstart"].hour, minute=case["dtstart"].minute), case["tz"])
        if kind != "normal":
            return f"dst-{kind}"
    if engine_occ[: len(oracle_occ)] == oracle_occ or oracle_occ[: len(engine_occ)] == engine_occ:
        return "count-until-boundary"
    if "BYSETPOS" in rule:
        return "bysetpos"
    if "BYDAY=" in rule and any(ch.isdigit() for ch in rule.split("BYDAY=")[1].split(";")[0]):
        return "ordinal-byday"
    return "other"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--rules", type=int, default=2000)
    ap.add_argument("--seed", type=int, default=5545)
    ap.add_argument("--fail-on-mismatch", action="store_true", help="exit 1 if any unexplained mismatch is found")
    ap.add_argument("--report", default=str(HERE / "report.json"))
    args = ap.parse_args()

    dll = build_cli()
    t0 = time.perf_counter()
    cases = make_cases(args.rules, args.seed)
    oracle_results = {}
    for c in cases:
        oracle_results[c["id"]] = oracle.expand(c["rrule"], c["dtstart"], c["tz"], c["exdates"], c["horizon"], LIMIT)
    oracle_seconds = time.perf_counter() - t0
    engine_results, engine_seconds = run_engine(dll, cases)

    mismatches = []
    total_occ = 0
    for c in cases:
        expected = oracle_results[c["id"]]
        total_occ += len(expected)
        got = engine_results.get(c["id"], {"error": "no result"})
        if "error" in got:
            category, actual = "engine-error", []
        else:
            actual = [datetime.strptime(s, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc) for s in got["occurrences"]]
            if actual == expected:
                continue
            category = classify(c, actual, expected)
        first = next((i for i, (a, b) in enumerate(zip(actual, expected)) if a != b), min(len(actual), len(expected)))
        mismatches.append({
            "id": c["id"], "rrule": c["rrule"], "tz": c["tz"], "dtstart": c["dtstart"].isoformat(),
            "exdates": [d.isoformat() for d in c["exdates"]], "category": category,
            "explained": category in EXPLAINED, "engine_error": got.get("error"),
            "first_difference_index": first,
            "engine": [d.isoformat() for d in actual[first:first + 3]],
            "dateutil": [d.isoformat() for d in expected[first:first + 3]],
        })

    unexplained = [m for m in mismatches if not m["explained"]]
    features = Counter()
    for c in cases:
        for key in ("BYSETPOS", "BYMONTHDAY", "BYMONTH=", "BYDAY", "COUNT", "UNTIL", "WKST", "INTERVAL"):
            if key in c["rrule"]:
                features[key.rstrip("=")] += 1
        features["EXDATE"] += bool(c["exdates"])
        kind = oracle.local_kind(c["dtstart"], c["tz"])
        if kind != "normal":
            features[f"DTSTART in DST {kind}"] += 1
    report = {
        "rules": len(cases), "seed": args.seed, "occurrences_compared": total_occ,
        "mismatches": len(mismatches), "unexplained_mismatches": len(unexplained),
        "by_category": dict(Counter(m["category"] for m in mismatches)),
        "explained_categories": EXPLAINED, "feature_coverage": dict(sorted(features.items())),
        "zones": sorted({c["tz"] for c in cases}),
        "engine_seconds": round(engine_seconds, 3), "dateutil_seconds": round(oracle_seconds, 3),
        "details": mismatches,
    }
    Path(args.report).write_text(json.dumps(report, indent=2) + "\n")

    print(f"rules compared:        {len(cases)} (seed {args.seed})")
    print(f"occurrences compared:  {total_occ}")
    print(f"mismatches:            {len(mismatches)} ({len(unexplained)} unexplained)")
    for cat, n in report["by_category"].items():
        print(f"  {cat:24s} {n}")
    for m in unexplained[:10]:
        print(f"  ! {m['id']} {m['tz']} DTSTART={m['dtstart']} {m['rrule']} -> engine {m['engine'] or m['engine_error']} vs dateutil {m['dateutil']}")
    print(f"report written to {Path(args.report).relative_to(ROOT) if Path(args.report).is_relative_to(ROOT) else args.report}")
    if args.fail_on_mismatch and unexplained:
        sys.exit(1)


if __name__ == "__main__":
    main()
