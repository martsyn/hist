namespace Hist.Server.Data.Models;

public record SeriesCoverage(
    string Symbol,
    string DataType,
    DateOnly? StartDate,
    DateTimeOffset? StartTs,
    DateOnly? EndDate,
    DateTimeOffset? EndTs,
    DateTimeOffset UpdatedAt
);
