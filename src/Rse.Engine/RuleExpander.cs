namespace Rse.Engine;

/// <summary>
/// Expands a <see cref="RecurrenceRule"/> into local wall-clock date-times, in order, starting at DTSTART.
/// COUNT, UNTIL and EXDATE are not applied here (see <see cref="RecurrenceSet"/>); the stream is
/// unbounded up to year 9999. Invalid dates such as February 30 are skipped, as RFC 5545 requires.
/// </summary>
public static class RuleExpander
{
    private const int MaxYear = 9999;

    /// <param name="rule">The rule to expand.</param>
    /// <param name="dtStart">Local DTSTART; its time of day is used for every occurrence.</param>
    /// <param name="startHint">
    /// Optional local time to fast-forward to. Periods that end before it are skipped without being
    /// expanded. Only valid for rules without COUNT (COUNT needs every earlier occurrence).
    /// </param>
    public static IEnumerable<DateTime> Expand(RecurrenceRule rule, DateTime dtStart, DateTime? startHint = null)
    {
        if (startHint is not null && rule.Count is not null)
            throw new ArgumentException("startHint cannot be used with a COUNT rule", nameof(startHint));
        return Iterate(rule, DateTime.SpecifyKind(dtStart, DateTimeKind.Unspecified), startHint);
    }

    private static IEnumerable<DateTime> Iterate(RecurrenceRule rule, DateTime dtStart, DateTime? startHint)
    {
        var plan = new Plan(rule, dtStart);
        var time = dtStart.TimeOfDay;
        var startDate = DateOnly.FromDateTime(dtStart);
        long index = 0;

        if (startHint is DateTime hint && hint > dtStart)
        {
            var periods = PeriodsBetween(rule, plan, startDate, DateOnly.FromDateTime(hint));
            index = periods - periods % rule.Interval;
        }

        var buffer = new List<DateOnly>(32);
        while (true)
        {
            var periodStart = PeriodStart(rule.Frequency, plan, startDate, index);
            if (periodStart is null) yield break;
            buffer.Clear();
            plan.Collect(periodStart.Value, buffer);
            ApplySetPos(rule.BySetPos, buffer);
            foreach (var day in buffer)
            {
                var dt = day.ToDateTime(TimeOnly.MinValue) + time;
                if (dt >= dtStart) yield return dt;
            }
            index += rule.Interval;
        }
    }

    /// <summary>First day of the <paramref name="index"/>-th period after the one containing DTSTART, or null past year 9999.</summary>
    private static DateOnly? PeriodStart(Frequency freq, Plan plan, DateOnly start, long index)
    {
        switch (freq)
        {
            case Frequency.Yearly:
                var y = start.Year + index;
                return y > MaxYear ? null : new DateOnly((int)y, 1, 1);
            case Frequency.Monthly:
                var m = (long)start.Year * 12 + start.Month - 1 + index;
                return m / 12 > MaxYear ? null : new DateOnly((int)(m / 12), (int)(m % 12) + 1, 1);
            case Frequency.Weekly:
                var dn = (long)plan.FirstWeekStart.DayNumber + index * 7;
                return dn + 6 > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber((int)dn);
            default:
                var d = (long)start.DayNumber + index;
                return d > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber((int)d);
        }
    }

    private static long PeriodsBetween(RecurrenceRule rule, Plan plan, DateOnly start, DateOnly target) => rule.Frequency switch
    {
        Frequency.Yearly => target.Year - start.Year,
        Frequency.Monthly => (target.Year - start.Year) * 12L + target.Month - start.Month,
        Frequency.Weekly => (target.DayNumber - plan.FirstWeekStart.DayNumber) / 7,
        _ => target.DayNumber - start.DayNumber,
    };

    private static void ApplySetPos(IReadOnlyList<int> setPos, List<DateOnly> days)
    {
        if (setPos.Count == 0 || days.Count == 0) return;
        var picked = new SortedSet<DateOnly>();
        foreach (var pos in setPos)
        {
            var i = pos > 0 ? pos - 1 : days.Count + pos;
            if (i >= 0 && i < days.Count) picked.Add(days[i]);
        }
        days.Clear();
        days.AddRange(picked);
    }

    /// <summary>Pre-computed filters for one rule, including the RFC 5545 defaults taken from DTSTART.</summary>
    private sealed class Plan
    {
        private readonly Frequency _freq;
        private readonly bool[] _month = new bool[13];
        private readonly bool _hasMonth;
        private readonly int[] _monthDays;
        private readonly bool[] _anyDay = new bool[7];
        private readonly WeekdayNum[] _ordinalDays;
        private readonly bool _hasDay;
        public DateOnly FirstWeekStart { get; }

