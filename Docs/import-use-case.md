# Use Case — Importação de Dados para Stage

Fluxo end-to-end para o usuário trazer dados (vendas, estoque, compras, mestres, IQVIA) para dentro do sistema. Os dados pousam no banco `Stage` (ver [schema.md §1](schema.md)); o registro de cada job de import vive em `engine.CargasStage` (ver [schema.md §2](schema.md)).

> **Status atual:** todos os 4 passos do fluxo implementados e validados end-to-end (ZIP com 18 linhas → tabelas Stage com 18 linhas, `LinhasImportadas=18`, `Status=Concluida`).

---

## Visão geral do fluxo

```
┌────────────────────────────────────────────────────────────────────┐
│  1. UI Blazor (/importar) → InputFile (.zip) → POST multipart      │   ← OK
│                                                                    │
│  2. apiservice — POST /api/imports/upload                          │   ← OK
│     - Valida estrutura: arquivos esperados + headers das CSVs      │
│     - Upload ZIP para MinIO bucket "imports/{id}.zip"              │
│     - INSERT engine.CargasStage (Status=Pendente, BlobKey)         │
│     - 202 Accepted { id, status, dataAgendamento }                 │
│                                                                    │
│  3. UI Blazor (/jobs) — tabela com status, polling 3s              │   ← OK
│     - GET /api/imports?take=50                                     │
│                                                                    │
│  4. Worker (BackgroundService)                                     │   ← OK
│     - Polling SQL Server com WITH (UPDLOCK, READPAST)              │
│     - Download ZIP do MinIO                                        │
│     - BEGIN TRANSACTION no Stage DB                                │
│     - DELETE em ordem reversa de FK                                │
│     - BULK INSERT em ordem de FK (DataTable tipada)                │
│     - COMMIT                                                       │
│     - UPDATE Status=Concluida + LinhasImportadas | Falha + erro    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 1. Contrato esperado do ZIP

O ZIP deve conter **exatamente 7 arquivos** (case-insensitive nos nomes; case-insensitive nas colunas; separador `,` ou `;`):

| Arquivo | Colunas obrigatórias (mínimas — outras são ignoradas) |
|---|---|
| `lojas.csv` | `LojaId, Nome, UF, Cidade` |
| `produtos.csv` | `Sku, Nome` |
| `vendas.csv` | `Data, LojaId, Sku, Quantidade, PrecoUnitario, ValorTotal` |
| `estoques_diarios.csv` | `Data, LojaId, Sku, QuantidadeEmEstoque` |
| `compras.csv` | `DataPedido, LojaId, Sku, Quantidade` |
| `promocoes.csv` | `DataInicio, DataFim, Sku` |
| `mercado_iqvia.csv` | `Mes, PrincipioAtivo, UF, DemandaMercadoUnidades` |

> Fonte da verdade: [`ImportSchemas.cs`](../CosmosPro.ML.DemandForCast.ApiService/Imports/ImportSchemas.cs). Atualizar este doc no mesmo commit que alterar o schema.

### Convenções

- **Encoding:** UTF-8 (com ou sem BOM).
- **Separador:** vírgula `,` ou ponto-e-vírgula `;` (Excel brasileiro exporta com `;`).
- **Headers:** primeira linha do CSV. Aspas opcionais (removidas no validador).
- **Validação na API é estrutural** (presença de arquivos + colunas). Validação semântica (FKs, datas plausíveis, integridade cronológica) fica para o Worker em transação.

---

## 2. API endpoint

### `POST /api/imports/upload`

Upload de um ZIP com os 7 CSVs.

**Request:**
- `Content-Type: multipart/form-data`
- Campo `file`: o arquivo ZIP.

**Limite de tamanho:** 500 MB (configurado em Kestrel + FormOptions no `Program.cs`).

**Respostas:**

- `202 Accepted` — ZIP aceito e enfileirado:
  ```json
  {
    "id": "019e45eb-baaf-7342-9a54-4250647e805a",
    "status": "Pendente",
    "dataAgendamento": "2026-05-20T15:05:37.1992441+00:00"
  }
  ```
  Header `Location: /api/imports/{id}`.

- `400 Bad Request` — validação falhou:
  ```json
  {
    "errors": [
      "'vendas.csv' está sem as colunas: PrecoUnitario, ValorTotal.",
      "Arquivo obrigatório ausente no ZIP: 'mercado_iqvia.csv'."
    ]
  }
  ```

**O que acontece internamente, em ordem:**

1. Validação de presença/tamanho do arquivo + extensão `.zip`.
2. **Validação estrutural** (sem I/O de DB ou MinIO) — abre o ZIP em memória, verifica nomes dos arquivos e headers. Falhas aqui retornam 400 imediatamente.
3. Geração de `Id` (GUID v7, time-ordered).
4. `EnsureBucketAsync("imports")` no MinIO (idempotente).
5. Upload do stream para `imports/{id}.zip`.
6. `INSERT engine.CargasStage` com `Status=Pendente`, `BlobKey={id}.zip`.
7. Retorna 202.

**Observação:** se o INSERT falhar após o upload, o objeto fica órfão no MinIO. Aceitável no POC; um GC periódico ou limpeza manual via console MinIO resolve.

### `GET /api/imports/{id}`

Retorna o estado atual da carga.

**Respostas:**

- `200 OK`:
  ```json
  {
    "id": "...",
    "status": "Pendente | Processando | Concluida | Falha",
    "dataAgendamento": "...",
    "dataInicioProcessamento": null,
    "dataConclusao": null,
    "nomeArquivoOriginal": "import-2026-05-20.zip",
    "blobKey": "...zip",
    "mensagemErro": null,
    "linhasImportadas": null
  }
  ```

- `404 Not Found` — id não existe.

---

## 3. Arquitetura técnica

```
┌─ apiservice ───────────────────────────────────────────────────────┐
│                                                                    │
│  Imports/                                                          │
│    ├─ ImportSchemas.cs    — dicionário arquivo → colunas esperadas │
│    ├─ ImportValidator.cs  — abre ZIP + valida headers              │
│    └─ ImportsEndpoints.cs — MapImportsEndpoints (POST + GET)       │
│                                                                    │
│  Dependências DI injetadas no endpoint:                            │
│    - EngineDbContext     (Aspire.Microsoft.EntityFrameworkCore.SqlServer)
│    - IMinioClient        (CommunityToolkit.Aspire.Minio.Client)    │
│    - ILogger<Program>                                              │
└────────────────────────────────────────────────────────────────────┘
```

### Bucket MinIO

- **Nome:** `imports`
- **Criação:** lazy, na primeira chamada do endpoint (`EnsureBucketAsync` é idempotente).
- **Objetos:** `{CargaStageId}.zip` — chave determinística baseada no Id da carga.
- **Persistência:** garantida pelo `WithDataVolume()` do container MinIO no AppHost (ZIPs sobrevivem entre F5s).

### Tabela `engine.CargasStage`

Detalhada em [schema.md §2](schema.md). Resumo dos campos preenchidos pela API:

| Campo | Valor no INSERT |
|---|---|
| `Id` | `Guid.CreateVersion7()` (time-ordered) |
| `Status` | `Pendente` |
| `DataAgendamento` | `DateTimeOffset.UtcNow` |
| `NomeArquivoOriginal` | nome enviado pelo usuário |
| `BlobKey` | `{Id}.zip` |
| `UsuarioId` | `"anonymous"` (placeholder p/ futura auth) |

Demais campos (`DataInicioProcessamento`, `DataConclusao`, `MensagemErro`, `LinhasImportadas`) ficam `NULL` até o Worker processar.

---

## 4. UI Blazor (implementada)

[Web/Components/Pages/Imports.razor](../CosmosPro.ML.DemandForCast.Web/Components/Pages/Imports.razor) — página `/importar`:
- `<InputFile accept=".zip" />` para seleção.
- Botão "Enviar" → chama `ImportsApiClient.UploadAsync`.
- Em sucesso, mostra alert verde com Id da carga + link para `/jobs`.
- Em falha estrutural (400), lista cada erro do payload `errors[]`.

[Web/Components/Pages/Jobs.razor](../CosmosPro.ML.DemandForCast.Web/Components/Pages/Jobs.razor) — página `/jobs`:
- Tabela das 50 cargas mais recentes ordenadas por `DataAgendamento desc`.
- Polling automático a cada 3s via `Timer` no `OnInitializedAsync`.
- Badge colorido por Status: cinza (Pendente), info (Processando), verde (Concluida), vermelho (Falha).
- `Dispose` cancela o timer e o CTS — sem leak de polling após sair da página.

**Cliente HTTP:** [Web/ImportsApiClient.cs](../CosmosPro.ML.DemandForCast.Web/ImportsApiClient.cs) — registrado em `Program.cs` via `AddHttpClient<ImportsApiClient>` com `BaseAddress = "https+http://apiservice"` (resolução de service discovery do Aspire). Métodos: `UploadAsync`, `ListAsync`, `GetAsync`.

## 5. Worker (implementado)

[CosmosPro.ML.DemandForCast.Worker](../CosmosPro.ML.DemandForCast.Worker/) — projeto BackgroundService.

### Loop de polling ([ImportWorker.cs](../CosmosPro.ML.DemandForCast.Worker/ImportWorker.cs))

- Intervalo: 5 segundos quando a fila está vazia. Quando processou, tenta a próxima imediatamente.
- **Claim atômico em uma round-trip:**
  ```sql
  ;WITH cte AS (
      SELECT TOP (1) *
      FROM dbo.CargasStage WITH (UPDLOCK, READPAST)
      WHERE Status = 'Pendente'
      ORDER BY DataAgendamento
  )
  UPDATE cte
      SET Status = 'Processando',
          DataInicioProcessamento = SYSDATETIMEOFFSET()
      OUTPUT INSERTED.Id, INSERTED.BlobKey, INSERTED.NomeArquivoOriginal;
  ```
  `UPDLOCK` segura linha exclusiva; `READPAST` faz workers concorrentes pularem linhas já locked. Resultado: padrão "competing consumers" em SQL Server puro.
- Em sucesso: `UPDATE Status='Concluida', LinhasImportadas=N` via EF `ExecuteUpdateAsync`.
- Em falha: `UPDATE Status='Falha', MensagemErro=...` (truncado a 2000 chars).

### Processamento ([CargaProcessor.cs](../CosmosPro.ML.DemandForCast.Worker/CargaProcessor.cs))

1. Baixa o ZIP do MinIO para `%TEMP%/carga-{Id}/` via `IMinioClient.GetObjectAsync` com callback stream.
2. `ZipFile.ExtractToDirectory` → 7 CSVs.
3. `BeginTransactionAsync` no `SqlConnection` do Stage DB.
4. **DELETE em ordem reversa de FK** (Vendas, EstoquesDiarios, Compras, Promocoes, MercadoIqvia, Produtos, Lojas).
5. **BULK INSERT em ordem de FK** (Lojas, Produtos, Vendas, EstoquesDiarios, Compras, Promocoes, MercadoIqvia):
   - Lê CSV com CsvHelper (`IgnoreBlankLines=true`, separador auto-detectado entre `,` e `;`).
   - Materializa em `DataTable` **com tipos explícitos** definidos em [TableSchemas.cs](../CosmosPro.ML.DemandForCast.Worker/TableSchemas.cs) — necessário porque o `SqlBulkCopy` não converte strings em `bit`/`int`/`decimal`/`DateTime` automaticamente.
   - `SqlBulkCopy.WriteToServerAsync(dataTable)` com `BatchSize=10000`, `BulkCopyTimeout=600s`.
6. `CommitAsync` se tudo OK, `RollbackAsync` em caso de exceção (propaga para o `ImportWorker` registrar `Falha`).
7. Cleanup do diretório temporário no `finally`.

### Decisões de design

- **DELETE em vez de TRUNCATE:** TRUNCATE não funciona com FKs apontadas. DELETE é mais lento mas correto. Para POC com até alguns milhões de linhas, aceitável.
- **DataTable tipada vs IDataReader do CsvHelper:** CsvHelper retorna tudo como string. Para `bit`/`int`/`decimal`/`DateTime`, isso quebra no SqlBulkCopy. Materializar em DataTable tipada custa memória (~250MB para 5M linhas), aceitável neste estágio.
- **`bit` aceita `0`/`1` E `true`/`false`** — ver [TableSchemas.Parse](../CosmosPro.ML.DemandForCast.Worker/TableSchemas.cs).
- **Idempotente por substituição:** cada carga limpa o Stage e refaz. Não há merge incremental.
- **Sem retentativa automática:** carga Falha permanece Falha. Usuário re-uploada se quiser.

## 6. Próximos passos

| Passo | O que falta | Onde implementar |
|---|---|---|
| Validação semântica | FKs entre tabelas, datas plausíveis, integridade cronológica | Worker, dentro da transação antes do COMMIT |
| Limpeza de ZIPs órfãos | Remover do MinIO ZIPs cuja CargaStage falhou após upload | Background timer ou comando admin |
| Atualização de UI em tempo real | Usar SignalR em vez de polling 3s no `/jobs` | Web project |

---

## Testar manualmente (curl)

```bash
# Happy path
curl -k -F "file=@import.zip;type=application/zip" \
  https://localhost:7425/api/imports/upload

# Consultar status
curl -k https://localhost:7425/api/imports/{id}
```

> Substitua o port `7425` pelo valor real do apiservice no Aspire Dashboard.
