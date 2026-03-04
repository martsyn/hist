namespace Hist.Server.Configuration;

public class AppSettings
{
    public int Port { get; set; } = 8088;
    public ClickHouseSettings ClickHouse { get; set; } = new();
    public TiingoSettings Tiingo { get; set; } = new();
    public ScheduleSettings Schedules { get; set; } = new();
}

public class ClickHouseSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int TcpPort { get; set; } = 9000;
    public string WriteUser { get; set; } = "hist_writer";
    public string WritePassword { get; set; } = "changeme";
    public string ReadUser { get; set; } = "hist_reader";
    public string ReadPassword { get; set; } = "changeme_reader";
    public string Database { get; set; } = "default";

    public string WriteConnectionString =>
        $"Host={Host};Port={TcpPort};User={WriteUser};Password={WritePassword};Database={Database}";

    public string ReadConnectionString =>
        $"Host={Host};Port={TcpPort};User={ReadUser};Password={ReadPassword};Database={Database}";
}

public class TiingoSettings
{
    public string Token { get; set; } = "";
    public int MaxThreads { get; set; } = 4;
}

public class ScheduleSettings
{
    public string DailyBars { get; set; } = "0 0 6 * * ?";
    public string MinuteBars { get; set; } = "0 30 6 * * ?";
    public string Dividends { get; set; } = "0 0 7 * * ?";
    public string Splits { get; set; } = "0 15 7 * * ?";
    public string Earnings { get; set; } = "0 30 7 * * ?";
}