        public Plan(RecurrenceRule rule, DateTime dtStart)
        {
            _freq = rule.Frequency;
            IEnumerable<int> months = rule.ByMonth;
            IEnumerable<int> monthDays = rule.ByMonthDay;
            IEnumerable<WeekdayNum> days = rule.ByDay;

            if (rule.ByMonthDay.Count == 0 && rule.ByDay.Count == 0)
            {
                switch (rule.Frequency)
                {
                    case Frequency.Yearly:
                        if (rule.ByMonth.Count == 0) months = [dtStart.Month];
                        monthDays = [dtStart.Day];
                        break;
                    case Frequency.Monthly:
                        monthDays = [dtStart.Day];
                        break;
                    case Frequency.Weekly:
                        days = [new WeekdayNum(0, dtStart.DayOfWeek)];
                        break;
                }
            }

            foreach (var m in months) { _month[m] = true; _hasMonth = true; }
            _monthDays = monthDays.ToArray();

            // Ordinals only have meaning for MONTHLY and YEARLY; elsewhere they behave like plain weekdays.
            var ordinalsAllowed = rule.Frequency is Frequency.Monthly or Frequency.Yearly;
            var ordinal = new List<WeekdayNum>();
            foreach (var d in days)
            {
                _hasDay = true;
                if (d.Ordinal != 0 && ordinalsAllowed) ordinal.Add(d);
                else _anyDay[(int)d.Day] = true;
            }
            _ordinalDays = ordinal.ToArray();

            var start = DateOnly.FromDateTime(dtStart);
            var back = ((int)start.DayOfWeek - (int)rule.WeekStart + 7) % 7;
            FirstWeekStart = start.AddDays(-back);
        }

        /// <summary>Appends the matching days of the period starting at <paramref name="periodStart"/>, in order.</summary>
        public void Collect(DateOnly periodStart, List<DateOnly> output)
        {
            switch (_freq)
            {
                case Frequency.Yearly when _hasMonth:
                    // Ordinal BYDAY values count within each month when BYMONTH is present.
                    for (var m = 1; m <= 12; m++)
                    {
                        if (!_month[m]) continue;
                        var first = new DateOnly(periodStart.Year, m, 1);
                        CollectFrame(first, LastOfMonth(first), output);
                    }
                    break;
                case Frequency.Yearly:
                    CollectFrame(periodStart, new DateOnly(periodStart.Year, 12, 31), output);
                    break;
                case Frequency.Monthly:
                    if (_hasMonth && !_month[periodStart.Month]) return;
                    CollectFrame(periodStart, LastOfMonth(periodStart), output);
                    break;
                case Frequency.Weekly:
                    CollectFrame(periodStart, periodStart.AddDays(6), output);
                    break;
                default:
                    CollectFrame(periodStart, periodStart, output);
                    break;
            }
        }

        private static DateOnly LastOfMonth(DateOnly first) =>
            new(first.Year, first.Month, DateTime.DaysInMonth(first.Year, first.Month));

        private void CollectFrame(DateOnly first, DateOnly last, List<DateOnly> output)
        {
            if (_freq != Frequency.Weekly && _freq != Frequency.Daily && !_hasDay && _monthDays.Length > 0
                && (_freq != Frequency.Yearly || _hasMonth))
            {
                // Fast path: frame is a single month and only BYMONTHDAY selects days.
                CollectMonthDays(first, output);
                return;
            }
            for (var d = first; d <= last; d = d.AddDays(1))
            {
                if (Matches(d, first, last)) output.Add(d);
            }
        }

        private void CollectMonthDays(DateOnly monthStart, List<DateOnly> output)
        {
            var dim = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var before = output.Count;
            foreach (var md in _monthDays)
            {
                var day = md > 0 ? md : dim + md + 1;
                if (day >= 1 && day <= dim) output.Add(new DateOnly(monthStart.Year, monthStart.Month, day));
            }
            if (_monthDays.Length > 1)
            {
                output.Sort(before, output.Count - before, null);
                for (var i = output.Count - 1; i > before; i--)
                    if (output[i] == output[i - 1]) output.RemoveAt(i);
            }
        }

        private bool Matches(DateOnly d, DateOnly frameFirst, DateOnly frameLast)
        {
            if (_hasMonth && !_month[d.Month]) return false;
            if (_monthDays.Length > 0)
            {
                var dim = DateTime.DaysInMonth(d.Year, d.Month);
                var ok = false;
                foreach (var md in _monthDays)
                {
                    if (d.Day == (md > 0 ? md : dim + md + 1)) { ok = true; break; }
                }
                if (!ok) return false;
            }
            if (!_hasDay) return true;
            var dow = d.DayOfWeek;
            if (_anyDay[(int)dow]) return true;
            foreach (var od in _ordinalDays)
            {
                if (od.Day != dow) continue;
                var n = od.Ordinal > 0
                    ? (d.DayNumber - frameFirst.DayNumber) / 7 + 1
                    : -((frameLast.DayNumber - d.DayNumber) / 7 + 1);
                if (n == od.Ordinal) return true;
            }
            return false;
        }
    }
}
