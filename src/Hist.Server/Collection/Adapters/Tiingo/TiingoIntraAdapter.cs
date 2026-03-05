using System.Globalization;
using Hist.Server.Data;
using Hist.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace Hist.Server.Collection.Adapters.Tiingo;

// Fetches intraday 1-minute bars from Tiingo IEX feed (history from 2016).
// Paginates backwards: first request gets the most recent 10k bars, each
// subsequent request sets endDate to the day before the earliest bar received,
// until we reach the desired start date or Tiingo has no more data.
public class TiingoIntraAdapter(
    ClickHouseRepository repo,
    HttpClient http,
    string token,
    ILogger<TiingoIntraAdapter> logger)
{
    public async Task<CollectionResult> ExecuteAsync(CollectionTask task, CancellationToken ct)
    {
        try
        {
            var symbol   = task.Symbol;
            var from     = task.Start ?? DateTimeOffset.UtcNow.AddYears(-1);
            var startStr = from.ToString("yyyy-MM-dd");
            var allBars  = new List<MinuteBar>();
            string? endStr = null;

            while (true)
            {
                var url = $"https://api.tiingo.com/iex/{Uri.EscapeDataString(symbol)}/prices" +
                          $"?startDate={startStr}&resampleFreq=1min" +
                          $"&columns=date,open,high,low,close,volume&format=csv";
                if (endStr != null) url += $"&endDate={endStr}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", $"Token {token}");
                var response = await http.SendAsync(req, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new CollectionResult(false, 0, "Symbol not found");
                if (!response.IsSuccessStatusCode)
                    return new CollectionResult(false, 0, $"HTTP {(int)response.StatusCode}");

                var csv   = await response.Content.ReadAsStringAsync(ct);
                var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2) break;

                var headers = lines[0].Split(',');
                int iDate   = Array.IndexOf(headers, "date");
                int iOpen   = Array.IndexOf(headers, "open");
                int iHigh   = Array.IndexOf(headers, "high");
                int iLow    = Array.IndexOf(headers, "low");
                int iClose  = Array.IndexOf(headers, "close");
                int iVolume = Array.IndexOf(headers, "volume");

                var pageBars = new List<MinuteBar>();
                foreach (var line in lines.Skip(1))
                {
                    var cols   = line.TrimEnd('\r').Split(',');
                    var ts     = DateTimeOffset.Parse(cols[iDate], CultureInfo.InvariantCulture);
                    var open   = decimal.Parse(cols[iOpen],   CultureInfo.InvariantCulture);
                    var high   = decimal.Parse(cols[iHigh],   CultureInfo.InvariantCulture);
                    var low    = decimal.Parse(cols[iLow],    CultureInfo.InvariantCulture);
                    var close  = decimal.Parse(cols[iClose],  CultureInfo.InvariantCulture);
                    var volume = (ulong)decimal.Parse(cols[iVolume], CultureInfo.InvariantCulture);
                    pageBars.Add(new MinuteBar(symbol, ts, open, high, low, close, volume));
                }

                allBars.AddRange(pageBars);
                var earliest = pageBars.Min(b => b.Ts);
                logger.LogInformation("Fetched {Symbol} →{End}: {Count} bars, earliest {Earliest} (total: {Total})",
                    symbol, endStr ?? "now", pageBars.Count, earliest, allBars.Count);

                if (earliest <= from) break;

                endStr = earliest.AddDays(-1).ToString("yyyy-MM-dd");
            }

            if (allBars.Count > 0)
            {
                await repo.InsertMinuteBarsAsync(allBars);
                await repo.UpsertCoverageAsync(new SeriesCoverage(
                    symbol, DataType.MinuteBars.ToApiString(),
                    null, allBars.Min(b => b.Ts),
                    null, allBars.Max(b => b.Ts),
                    DateTimeOffset.UtcNow));
            }

            logger.LogInformation("Completed intraday for {Symbol}: {Count} bars total", symbol, allBars.Count);
            return new CollectionResult(true, allBars.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TiingoIntraAdapter failed for {Symbol}", task.Symbol);
            return new CollectionResult(false, 0, ex.Message);
        }
    }
}
