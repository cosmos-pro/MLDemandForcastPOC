using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record PromocaoRow(
    DateOnly DataInicio,
    DateOnly DataFim,
    string Sku,
    int? LojaId,
    string? Tipo,
    decimal? DescontoPct);

public sealed class PromocaoFaker : Faker<PromocaoRow>
{
    private static readonly string[] Tipos = ["desconto", "leve3pague2", "queima", "campanha"];

    public PromocaoFaker(IList<int> lojaIds, IList<string> skus, DateOnly start, DateOnly end, int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var inicio = DateOnly.FromDateTime(f.Date.Between(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue)));
            var fim = inicio.AddDays(f.Random.Int(3, 30));
            return new PromocaoRow(
                DataInicio: inicio,
                DataFim: fim,
                Sku: f.Random.ListItem(skus),
                LojaId: f.Random.Bool(0.3f) ? f.Random.ListItem(lojaIds) : null,
                Tipo: f.Random.ArrayElement(Tipos),
                DescontoPct: Math.Round((decimal)f.Random.Double(5, 50), 2));
        });
    }
}
