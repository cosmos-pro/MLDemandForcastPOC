using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

// Auto-wira um recurso ClickHouse no DbGate, injetando as env vars que o
// container do DbGate espera (LABEL_/SERVER_/USER_/PASSWORD_/PORT_/ENGINE_ +
// CONNECTIONS).
//
// Substituto local até `Aspire.Hosting.ClickHouse` (publicado pela ClickHouse
// Inc.) ganhar suporte nativo. Mirra fielmente o padrão usado por
// `CommunityToolkit.Aspire.Hosting.SqlServer.Extensions.WithDbGate`. Remover
// quando upstream cobrir esse caso (ou após PR aceito em ClickHouse.Aspire).
internal static class ClickHouseDbGateExtensions
{
    public static IResourceBuilder<ClickHouseServerResource> WithDbGate(
        this IResourceBuilder<ClickHouseServerResource> builder,
        Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= "dbgate";

        var dbGateBuilder = DbGateBuilderExtensions.AddDbGate(builder.ApplicationBuilder, containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder))
            .WaitFor(builder);

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(
        EnvironmentCallbackContext context,
        IResourceBuilder<ClickHouseServerResource> builder)
    {
        var resource = builder.Resource;
        // Espelha DbGateBuilderExtensions.SanitizeConnectionId (público no main
        // do CommunityToolkit, ainda ausente em 13.1.1). Atualizar quando o
        // método estiver disponível no pacote publicado.
        var connectionId = resource.Name.Replace('-', '_');
        var label = $"LABEL_{connectionId}";

        // Idempotência: múltiplas chamadas WithDbGate sobre o mesmo recurso
        // são absorvidas — espelha o comportamento da SqlServer.Extensions.
        if (context.EnvironmentVariables.ContainsKey(label))
        {
            return;
        }

        context.EnvironmentVariables.Add(label, resource.Name);

        // O plugin dbgate-plugin-clickhouse ignora SERVER_/PORT_ — ele só usa
        // `databaseUrl` (`URL_{id}`). Sem URL_, cai no default localhost:8123 do
        // @clickhouse/client e o dev vê ECONNREFUSED. Por isso passamos a URL
        // completa apontando para o hostname do container ClickHouse na rede
        // do Aspire.
        var port = resource.PrimaryEndpoint.TargetPort!.Value;
        context.EnvironmentVariables.Add(
            $"URL_{connectionId}",
            $"http://{resource.Name}:{port}");

        if (resource.UserNameParameter is not null)
        {
            context.EnvironmentVariables.Add($"USER_{connectionId}", resource.UserNameParameter);
        }
        else
        {
            context.EnvironmentVariables.Add($"USER_{connectionId}", "default");
        }

        if (resource.PasswordParameter is not null)
        {
            context.EnvironmentVariables.Add($"PASSWORD_{connectionId}", resource.PasswordParameter);
        }

        context.EnvironmentVariables.Add($"ENGINE_{connectionId}", "clickhouse@dbgate-plugin-clickhouse");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{connectionId}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = connectionId;
        }
    }
}
