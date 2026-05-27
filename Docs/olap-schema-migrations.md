# OLAP Schema Migrations

Mecanismo de migração de schema para o banco **`vendas-olap`** no ClickHouse.

> Quer só adicionar uma migration nova? Pule para [§5 Como adicionar uma migration](#5-como-adicionar-uma-migration).

---

## 1. Por que um runner customizado

Cada um dos três bancos do POC tem um mecanismo diferente de schema:

| Banco | Mecanismo | Pacote/Projeto |
|---|---|---|
| `vendas` (SQL Server) | **DACPAC** declarativo | [CosmosPro.ML.DemandForCast.Database](../CosmosPro.ML.DemandForCast.Database/) (`MSBuild.Sdk.SqlProj`) |
| `engine` (SQL Server) | **EF Core migrations** *(planejado)* | dentro do projeto Engine (quando existir) |
| `vendas-olap` (ClickHouse) | **Runner customizado one-shot** ← este doc | [CosmosPro.ML.DemandForCast.OlapSchema](../CosmosPro.ML.DemandForCast.OlapSchema/) |

ClickHouse **não tem DACPAC** nem um produto declarativo equivalente. A alternativa "scripts idempotentes no boot do ApiService" foi descartada porque `CREATE TABLE IF NOT EXISTS` cobre criação mas não evolução (`ALTER TABLE` precisa de controle de versão pra não rodar duas vezes).

A solução: um console .NET one-shot, próprio do projeto, que mantém uma tabela `__schema_migrations` dentro do próprio ClickHouse para rastrear versões aplicadas. **Mesmo padrão que `golang-migrate` ou `dbmate`, em ~120 linhas de C#**, mantendo o stack 100% .NET.

---

## 2. Arquitetura

```
AppHost (F5)
  └─ vendas-olap-schema (Project, one-shot)
       │
       ├─ AddServiceDefaults()        ← OTel + logs para o Dashboard
       ├─ AddClickHouseDataSource("vendas-olap")
       │
       └─ Migrator.RunAsync()
            ├─ EnsureMigrationsTableAsync()    ← CREATE TABLE IF NOT EXISTS __schema_migrations
            ├─ GetAppliedVersionsAsync()       ← SELECT version FROM __schema_migrations
            ├─ LoadEmbeddedScripts()           ← lê Scripts/*.sql via Assembly.GetManifestResourceStream
            └─ for each pending:
                ├─ split em statements
                ├─ ExecuteNonQueryAsync (cada um)
                └─ INSERT INTO __schema_migrations
```

**Wire-up no AppHost** ([AppHost.cs](../CosmosPro.ML.DemandForCast.AppHost/AppHost.cs)):

```csharp
var olapSchema = builder.AddProject<Projects.CosmosPro_ML_DemandForCast_OlapSchema>("vendas-olap-schema")
                        .WithReference(vendasOlapDb)
                        .WaitFor(vendasOlapDb)
                        .WithParentRelationship(vendasOlapDb.Resource);

var apiService = builder.AddProject<...>("apiservice")
    // ...
    .WaitForCompletion(olapSchema);
```

- `.WithReference` injeta `ConnectionStrings__vendas-olap` no console.
- `.WaitFor` segura o início até o ClickHouse responder ao health check.
- `.WithParentRelationship` aninha o resource sob `vendas-olap` no Dashboard.
- O `apiservice` usa `.WaitForCompletion(olapSchema)` para garantir que o schema está aplicado antes dele subir.

---

## 3. Estado registrado: tabela `__schema_migrations`

A tabela é criada pelo runner na primeira execução (idempotente):

```sql
CREATE TABLE IF NOT EXISTS __schema_migrations
(
    version    String,
    applied_at DateTime DEFAULT now()
)
ENGINE = MergeTree
ORDER BY version;
```

Queries úteis (rodando pelo DbGate ou `clickhouse-client`):

```sql
-- Versões aplicadas em ordem cronológica
SELECT version, applied_at
FROM __schema_migrations
ORDER BY applied_at;

-- Última migration aplicada
SELECT version, applied_at
FROM __schema_migrations
ORDER BY applied_at DESC
LIMIT 1;
```

---

## 4. Como o runner descobre os scripts

Os arquivos `.sql` são **embarcados no assembly** via `EmbeddedResource` no csproj:

```xml
<ItemGroup>
  <EmbeddedResource Include="Scripts\*.sql" />
</ItemGroup>
```

Em runtime, o runner usa `Assembly.GetManifestResourceNames()` filtrando pelo prefixo `CosmosPro.ML.DemandForCast.OlapSchema.Scripts.` e extensão `.sql`. O nome do arquivo sem extensão vira a **`version`** registrada em `__schema_migrations`.

> **Por que embarcar e não bind mount?** Garante que o conjunto de migrações distribuído é exatamente o que foi commitado/buildado, sem depender de o filesystem do dev ter os mesmos arquivos. Reproduzível e auto-contido.

---

## 5. Como adicionar uma migration

### Passo a passo

1. Criar arquivo em [Scripts/](../CosmosPro.ML.DemandForCast.OlapSchema/Scripts/) seguindo a convenção `NNN_descricao.sql`:

   ```
   Scripts/
     001_create_venda_diaria.sql
     002_add_promocao_columns.sql
     003_create_mv_venda_mensal.sql
   ```

2. Escrever o SQL. **Cada statement deve terminar com `;` no fim da linha**:

   ```sql
   -- 001_create_venda_diaria.sql
   CREATE TABLE IF NOT EXISTS venda_diaria
   (
       data         Date,
       sku          String,
       loja         String,
       quantidade   UInt32,
       receita      Decimal(18, 4)
   )
   ENGINE = MergeTree
   PARTITION BY toYYYYMM(data)
   ORDER BY (loja, sku, data);
   ```

3. Rebuild + F5 (ou `aspire run`). O runner detecta, aplica e registra. Próximas execuções pulam (idempotente).

4. Validar via DbGate ou query:

   ```sql
   SELECT * FROM __schema_migrations WHERE version = '001_create_venda_diaria';
   ```

### Convenções

| Aspecto | Convenção |
|---|---|
| Nome do arquivo | `NNN_descricao.sql` — `NNN` é prefixo numérico sortable |
| Ordem de aplicação | Lexicográfica (`StringComparer.Ordinal`) — por isso `NNN` precisa ser zero-padded |
| `version` registrada | Nome do arquivo sem extensão (ex.: `001_create_venda_diaria`) |
| Statements por arquivo | Múltiplos OK, separados por `;` no fim de linha |
| Encoding | UTF-8 (sem BOM preferencial) |

---

## 6. Limitações conhecidas

| Limitação | Impacto | Mitigação |
|---|---|---|
| **Forward-only** (sem `down`/rollback) | Não dá pra desfazer uma migration aplicada via runner. | Reverter exige escrever uma migration nova (ex.: `010_drop_velha_tabela.sql`). |
| **Sem checksum** | Editar um `.sql` já aplicado **não** dispara reaplicação. | Política: arquivos aplicados são imutáveis. Mudança = nova migration. |
| **SQL split naive** | Statement com `;` no fim de uma linha dentro de string literal (`'foo;\n'` raro mas possível) quebra o split. | Para POC, evitar; se virar problema, trocar `SplitStatements()` por parser real ou um statement por arquivo. |
| **Sem locking distribuído** | Dois processos rodando o runner em paralelo podem aplicar a mesma migration duas vezes. | Não acontece no fluxo Aspire (one-shot). Se virar concern, adicionar lock advisory. |
| **Sem dry-run** | Aplica direto. | Para inspecionar antes, rodar o `.sql` manualmente no DbGate primeiro. |

---

## 7. Troubleshooting

**O resource `vendas-olap-schema` fica em `Waiting` para sempre.**
Verifique se `vendas-olap` está `Running + Healthy`. O `.WaitFor(vendasOlapDb)` segura o runner até o ClickHouse responder ao health check `/ping`. Se o ClickHouse caiu (ex.: Docker reiniciado), o runner não dispara.

**Migration roda mas não fica registrada.**
Cheque os console logs do resource via Aspire Dashboard ou MCP (`mcp__aspire__list_console_logs vendas-olap-schema`). Erros de SQL parecem no log com o stack trace do `ClickHouseException`. A migration **só é registrada na `__schema_migrations` após todos os statements do arquivo passarem** — falha parcial deixa estado inconsistente que pode exigir limpeza manual.

**Editei um `.sql` e não foi reaplicado.**
Esperado (sem checksum). Crie uma nova migration com a mudança ou, se for cedo no ciclo, delete manualmente da `__schema_migrations` e rode de novo:

```sql
DELETE FROM __schema_migrations WHERE version = '001_create_venda_diaria';
```

(Em ClickHouse, `DELETE` em MergeTree é assíncrono — para garantir, esperar uns segundos ou usar `ALTER TABLE __schema_migrations DELETE WHERE ...` + `SYSTEM SYNC REPLICA` se aplicável.)

**Quero resetar tudo.**
Drope a tabela e o volume:
```powershell
# Pelo DbGate:
DROP TABLE __schema_migrations;
# Para zerar dados do banco também: docker volume rm cosmospro.ml.demandforcast.apphost-6ed4355b50-clickhouse-data
```
Próximo `aspire run` recria do zero.

---

## 8. Arquivos relevantes

| Arquivo | Papel |
|---|---|
| [CosmosPro.ML.DemandForCast.OlapSchema/Program.cs](../CosmosPro.ML.DemandForCast.OlapSchema/Program.cs) | Entry-point. Configura Host + ClickHouse data source + chama Migrator. |
| [CosmosPro.ML.DemandForCast.OlapSchema/Migrator.cs](../CosmosPro.ML.DemandForCast.OlapSchema/Migrator.cs) | Lógica do runner. `RunAsync`, `EnsureMigrationsTableAsync`, `LoadEmbeddedScripts`, `SplitStatements`. |
| [CosmosPro.ML.DemandForCast.OlapSchema/Scripts/](../CosmosPro.ML.DemandForCast.OlapSchema/Scripts/) | Pasta com as migrações `.sql`. |
| [CosmosPro.ML.DemandForCast.OlapSchema/CosmosPro.ML.DemandForCast.OlapSchema.csproj](../CosmosPro.ML.DemandForCast.OlapSchema/CosmosPro.ML.DemandForCast.OlapSchema.csproj) | csproj. Note o `<EmbeddedResource Include="Scripts\*.sql" />`. |
| [CosmosPro.ML.DemandForCast.AppHost/AppHost.cs](../CosmosPro.ML.DemandForCast.AppHost/AppHost.cs) | Wire-up do resource no Aspire (busca por `olapSchema`). |

---

## 9. Quando migrar para uma ferramenta externa

Este runner foi escolhido para POC priorizando: ① stack único (.NET puro), ② simetria com o `vendas-schema` (one-shot Aspire), ③ zero dependência externa.

Considerar substituir por **`dbmate`**, **`golang-migrate`** ou similar quando:

- A equipe crescer e mais gente precisar editar migrations (ferramenta externa tem CLI mais ergonômica).
- Surgir necessidade real de rollback/down migrations.
- Pipeline CI/CD precisar aplicar migrações sem subir o AppHost inteiro.

A substituição é localizada: troca o conteúdo do projeto `OlapSchema` (Migrator + Program) por uma invocação da ferramenta externa, ponto de plug no AppHost continua o mesmo.
