using CosmosPro.ML.DemandForCast.Engine.Entities;
using Microsoft.EntityFrameworkCore;

namespace CosmosPro.ML.DemandForCast.Engine;

public sealed class EngineDbContext(DbContextOptions<EngineDbContext> options) : DbContext(options)
{
    public DbSet<CargaStage> CargasStage => Set<CargaStage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CargaStage>(b =>
        {
            b.ToTable("CargasStage");
            b.HasKey(x => x.Id);

            b.Property(x => x.Status)
             .HasConversion<string>()
             .HasMaxLength(20)
             .IsRequired();

            b.Property(x => x.DataAgendamento).IsRequired();
            b.Property(x => x.NomeArquivoOriginal).IsRequired().HasMaxLength(260);
            b.Property(x => x.BlobKey).IsRequired().HasMaxLength(260);
            b.Property(x => x.MensagemErro).HasMaxLength(2000);
            b.Property(x => x.UsuarioId).HasMaxLength(100);

            // Padrão de acesso típico do Worker: pegar próxima Pendente em ordem
            // cronológica de upload, usando WITH (UPDLOCK, READPAST) para
            // serialização (competing consumers em SQL Server puro).
            b.HasIndex(x => new { x.Status, x.DataAgendamento })
             .HasDatabaseName("IX_CargasStage_Status_DataAgendamento");
        });
    }
}
