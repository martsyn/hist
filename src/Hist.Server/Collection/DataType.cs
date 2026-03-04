namespace Hist.Server.Collection;

public enum DataType
{
    DailyBars,
    MinuteBars,
    Dividends,
    Splits,
    Earnings
}

public static class DataTypeExtensions
{
    public static string ToApiString(this DataType dt) => dt switch
    {
        DataType.DailyBars => "daily_bars",
        DataType.MinuteBars => "minute_bars",
        DataType.Dividends => "dividends",
        DataType.Splits => "splits",
        DataType.Earnings => "earnings",
        _ => dt.ToString().ToLowerInvariant()
    };

    // Describes the data source and key characteristics for consumers.
    public static string ToDescription(this DataType dt) => dt switch
    {
        DataType.DailyBars  => "Tiingo EOD — consolidated tape, raw unadjusted OHLCV, 20+ years",
        DataType.MinuteBars => "Tiingo IEX — IEX exchange feed only (not consolidated), raw unadjusted OHLCV, 1-min bars since 2016, no gap-fill",
        DataType.Dividends  => "Tiingo EOD — cash dividend amounts (raw, not adjusted), co-fetched with daily bars",
        DataType.Splits     => "Tiingo EOD — split factor (numerator/1), co-fetched with daily bars",
        DataType.Earnings   => "Yahoo Finance (OoplesFinance) — EPS actual/estimate only, no revenue",
        _ => ""
    };

    public static DataType FromApiString(string s) => s switch
    {
        "daily_bars" => DataType.DailyBars,
        "minute_bars" => DataType.MinuteBars,
        "dividends" => DataType.Dividends,
        "splits" => DataType.Splits,
        "earnings" => DataType.Earnings,
        _ => throw new ArgumentException($"Unknown data type: {s}")
    };
}
