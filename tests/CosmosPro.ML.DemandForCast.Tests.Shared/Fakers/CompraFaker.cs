using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record CompraRow(
    DateOnly DataPedido,
    DateOnly? DataRecebimento,
    int LojaId,
    string Sku,
    decimal Quantidade,
    string? Fornecedor);

public sealed class CompraFaker : Faker<CompraRow>
{
    private static readonly string[] Fornecedores = ["Distribuidor ABC", "Distribuidor XYZ", "Atacadão Pharma", "MedSupply BR"];

    public CompraFaker(IList<int> lojaIds, IList<string> skus, DateOnly start, DateOnly end, int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var pedido = DateOnly.FromDateTime(f.Date.Between(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue)));
            var leadDays = f.Random.Int(3, 14);
            var recebimento = f.Random.Bool(0.85f) ? pedido.AddDays(leadDays) : (DateOnly?)null;
            return new CompraRow(
                DataPedido: pedido,
                DataRecebimento: recebimento,
                LojaId: f.Random.ListItem(lojaIds),
                Sku: f.Random.ListItem(skus),
                Quantidade: Math.Round((decimal)f.Random.Double(10, 500), 3),
                Fornecedor: f.Random.ArrayElement(Fornecedores));
        });
    }
}
