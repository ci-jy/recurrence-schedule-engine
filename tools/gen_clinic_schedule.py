#!/usr/bin/env python3
"""Generates data/clinic_schedule.json: a seeded, realistic schedule of ~500 recurring series for a
two-site outpatient clinic (Toronto and London) that shares telehealth lines across time zones."""
import json
import random
from datetime import date, timedelta
from pathlib import Path

SEED = 20260301
OUT = Path(__file__).resolve().parent.parent / "data" / "clinic_schedule.json"

SITES = {
    "Toronto": ("America/Toronto", [f"Toronto Room {n}" for n in range(101, 121)]),
    "London": ("Europe/London", [f"London Room {c}" for c in "ABCDEFGHIJKL"]),
}
TELEHEALTH = [f"Telehealth Line {n}" for n in range(1, 5)]
SERVICES = ["Physiotherapy", "Diabetes Clinic", "Cardiology", "Dermatology", "Orthopaedics", "Paediatrics",
            "Speech Therapy", "Oncology Follow-up", "Prenatal Care", "Mental Health Group", "Sleep Study Review",
            "Wound Care", "Allergy Shots", "Occupational Therapy", "Audiology", "Nutrition Counselling"]
DAYS = ["MO", "TU", "WE", "TH", "FR"]
HOLIDAYS = {  # observed closures used as EXDATEs
    "America/Toronto": ["2025-12-25", "2025-12-26", "2026-01-01", "2026-02-16", "2026-04-03", "2026-05-18"],
    "Europe/London": ["2025-12-25", "2025-12-26", "2026-01-01", "2026-04-03", "2026-04-06", "2026-05-04"],
}


def pattern(rng):
    """(label, rrule, duration_minutes, wants_holiday_exdates)"""
    kind = rng.choices(["weekly", "biweekly", "multi", "monthly-nth", "monthly-last", "quarterly", "daily", "monthly-day"],
                       weights=[30, 12, 22, 10, 6, 4, 6, 10])[0]
    if kind == "weekly":
        return "weekly", f"FREQ=WEEKLY;BYDAY={rng.choice(DAYS)}", rng.choice([30, 45, 60, 90, 120]), True
    if kind == "biweekly":
        return "every two weeks", f"FREQ=WEEKLY;INTERVAL=2;BYDAY={rng.choice(DAYS)}", rng.choice([60, 90, 120]), True
    if kind == "multi":
        days = sorted(rng.sample(DAYS, rng.randint(2, 3)), key=DAYS.index)
        return "twice weekly", f"FREQ=WEEKLY;BYDAY={','.join(days)}", rng.choice([30, 45, 60]), True
    if kind == "monthly-nth":
        n = rng.choice([1, 2, 3, -1])
        return "monthly", f"FREQ=MONTHLY;BYDAY={n}{rng.choice(DAYS)}", rng.choice([60, 120, 180]), False
    if kind == "monthly-last":
        return "last weekday review", "FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1", rng.choice([60, 90]), False
    if kind == "quarterly":
        return "quarterly maintenance", f"FREQ=MONTHLY;INTERVAL=3;BYDAY={rng.choice([1, 2])}SA", rng.choice([180, 240]), False
    if kind == "daily":
        return "weekday mornings", "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", rng.choice([30, 45]), True
    return "monthly", f"FREQ=MONTHLY;BYMONTHDAY={rng.choice([1, 10, 15, -1])}", rng.choice([60, 120]), False


def bound(rng, rrule, start):
    r = rng.random()
    if r < 0.15:
        return rrule + f";COUNT={rng.randint(6, 40)}"
    if r < 0.3:
        until = start + timedelta(days=rng.randint(90, 540))
        return rrule + f";UNTIL={until:%Y%m%d}T235959Z"
    return rrule


def main():
    rng = random.Random(SEED)
    series = []
    resources = [(tz, room) for tz, rooms in SITES.values() for room in rooms]
    resources += [(rng.choice(["America/Toronto", "Europe/London"]), line) for line in TELEHEALTH]
    for tz, resource in resources:
        per_resource = rng.randint(12, 16) if "Telehealth" not in resource else rng.randint(10, 14)
        for _ in range(per_resource):
            # Telehealth lines are booked from both sites, each in its own zone.
            zone = tz if "Telehealth" not in resource else rng.choice(["America/Toronto", "Europe/London"])
            label, rrule, minutes, holidays = pattern(rng)
            start = date(2025, 9, 1) + timedelta(days=rng.randrange(0, 150))
            hour = rng.choice(range(7, 18)) if "quarterly" not in label else rng.choice([6, 7, 8])
            minute = rng.choice([0, 15, 30, 45])
            service = rng.choice(SERVICES) if "maintenance" not in label else "Equipment maintenance"
            series.append({
                "title": f"{service} ({label})",
                "resource": resource,
                "rrule": bound(rng, rrule, start),
                "dtStart": f"{start:%Y-%m-%d}T{hour:02d}:{minute:02d}:00",
                "timeZone": zone,
                "durationMinutes": minutes,
                "exDates": [f"{d}T{hour:02d}:{minute:02d}:00" for d in HOLIDAYS[zone]] if holidays else [],
            })
    OUT.write_text(json.dumps(series, indent=1) + "\n")
    print(f"wrote {len(series)} series over {len(resources)} resources to {OUT}")


if __name__ == "__main__":
    main()
