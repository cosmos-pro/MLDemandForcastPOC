using CosmosPro.ML.DemandForCast.Web;

namespace CosmosPro.ML.DemandForCast.Web.Tests;

public sealed class FormatHelpersTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1,0 KB")]
    [InlineData(1024 * 100, "100,0 KB")]
    [InlineData(1024 * 1024, "1,0 MB")]
    [InlineData(1024L * 1024 * 1024, "1,00 GB")]
    public void FormatBytes_escala_corretamente_pelos_4_intervalos(long bytes, string expectedBrPt)
    {
        // O formato usa cultura corrente; em pt-BR o separador é vírgula.
        // Para evitar flakiness de cultura no CI, comparamos sem fixar o caractere
        // de separador decimal.
        var result = FormatHelpers.FormatBytes(bytes);
        var normalized = result.Replace(",", ".").Replace(" ", "");
        var expectedNormalized = expectedBrPt.Replace(",", ".").Replace(" ", "");
        normalized.Should().Be(expectedNormalized);
    }
}
