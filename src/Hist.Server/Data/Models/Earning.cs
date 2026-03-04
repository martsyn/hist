namespace Hist.Server.Data.Models;

public record Earning(
    string Symbol,
    DateOnly Period,
    decimal? EpsActual,
    decimal? EpsEstimate,
    decimal? RevenueActual,
    decimal? RevenueEstimate,
    DateOnly? ReportedDate
);
