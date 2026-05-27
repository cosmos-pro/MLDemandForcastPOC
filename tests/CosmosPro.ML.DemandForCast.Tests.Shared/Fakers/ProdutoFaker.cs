using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record ProdutoRow(
    string Sku,
    string Nome,
    string? Categoria,
    string? Subcategoria,
    string? Fabricante,
    string? PrincipioAtivo,
    string? Apresentacao,
    string? Ean,
    string? RegistroAnvisa,
    string? ListaControle,
    string? ClasseTerapeutica,
    bool Ativo);

public sealed class ProdutoFaker : Faker<ProdutoRow>
{
    private static readonly string[] Categorias = ["Medicamentos", "Higiene Pessoal", "Cosméticos", "Suplementos"];
    private static readonly string[] Subcategorias = ["Antitérmicos", "Antialérgicos", "Antibióticos", "Vitaminas", "Pomadas"];
    private static readonly string[] Fabricantes = ["EMS", "Medley", "Eurofarma", "Aché", "Hypera", "Pfizer"];
    private static readonly string[] PrincipiosAtivos = ["Dipirona Sódica", "Paracetamol", "Loratadina", "Amoxicilina", "Ibuprofeno", "Omeprazol"];
    private static readonly string[] ListasControle = ["A1", "A2", "A3", "B1", "B2", "C1", "C2"];

    public ProdutoFaker(int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var idx = f.IndexFaker + 1;
            return new ProdutoRow(
                Sku: $"SKU{idx:D6}",
                Nome: $"{f.PickRandom(PrincipiosAtivos)} {f.Random.Int(10, 1000)}mg {f.Random.Int(10, 60)}cp",
                Categoria: f.PickRandom(Categorias),
                Subcategoria: f.PickRandom(Subcategorias),
                Fabricante: f.PickRandom(Fabricantes),
                PrincipioAtivo: f.PickRandom(PrincipiosAtivos),
                Apresentacao: $"{f.Random.Int(10, 60)}cp {f.Random.Int(50, 1000)}mg",
                Ean: f.Random.Replace("#############"),
                RegistroAnvisa: $"1.{f.Random.Int(1000, 9999)}.{f.Random.Int(1000, 9999)}.{idx:D3}",
                ListaControle: f.Random.Bool(0.1f) ? f.PickRandom(ListasControle) : null,
                ClasseTerapeutica: f.PickRandom("N02BB", "N02BE", "R06AX", "J01CA", "M01AE", "A02BC"),
                Ativo: f.Random.Bool(0.95f));
        });
    }
}
