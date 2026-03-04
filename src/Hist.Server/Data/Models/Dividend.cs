namespace Hist.Server.Data.Models;

public record Dividend(
    string Symbol,
    DateOnly ExDate,
    decimal Amount
);
