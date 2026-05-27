using CosmosPro.ML.DemandForCast.Engine;
using CosmosPro.ML.DemandForCast.Engine.Entities;
using Microsoft.EntityFrameworkCore;

namespace CosmosPro.ML.DemandForCast.Engine.Tests;

public sealed class EngineDbContextTests
{
    private static EngineDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<EngineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Model_mapeia_CargaStage_para_tabela_CargasStage_no_plural()
    {
        using var db = NewInMemoryContext();
        var entityType = db.Model.FindEntityType(typeof(CargaStage));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("CargasStage");
    }

    [Fact]
    public void Status_eh_persistido_como_string_para_legibilidade_no_DB()
    {
        using var db = NewInMemoryContext();
        var property = db.Model
            .FindEntityType(typeof(CargaStage))!
            .FindProperty(nameof(CargaStage.Status))!;

        property.GetMaxLength().Should().Be(20);
        // O CLR type da entity é o enum, mas o provider type (o que vai pro DB)
        // deve ser string por causa do HasConversion<string>().
        property.GetProviderClrType().Should().Be(typeof(string),
            "Status enum deve ser persistido como string no DB");
    }

    [Fact]
    public void Indice_de_polling_existe_em_Status_e_DataAgendamento()
    {
        using var db = NewInMemoryContext();
        var indexes = db.Model
            .FindEntityType(typeof(CargaStage))!
            .GetIndexes();

        indexes.Should().ContainSingle(i =>
            i.GetDatabaseName() == "IX_CargasStage_Status_DataAgendamento"
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Status", "DataAgendamento" }));
    }

    [Fact]
    public async Task Salvar_e_recuperar_uma_CargaStage_funciona()
    {
        await using var db = NewInMemoryContext();

        var carga = new CargaStage
        {
            Id = Guid.NewGuid(),
            Status = CargaStageStatus.Pendente,
            DataAgendamento = DateTimeOffset.UtcNow,
            NomeArquivoOriginal = "test.zip",
            BlobKey = "test.zip",
        };

        db.CargasStage.Add(carga);
        await db.SaveChangesAsync();

        var loaded = await db.CargasStage.SingleAsync(c => c.Id == carga.Id);
        loaded.Status.Should().Be(CargaStageStatus.Pendente);
        loaded.NomeArquivoOriginal.Should().Be("test.zip");
    }
}
