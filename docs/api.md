# API

The API listens on port 25080. All times in responses are UTC unless the field name says local.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/series` | Create a series |
| `GET` | `/api/series[?resource=]`, `/api/series/{id}` | List or fetch stored series |
| `DELETE` | `/api/series/{id}` | Remove a series |
| `GET` | `/api/occurrences?from=&to=[&resource=]` | Every occurrence in a window (max 366 days), sorted by UTC start |
| `POST` | `/api/conflicts/check` | Clashes between a proposed series and stored series on the same resource |

## Series

```json
{
  "title": "Hand Therapy (weekly)",
  "resource": "Toronto Room 101",
  "rrule": "FREQ=WEEKLY;BYDAY=TU,TH",
  "dtStart": "2026-01-06T10:00:00",
  "timeZone": "America/Toronto",
  "durationMinutes": 90,
  "exDates": []
}
```

- `title` is required, at most 200 characters. `resource` is optional, at most 100.
- `dtStart` and `exDates` must be local wall-clock times with no UTC offset. They are interpreted in `timeZone` (an IANA name).
- `durationMinutes` is between 1 and 1440.
- The parser supports FREQ (DAILY to YEARLY), INTERVAL, COUNT, UNTIL (UTC, local or date-only), BYDAY with ordinals, BYMONTHDAY (including negative values), BYMONTH, BYSETPOS and WKST. Anything else is rejected with an error rather than ignored.

## Conflict check

Request body: `{series, from, to, maxPerSeries}`, where `series` has the shape above and `maxPerSeries` limits how many clashes are listed per existing series. See `examples/proposed_series.json`.

The response has `seriesChecked`, `conflictingSeries`, `totalClashes` and a `conflicts` list. Each entry names the existing series and lists clashes with `proposedStartUtc`, `existingStartUtc`, `overlapStartUtc` and `overlapEndUtc`.
