using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CosmosPro.ML.DemandForCast.ApiService.Stage;

internal static class StageEndpoints
{
    public static IEndpointRouteBuilder MapStageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stage").WithTags("Stage");

        group.MapGet("/tables", () =>
            Results.Ok(StageBrowser.Tables.Values
                .Select(t => new StageTableSummary(t.Alias, t.Label, t.Icon))
                .ToList()))
            .WithName("ListStageTables")
            .Produces<List<StageTableSummary>>();

        group.MapGet("/{table}", BrowseAsync)
            .WithName("BrowseStageTable")
            .Produces<StagePageResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> BrowseAsync(
        string table,
        SqlConnection connection,
        ILogger<Program> logger,
        CancellationToken ct,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        [FromQuery] string? orderBy = null,
        [FromQuery] bool desc = false)
    {
        if (!StageBrowser.TryGetTable(table, out var info))
            return Results.NotFound(new { error = $"Tabela '{table}' não reconhecida." });

        try
        {
            var browser = new StageBrowser(connection);
            var page = await browser.QueryAsync(info, skip, take, orderBy, desc, ct);
            return Results.Ok(new StagePageResponse(page.Total, page.Columns, page.Rows));
        }
        catch (SqlException ex)
        {
            // Tabela pode não existir ainda se o Stage nunca recebeu carga.
            logger.LogWarning(ex, "Falha ao consultar Stage.{Table}", info.TableName);
            return Results.Ok(new StagePageResponse(0, info.Columns, []));
        }
    }
}

internal sealed record StageTableSummary(string Alias, string Label, string Icon);

internal sealed record StagePageResponse(
    long Total,
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows);
