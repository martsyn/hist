namespace Hist.Server.Data.Models;

public record Split(
    string Symbol,
    DateOnly Date,
    decimal Numerator,
    decimal Denominator
);
