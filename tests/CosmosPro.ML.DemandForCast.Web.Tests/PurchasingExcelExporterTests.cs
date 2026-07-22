using ClosedXML.Excel;
using CosmosPro.ML.DemandForCast.Web;

namespace CosmosPro.ML.DemandForCast.Web.Tests;

public sealed class PurchasingExcelExporterTests
{
    private const string Classic = "emax-eseg";
    private const string Forecast = "forecast-rop";

    [Fact]
    public void BuildLivroPedidos_gera_planilha_com_cabecalho_datas_e_numeros()
    {
        var output = OutputCom(
            pedidos:
            [
                new OrderRecordView(new DateOnly(2025, 12, 31), "SKU00014", 1, Quantidade: 24, PosicaoAntes: 29, ReorderPoint: 31, OrderUpToLevel: 53),
                new OrderRecordView(new DateOnly(2025, 12, 30), "SKU00017", 1, Quantidade: 22, PosicaoAntes: 26, ReorderPoint: 27, OrderUpToLevel: 48),
            ],
            produtos: new Dictionary<string, string> { ["SKU00014"] = "Amoxicilina EMS Cx 20 cps" });

        var bytes = PurchasingExcelExporter.BuildLivroPedidos(output, Classic);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Livro de pedidos");

        // Cabeçalho.
        ws.Cell(1, 1).GetString().Should().Be("Data");
        ws.Cell(1, 8).GetString().Should().Be("Qtd pedida");

        // Duas linhas de dados.
        ws.LastRowUsed()!.RowNumber().Should().Be(3);

        // Produto resolvido via dicionário.
        ws.Cell(2, 3).GetString().Should().Be("Amoxicilina EMS Cx 20 cps");

        // Data como célula de data (não texto) e quantidade como número (locale-safe no Excel).
        ws.Cell(2, 1).DataType.Should().Be(XLDataType.DateTime);
        ws.Cell(2, 8).DataType.Should().Be(XLDataType.Number);
        ws.Cell(2, 8).GetValue<int>().Should().Be(24);
    }

    [Fact]
    public void BuildSugestaoHoje_funde_politicas_por_sku_loja_e_calcula_delta()
    {
        var output = OutputCom(
            politicas:
            [
                Politica(Classic, listaCompra: [Item("SKU00246", 1, posicao: 8, qtd: 8)]),
                Politica(Forecast, listaCompra: [Item("SKU00246", 1, posicao: 8, qtd: 14)]),
            ],
            produtos: new Dictionary<string, string> { ["SKU00246"] = "Ibuprofeno Pfizer Fr 200ml" });

        var bytes = PurchasingExcelExporter.BuildSugestaoHoje(output);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Sugestão de hoje");

        ws.Cell(1, 1).GetString().Should().Be("SKU");
        ws.Cell(1, 2).GetString().Should().Be("Produto");

        // Uma linha fundida para o SKU×loja compartilhado.
        ws.LastRowUsed()!.RowNumber().Should().Be(2);
        ws.Cell(2, 1).GetString().Should().Be("SKU00246");
        ws.Cell(2, 2).GetString().Should().Be("Ibuprofeno Pfizer Fr 200ml");

        // Última coluna é o Δ (ML − clássico) = 14 − 8 = 6.
        var lastCol = ws.LastColumnUsed()!.ColumnNumber();
        ws.Cell(1, lastCol).GetString().Should().Contain("Δ");
        ws.Cell(2, lastCol).GetValue<int>().Should().Be(6);
    }

    [Fact]
    public void BuildCompleto_gera_workbook_com_duas_planilhas()
    {
        var output = OutputCom(
            politicas:
            [
                Politica(Classic, listaCompra: [Item("SKU00246", 1, posicao: 8, qtd: 8)],
                    pedidos: [new OrderRecordView(new DateOnly(2025, 12, 31), "SKU00246", 1, 8, 8, 9, 16)]),
                Politica(Forecast, listaCompra: [Item("SKU00246", 1, posicao: 8, qtd: 14)]),
            ],
            produtos: new Dictionary<string, string> { ["SKU00246"] = "Ibuprofeno Pfizer Fr 200ml" });

        var bytes = PurchasingExcelExporter.BuildCompleto(output, Classic);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        wb.Worksheets.Select(w => w.Name).Should().Contain(["Sugestão de hoje", "Livro de pedidos"]);
    }

    // ---- Fábricas de dados de teste. ----------------------------------------

    private static SimulationOutput OutputCom(
        IReadOnlyList<PolicySimulationResultView>? politicas = null,
        IReadOnlyList<OrderRecordView>? pedidos = null,
        IReadOnlyDictionary<string, string>? produtos = null)
    {
        politicas ??= [Politica(Classic, pedidos: pedidos)];

        return new SimulationOutput(
            GeradoEm: new DateTimeOffset(2025, 12, 31, 0, 3, 0, TimeSpan.Zero),
            TreinoJobId: Guid.Empty,
            Produtos: produtos,
            Resultado: new SimulationResultView(
                DataInicio: new DateOnly(2025, 11, 1),
                DataFim: new DateOnly(2025, 12, 31),
                LeadTimeDias: 7,
                CicloDias: 7,
                FatorServico: 1.65,
                SeriesAvaliadas: 1,
                Politicas: politicas));
    }

    private static PolicySimulationResultView Politica(
        string policy,
        IReadOnlyList<BuyListItemView>? listaCompra = null,
        IReadOnlyList<OrderRecordView>? pedidos = null) =>
        new(policy, Kpis(), PorDimensao: null, ListaCompraFinal: listaCompra, Pedidos: pedidos);

    private static BuyListItemView Item(string sku, int loja, decimal posicao, decimal qtd) =>
        new(sku, loja, Estoque: posicao, EmTransito: 0, Posicao: posicao, ReorderPoint: 9, OrderUpToLevel: 16, QuantidadeSugerida: qtd);

    private static PolicyKpisView Kpis() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
