namespace Hist.Server.Data.Models;

public record DailyBar(
    string Symbol,
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    ulong Volume,
    decimal AdjOpen,
    decimal AdjHigh,
    decimal AdjLow,
    decimal AdjClose,
    ulong AdjVolume
);
