using Hist.Server.Data;

namespace Hist.Server.Api;

public static class UniverseEndpoints
{
    public static RouteGroupBuilder MapUniverseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUniverse);
        return group;
    }

    private static async Task<IResult> GetUniverse(ClickHouseRepository repo)
    {
        var coverage = await repo.GetAllCoverageAsync();

        var bySymbol = coverage
            .GroupBy(c => c.Symbol)
            .Select(g => new
            {
                symbol = g.Key,
                coverage = g.Select(c => new
                {
                    data_type = c.DataType,
                    start_date = c.StartDate,
                    start_ts = c.StartTs,
                    end_date = c.EndDate,
                    end_ts = c.EndTs,
                    updated_at = c.UpdatedAt
                }).ToList()
            })
            .OrderBy(x => x.symbol)
            .ToList();

        return Results.Ok(bySymbol);
    }
}
