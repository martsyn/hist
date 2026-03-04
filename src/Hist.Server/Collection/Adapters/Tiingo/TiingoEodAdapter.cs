using System.Globalization;
using Hist.Server.Data;
using Hist.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace Hist.Server.Collection.Adapters.Tiingo;

// Fetches EOD data from Tiingo: raw OHLCV + dividends + splits in one CSV call.
// Always stores raw (unadjusted) prices. Auth via Authorization header per-request.
public class TiingoEodAdapter(
    ClickHouseRepository repo,
    HttpClient http,
    string token,
    ILogger<TiingoEodAdapter> logger)
{
    public async Task<CollectionResult> ExecuteAsync(CollectionTask task, CancellationToken ct)
    {
        try
        {
            var symbol = task.Symbol;
            var from = task.Start ?? DateTimeOffset.UtcNow.AddYears(-20);
            var startDate = from.ToString("yyyy-MM-dd");

            var url = $"https://api.tiingo.com/tiingo/daily/{Uri.EscapeDataString(symbol)}/prices" +
                      $"?startDate={startDate}&format=csv";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Authorization", $"Token {token}");
            var response = await http.SendAsync(req, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new CollectionResult(false, 0, "Symbol not found");
            if (!response.IsSuccessStatusCode)
                return new CollectionResult(false, 0, $"HTTP {(int)response.StatusCode}");

            var csv = await response.Content.ReadAsStringAsync(ct);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return new CollectionResult(true, 0);

            var headers = lines[0].Split(',');
            int iDate        = Array.IndexOf(headers, "date");
            int iOpen        = Array.IndexOf(headers, "open");
            int iHigh        = Array.IndexOf(headers, "high");
            int iLow         = Array.IndexOf(headers, "low");
            int iClose       = Array.IndexOf(headers, "close");
            int iVolume      = Array.IndexOf(headers, "volume");
            int iDivCash     = Array.IndexOf(headers, "divCash");
            int iSplitFactor = Array.IndexOf(headers, "splitFactor");

            var bars      = new List<DailyBar>();
            var dividends = new List<Dividend>();
            var splits    = new List<Split>();

            foreach (var line in lines.Skip(1))
            {
                var cols = line.TrimEnd('\r').Split(',');
                var date    = DateOnly.FromDateTime(DateTime.Parse(cols[iDate], CultureInfo.InvariantCulture));
                var open    = decimal.Parse(cols[iOpen],   CultureInfo.InvariantCulture);
                var high    = decimal.Parse(cols[iHigh],   CultureInfo.InvariantCulture);
                var low     = decimal.Parse(cols[iLow],    CultureInfo.InvariantCulture);
                var close   = decimal.Parse(cols[iClose],  CultureInfo.InvariantCulture);
                var volume  = ulong.Parse(cols[iVolume],   CultureInfo.InvariantCulture);

                bars.Add(new DailyBar(symbol, date, open, high, low, close, volume));

                if (iDivCash >= 0)
                {
                    var divCash = decimal.Parse(cols[iDivCash], CultureInfo.InvariantCulture);
                    if (divCash > 0)
                        dividends.Add(new Dividend(symbol, date, divCash));
                }

                if (iSplitFactor >= 0)
                {
                    var factor = decimal.Parse(cols[iSplitFactor], CultureInfo.InvariantCulture);
                    if (factor != 1.0m)
                        splits.Add(new Split(symbol, date, factor, 1m));
                }
            }

            if (bars.Count > 0)      await repo.InsertDailyBarsAsync(bars);
            if (dividends.Count > 0) await repo.InsertDividendsAsync(dividends);
            if (splits.Count > 0)    await repo.InsertSplitsAsync(splits);

            var now = DateTimeOffset.UtcNow;
            var minDate = bars.Count > 0 ? bars.Min(b => b.Date) : (DateOnly?)null;
            var maxDate = bars.Count > 0 ? bars.Max(b => b.Date) : (DateOnly?)null;

            foreach (var dt in new[] { DataType.DailyBars, DataType.Dividends, DataType.Splits })
                await repo.UpsertCoverageAsync(new SeriesCoverage(
                    symbol, dt.ToApiString(), minDate, null, maxDate, null, now));

            logger.LogInformation("Completed EOD for {Symbol}: {Bars} bars, {Divs} dividends, {Splits} splits",
                symbol, bars.Count, dividends.Count, splits.Count);

            return new CollectionResult(true, bars.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TiingoEodAdapter failed for {Symbol}", task.Symbol);
            return new CollectionResult(false, 0, ex.Message);
        }
    }
}
