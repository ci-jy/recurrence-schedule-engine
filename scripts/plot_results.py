#!/usr/bin/env python3
"""Builds docs/results.png from the committed benchmark and differential-test results."""
import csv
import json
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "bench" / "results"
UNITS = {"ns": 1e-9, "μs": 1e-6, "us": 1e-6, "ms": 1e-3, "s": 1.0}


def seconds(text):
    value, unit = text.replace(",", "").split()
    return float(value) * UNITS[unit]


def rows(name):
    with open(RES / name, newline="") as f:
        return list(csv.DictReader(f))


def main():
    counts = json.loads((RES / "occurrence_counts.json").read_text())
    expansion = rows("ExpansionBenchmarks-report.csv")
    conflicts = rows("ConflictBenchmarks-report.csv")
    latency = json.loads((RES / "latency.json").read_text())
    diff = json.loads((ROOT / "difftest" / "report.json").read_text())

    fig, axes = plt.subplots(1, 3, figsize=(15, 4.6))

    ax = axes[0]
    groups = sorted({r["Series"] for r in expansion}, key=int)
    width = 0.38
    for k, method in enumerate(["SingleThreaded", "Parallel"]):
        vals = [counts[g] / seconds(next(r["Mean"] for r in expansion if r["Series"] == g and r["Method"] == method)) / 1e6
                for g in groups]
        xs = [i + (k - 0.5) * width for i in range(len(groups))]
        bars = ax.bar(xs, vals, width, label=method.replace("SingleThreaded", "single-threaded").lower())
        ax.bar_label(bars, fmt="%.1fM", fontsize=8)
    ax.set_xticks(range(len(groups)), [f"{g} series\n({counts[g]:,} occ.)" for g in groups])
    ax.set_ylabel("million occurrences / second")
    ax.set_title("Expansion throughput (1-year window)")
    ax.legend()

    ax = axes[1]
    labels, vals = [], []
    for r in conflicts:
        labels.append("engine\n" + r["Method"].replace("AllSeries", "").lower())
        vals.append(seconds(r["Mean"]) * 1e3)
    labels += ["API p50", "API p95"]
    vals += [latency["p50_ms"], latency["p95_ms"]]
    bars = ax.bar(labels, vals, color=["#4c72b0", "#4c72b0", "#dd8452", "#c44e52"])
    ax.bar_label(bars, fmt="%.2f ms", fontsize=8)
    ax.set_ylabel("milliseconds")
    ax.set_title("Conflict check, 6-month window\n(engine: vs all 504 series; API: same-room, incl. DB + HTTP)", fontsize=10)

    ax = axes[2]
    explained = diff["mismatches"] - diff["unexplained_mismatches"]
    agree = diff["rules"] - diff["mismatches"]
    bars = ax.bar(["identical", "documented\nRFC difference", "unexplained"],
                  [agree, explained, diff["unexplained_mismatches"]], color=["#55a868", "#ccb974", "#c44e52"])
    ax.bar_label(bars, fontsize=9)
    ax.set_ylabel("random rules")
    ax.set_title(f"Differential test vs python-dateutil\n{diff['rules']:,} rules, {diff['occurrences_compared']:,} occurrences, "
                 f"{len(diff['zones'])} zones", fontsize=10)

    fig.tight_layout()
    out = ROOT / "docs" / "results.png"
    out.parent.mkdir(exist_ok=True)
    fig.savefig(out, dpi=130)
    print(f"wrote {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
