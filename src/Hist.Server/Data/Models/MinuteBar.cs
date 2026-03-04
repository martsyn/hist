namespace Hist.Server.Data.Models;

public record MinuteBar(
    string Symbol,
    DateTimeOffset Ts,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    ulong Volume
);
