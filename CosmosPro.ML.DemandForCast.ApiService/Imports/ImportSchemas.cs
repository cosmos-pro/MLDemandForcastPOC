namespace CosmosPro.ML.DemandForCast.ApiService.Imports;

/// <summary>
/// Define quais CSVs são esperados no ZIP de import e quais colunas cada um
/// precisa conter (case-insensitive, qualquer ordem). Mantido em sync com o
/// schema do banco Stage (ver Docs/schema.md).
/// </summary>
internal static class ImportSchemas
{
    public static readonly IReadOnlyDictionary<string, string[]> ExpectedFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["lojas.csv"] = ["LojaId", "Nome", "UF", "Cidade"],
        ["produtos.csv"] = ["Sku", "Nome"],
        ["vendas.csv"] = ["Data", "LojaId", "Sku", "Quantidade", "PrecoUnitario", "ValorTotal"],
        ["estoques_diarios.csv"] = ["Data", "LojaId", "Sku", "QuantidadeEmEstoque"],
        ["compras.csv"] = ["DataPedido", "LojaId", "Sku", "Quantidade"],
        ["promocoes.csv"] = ["DataInicio", "DataFim", "Sku"],
        ["mercado_iqvia.csv"] = ["Mes", "PrincipioAtivo", "UF", "DemandaMercadoUnidades"],
    };
}
