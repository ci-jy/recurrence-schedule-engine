"""Reference expansion with python-dateutil's rrule, the independent oracle for the engine."""
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

from dateutil.rrule import rrulestr


def expand(rrule_text, dtstart_naive, zone_name, exdates_naive, horizon_utc, limit):
    """UTC occurrences (as datetimes) of the recurrence set, up to `horizon_utc` and at most `limit`.

    Local wall-clock times are attached to the zone with fold=0, which zoneinfo resolves exactly as
    RFC 5545 prescribes: gap times use the pre-transition offset, ambiguous times the first instance.
    """
    zone = ZoneInfo(zone_name)
    rule = rrulestr(rrule_text, dtstart=dtstart_naive.replace(tzinfo=zone))
    excluded = set(exdates_naive)
    out = []
    for occ in rule:
        utc = occ.astimezone(timezone.utc)
        if utc > horizon_utc:
            break
        if occ.replace(tzinfo=None) in excluded:
            continue
        out.append(utc)
        if len(out) >= limit:
            break
    return out


def local_kind(naive_local, zone_name):
    """'gap', 'overlap' or 'normal' for a local wall-clock time in the zone."""
    zone = ZoneInfo(zone_name)
    a = naive_local.replace(tzinfo=zone, fold=0)
    b = naive_local.replace(tzinfo=zone, fold=1)
    if a.utcoffset() == b.utcoffset():
        return "normal"
    roundtrip = a.astimezone(timezone.utc).astimezone(zone).replace(tzinfo=None)
    return "gap" if roundtrip != naive_local else "overlap"


WEEKDAY_CODES = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"]


def expand_full_first_week(rrule_text, dtstart_naive, zone_name, exdates_naive, horizon_utc, limit):
    """dateutil expansion of a WEEKLY rule with the first week evaluated in full (RFC 5545 reading).

    dateutil starts the first WEEKLY period at DTSTART rather than at the WKST-aligned week start, so
    BYSETPOS counts positions in a truncated week. Here DTSTART is moved back to the week start,
    COUNT is applied by hand, and occurrences before the real DTSTART are dropped.
    """
    from datetime import timedelta

    parts = dict(p.split("=", 1) for p in rrule_text.split(";"))
    wkst = WEEKDAY_CODES.index(parts.get("WKST", "MO"))
    count = int(parts.pop("COUNT")) if "COUNT" in parts else None
    week_start = dtstart_naive - timedelta(days=(dtstart_naive.weekday() - wkst) % 7)
    text = ";".join(f"{k}={v}" for k, v in parts.items())
    zone = ZoneInfo(zone_name)
    rule = rrulestr(text, dtstart=week_start.replace(tzinfo=zone))
    excluded = set(exdates_naive)
    out, produced = [], 0
    for occ in rule:
        if occ.replace(tzinfo=None) < dtstart_naive:
            continue
        utc = occ.astimezone(timezone.utc)
        if utc > horizon_utc or (count is not None and produced >= count):
            break
        produced += 1
        if occ.replace(tzinfo=None) in excluded:
            continue
        out.append(utc)
        if len(out) >= limit:
            break
    return out
