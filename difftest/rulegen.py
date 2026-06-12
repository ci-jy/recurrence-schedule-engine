"""Seeded generator of random RFC 5545 recurrence rules for differential testing.

Rules deliberately concentrate on the hard cases: DST-switching zones, start times inside DST gaps and
overlaps, start dates on transition days, BYSETPOS, negative month days, ordinal weekdays, WKST with
INTERVAL > 1, and COUNT/UNTIL bounds.
"""
import random
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

ZONES = [
    "America/Toronto", "Europe/London", "America/New_York", "Europe/Berlin", "Australia/Sydney",
    "Pacific/Auckland", "America/St_Johns", "Australia/Lord_Howe", "America/Sao_Paulo",
    "Asia/Kolkata", "UTC",
]
DAYS = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"]
DST_TIMES = ["00:30", "01:00", "01:30", "01:59", "02:00", "02:15", "02:30", "02:45", "03:00", "03:30"]


def transition_dates(zone_name, year):
    """Local dates in `year` on which the zone's UTC offset changes."""
    zone = ZoneInfo(zone_name)
    out = []
    t = datetime(year, 1, 1, tzinfo=timezone.utc)
    prev = t.astimezone(zone).utcoffset()
    while t.year == year:
        t += timedelta(hours=6)
        off = t.astimezone(zone).utcoffset()
        if off != prev:
            out.append((t - timedelta(hours=6)).astimezone(zone).date())
            prev = off
    return out


def _pick_days(rng, k):
    return rng.sample(DAYS, k)


def random_rule(rng):
    """Returns (rrule_text, dtstart_naive_local, zone_name)."""
    zone = rng.choice(ZONES)
    year = rng.randint(2021, 2028)
    if rng.random() < 0.35 and (tds := transition_dates(zone, year)):
        date = rng.choice(tds) + timedelta(days=rng.choice([0, 0, 0, -7, 7, -1]))
    else:
        date = datetime(year, 1, 1).date() + timedelta(days=rng.randrange(365))
    hhmm = rng.choice(DST_TIMES) if rng.random() < 0.45 else f"{rng.randint(6, 20):02d}:{rng.choice(['00', '15', '30', '45'])}"
    h, m = map(int, hhmm.split(":"))
    dtstart = datetime(date.year, date.month, date.day, h, m)

    freq = rng.choices(["DAILY", "WEEKLY", "MONTHLY", "YEARLY"], weights=[2, 3, 4, 2])[0]
    parts = [f"FREQ={freq}"]
    if rng.random() < 0.45:
        parts.append(f"INTERVAL={rng.randint(2, 4)}")

    bymonth = None
    if rng.random() < (0.35 if freq in ("YEARLY",) else 0.15):
        bymonth = sorted(rng.sample(range(1, 13), rng.randint(1, 4)))
        parts.append("BYMONTH=" + ",".join(map(str, bymonth)))

    byday = None
    bymonthday = None
    setpos = None
    r = rng.random()
    if freq in ("MONTHLY", "YEARLY"):
        if r < 0.3:
            # Ordinal weekdays: "2nd Tuesday", "last Friday", "20th Monday of the year".
            lim = 5 if (freq == "MONTHLY" or bymonth) else 52
            vals = []
            for _ in range(rng.randint(1, 2)):
                n = rng.choice([1, 2, 3, 4, -1, -2] + ([5, -5] if lim == 5 else [10, 20, 52, -10]))
                vals.append(f"{n}{rng.choice(DAYS)}")
            byday = vals
        elif r < 0.55:
            # Set positions over plain weekdays: "last weekday of the month".
            byday = _pick_days(rng, rng.randint(2, 5))
            setpos = rng.sample([1, 2, 3, -1, -2], rng.randint(1, 2))
        elif r < 0.8:
            pool = list(range(1, 32)) + [-1, -2, -3, -5, -10]
            if bymonth:
                pool = list(range(1, 29)) + [-1, -2, -3, -5, -10]
            bymonthday = rng.sample(pool, rng.randint(1, 3))
            if rng.random() < 0.25:
                setpos = [rng.choice([1, -1, 2])]
        elif r < 0.88:
            # Friday-the-13th style intersections of BYMONTHDAY and plain BYDAY.
            bymonthday = [rng.randint(1, 28)]
            byday = _pick_days(rng, rng.randint(2, 4))
    elif freq == "WEEKLY":
        if r < 0.7:
            byday = _pick_days(rng, rng.randint(1, 4))
            if r < 0.15 and len(byday) > 1:
                setpos = [rng.choice([1, -1])]
        if rng.random() < 0.4:
            parts.append("WKST=" + rng.choice(DAYS))
    else:  # DAILY
        if r < 0.35:
            byday = _pick_days(rng, rng.randint(1, 5))
        elif r < 0.5:
            bymonthday = rng.sample(list(range(1, 29)) + [-1, -2], rng.randint(1, 4))

    if bymonthday:
        parts.append("BYMONTHDAY=" + ",".join(map(str, bymonthday)))
    if byday:
        parts.append("BYDAY=" + ",".join(byday))
    if setpos:
        parts.append("BYSETPOS=" + ",".join(map(str, setpos)))

    b = rng.random()
    if b < 0.4:
        parts.append(f"COUNT={rng.randint(1, 60)}")
    elif b < 0.75:
        # UNTIL as a UTC instant, sometimes landing exactly on a candidate occurrence.
        span = timedelta(days=rng.randint(10, 1500))
        until_local = dtstart + span
        if rng.random() < 0.5:
            until_local = until_local.replace(hour=dtstart.hour, minute=dtstart.minute)
        until = until_local.replace(tzinfo=ZoneInfo(zone)).astimezone(timezone.utc)
        parts.append("UNTIL=" + until.strftime("%Y%m%dT%H%M%SZ"))

    rng.shuffle(parts)
    return ";".join(parts), dtstart, zone
