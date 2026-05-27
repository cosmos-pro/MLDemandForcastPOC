namespace CosmosPro.ML.DemandForCast.OlapSchema.Tests;

public sealed class MigratorSplitStatementsTests
{
    [Fact]
    public void Single_statement_sem_terminador_retorna_um_item()
    {
        var result = Migrator.SplitStatements("CREATE TABLE foo (id Int32) ENGINE = MergeTree ORDER BY id").ToList();
        result.Should().ContainSingle().Which.Should().StartWith("CREATE TABLE");
    }

    [Fact]
    public void Multi_statement_separado_por_ponto_e_virgula_LF_quebra_em_n_itens()
    {
        var sql = "CREATE TABLE a (x Int32) ENGINE = MergeTree ORDER BY x;\n"
                + "CREATE TABLE b (y Int32) ENGINE = MergeTree ORDER BY y;\n";
        var result = Migrator.SplitStatements(sql).ToList();
        result.Should().HaveCount(2);
        result[0].Should().Contain("CREATE TABLE a");
        result[1].Should().Contain("CREATE TABLE b");
    }

    [Fact]
    public void Multi_statement_separado_por_CRLF_tambem_funciona()
    {
        var sql = "CREATE TABLE a (x Int32) ENGINE = MergeTree ORDER BY x;\r\n"
                + "CREATE TABLE b (y Int32) ENGINE = MergeTree ORDER BY y;\r\n";
        Migrator.SplitStatements(sql).Should().HaveCount(2);
    }

    [Fact]
    public void Trailing_semicolon_eh_removido_de_cada_statement()
    {
        var result = Migrator.SplitStatements("SELECT 1;").ToList();
        result.Should().ContainSingle().Which.Should().Be("SELECT 1");
    }

    [Fact]
    public void Linhas_vazias_e_whitespace_sao_ignoradas()
    {
        var sql = "SELECT 1;\n\n   \nSELECT 2;\n";
        Migrator.SplitStatements(sql).Should().Equal("SELECT 1", "SELECT 2");
    }
}
