using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record VendaRow(
    DateOnly Data,
    int LojaId,
    string Sku,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal ValorTotal);

public sealed class VendaFaker : Faker<VendaRow>
{
    public VendaFaker(IList<int> lojaIds, IList<string> skus, DateOnly start, DateOnly end, int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var qty = Math.Round((decimal)f.Random.Double(1, 30), 3);
            var preco = Math.Round((decimal)f.Random.Double(5, 200), 4);
            var dateTime = f.Date.Between(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
            return new VendaRow(
                Data: DateOnly.FromDateTime(dateTime),
                LojaId: f.Random.ListItem(lojaIds),
                Sku: f.Random.ListItem(skus),
                Quantidade: qty,
                PrecoUnitario: preco,
                ValorTotal: Math.Round(qty * preco, 4));
        });
    }
}
