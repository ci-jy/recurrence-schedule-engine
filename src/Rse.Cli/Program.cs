using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rse.Engine;

// Batch expander used by the differential test harness.
// Reads one JSON request per line on stdin and writes one JSON result per line on stdout:
//   {"id":"r1","rrule":"FREQ=...","dtstart":"2024-03-10T02:30:00","tz":"America/Toronto",
//    "exdates":["2024-03-17T02:30:00"],"horizon":"2029-01-01T00:00:00Z","limit":200}
//   -> {"id":"r1","occurrences":["2024-03-10T07:30:00Z", ...]}  or  {"id":"r1","error":"..."}
if (args.Length != 1 || args[0] != "expand")
{
    Console.Error.WriteLine("usage: Rse.Cli expand < requests.jsonl > results.jsonl");
    return 2;
}

var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = false };
string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    var req = JsonNode.Parse(line)!.AsObject();
    var result = new JsonObject { ["id"] = req["id"]!.GetValue<string>() };
    try
    {
        var dtStart = ParseLocal(req["dtstart"]!.GetValue<string>());
        var exDates = req["exdates"]?.AsArray().Select(n => ParseLocal(n!.GetValue<string>())) ?? [];
        var set = new RecurrenceSet(req["rrule"]!.GetValue<string>(), dtStart, req["tz"]!.GetValue<string>(), exDates);
        var horizon = DateTime.Parse(req["horizon"]!.GetValue<string>(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var limit = req["limit"]?.GetValue<int>() ?? 200;
        var occ = new JsonArray();
        foreach (var o in set.Enumerate().TakeWhile(o => o.UtcStart <= horizon).Take(limit))
            occ.Add(o.UtcStart.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        result["occurrences"] = occ;
    }
    catch (Exception e) when (e is FormatException or ArgumentException)
    {
        result["error"] = e.Message;
    }
    output.WriteLine(result.ToJsonString());
}
output.Flush();
return 0;

static DateTime ParseLocal(string s) =>
    DateTime.ParseExact(s, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
