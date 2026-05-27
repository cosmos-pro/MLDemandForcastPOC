using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record EstoqueDiarioRow(
    DateOnly Data,
    int LojaId,
    string Sku,
    decimal QuantidadeEmEstoque);

public sealed class EstoqueDiarioFaker : Faker<EstoqueDiarioRow>
{
    public EstoqueDiarioFaker(IList<int> lojaIds, IList<string> skus, DateOnly start, DateOnly end, int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var dateTime = f.Date.Between(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
            return new EstoqueDiarioRow(
                Data: DateOnly.FromDateTime(dateTime),
                LojaId: f.Random.ListItem(lojaIds),
                Sku: f.Random.ListItem(skus),
                QuantidadeEmEstoque: Math.Round((decimal)f.Random.Double(0, 200), 3));
        });
    }
}
