# Exportar dados da Sugestão de compra para Excel (.xlsx)

**Data:** 2026-07-22
**Status:** Implementado (TDD — exporter com testes xUnit; UI com botões + JS interop)

## Problema

A tela `/sugestao-compra` ([SugestaoCompra.razor](../../../CosmosPro.ML.DemandForCast.Web/Components/Pages/SugestaoCompra.razor))
mostra duas listas úteis para o usuário de varejo, mas não há como levá-las para fora da UI.
O usuário precisa exportar esses dados para **Excel (.xlsx)** para trabalhar/auditar offline.

Escopo de exportação (confirmado com o usuário):
- **Sugestão de hoje** — lista de compra sugerida do dia (`BuyRow`, fusão de políticas).
- **Livro de pedidos** — histórico de pedidos lançados na janela (`OrderRecordView`, por política).

Fora de escopo: o resumo por categoria (Prescrição/OTC/Controlado).

## Contexto técnico

- Frontend: **Blazor Server** (.NET 10 Aspire), `@rendermode InteractiveServer`, biblioteca **Radzen.Blazor**.
- Os dados das duas abas já ficam **materializados na memória do componente** após
  `PurchasingApiClient.ParseResultado(_selected.ResultadoJson)`:
  - Sugestão de hoje: construída por `BuildBuyList(...)` no próprio `@code` → `List<BuyRow>`.
  - Livro de pedidos: `politica.Pedidos` → `IReadOnlyList<OrderRecordView>`, com a política escolhida no dropdown.
- **Não existe** hoje nenhuma exportação (CSV/Excel) nem biblioteca de planilha no repositório.
  `CsvHelper` existe, mas só no projeto Worker.
- Padrão de JS interop já usado para clique em elemento: `window.clickElementById` em
  `CosmosPro.ML.DemandForCast.Web/wwwroot/app.js`, chamado via `IJSRuntime` em `Imports.razor`.

## Decisão de abordagem

**Gerar o .xlsx no próprio componente Blazor (projeto Web), usando as listas já em memória,
e entregar o arquivo ao navegador via JS interop.** Sem novo endpoint de API.

Motivos:
- O usuário pediu Excel de verdade (.xlsx), não CSV. Célula tipada resolve locale pt-BR
  (vírgula decimal, datas) sem gambiarra de separador.
- A lógica de fusão da "Sugestão de hoje" (`BuildBuyList`) vive no razor; gerar no componente
  evita duplicá-la num endpoint server-side.
- É um POC — menor superfície é melhor.

Alternativas descartadas:
- Endpoint na API que relê `ResultadoJson` e gera o xlsx → duplicaria `BuildBuyList`.
- CSV via data-URL → não é .xlsx; sofre com locale pt-BR no Excel.

## Componentes

### 1. Pacote
- Adicionar `ClosedXML` ao `CosmosPro.ML.DemandForCast.Web.csproj`.

### 2. Exportador (`PurchasingExcelExporter`)
Classe no projeto Web. Não conhece Blazor nem HTTP — recebe dados já materializados e devolve `byte[]`.

Responsabilidades:
- `byte[] BuildSugestaoHoje(IReadOnlyList<BuyRow> rows, IReadOnlyList<string> policies, string geradoEm)`
  - Colunas espelhando a grade atual: SKU, Produto, Loja, e por política `Pos.`/`Pedir`, mais a coluna Δ (ML − clássico).
- `byte[] BuildLivroPedidos(IReadOnlyList<OrderRecordView> pedidos, IReadOnlyDictionary<string,string> produtos, string policy)`
  - Colunas: Data, SKU, Produto, Loja, Posição, Ponto pedido (s), Alvo (S), Qtd pedida.
- `byte[] BuildWorkbook(...)` — workbook único com **duas planilhas** (Sugestão de hoje + Livro de pedidos),
  reaproveitando a montagem das duas anteriores num só `XLWorkbook`.

Tipagem de células:
- Datas como `DateOnly`/`DateTime` (formato de data do Excel), quantidades/posições/pontos como número.
  Nada de string em campo numérico — é o que garante o comportamento correto em Excel pt-BR.

### 3. Download via JS interop
- Adicionar em `app.js`:
  ```js
  window.downloadFile = (fileName, base64, contentType) => {
    const a = document.createElement('a');
    a.href = `data:${contentType};base64,${base64}`;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  };
  ```
- No componente: `await JS.InvokeVoidAsync("downloadFile", fileName, Convert.ToBase64String(bytes),
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")`.

### 4. UI (botões em `SugestaoCompra.razor`)
- Botão **"Exportar"** em cada aba (Sugestão de hoje / Livro de pedidos) — baixa só a aba visível;
  o Livro de pedidos respeita a **política selecionada** no dropdown.
- Botão **"Exportar tudo"** — gera um único .xlsx com as duas planilhas.
- Nome do arquivo com a data da simulação:
  - `sugestao-compra_YYYY-MM-DD.xlsx`
  - `livro-de-pedidos_YYYY-MM-DD.xlsx`
  - `sugestao-compra-completo_YYYY-MM-DD.xlsx`
- Botões só habilitados quando há simulação selecionada e parseada com sucesso (mesma condição do
  bloco de detalhe já existente).

## Fluxo de dados

1. Usuário seleciona uma simulação concluída → `_selected.ResultadoJson` é parseado em `SimulationOutput`.
2. Usuário clica num botão de exportar.
3. Componente monta as listas (reusa `BuildBuyList` / `politica.Pedidos`) e chama `PurchasingExcelExporter`.
4. Exportador devolve `byte[]` do .xlsx.
5. Componente converte para base64 e chama `downloadFile` via `IJSRuntime`.
6. Navegador baixa o arquivo.

## Tratamento de erros

- Botões desabilitados quando não há dados → evita export vazio.
- Se a geração lançar exceção, mostrar `NotificationService` (já injetado na página) com mensagem de erro;
  não deixar exceção estourar silenciosamente.
- Listas vazias (ex.: política sem pedidos) geram planilha só com cabeçalho — comportamento aceitável.

## Testes

- Teste unitário de `PurchasingExcelExporter` (xUnit + FluentAssertions, projeto de testes existente):
  - Dado listas de amostra, o `byte[]` resultante abre como `XLWorkbook` válido.
  - Planilhas esperadas existem, com cabeçalhos e contagem de linhas corretos.
  - Célula de data é do tipo data; célula de quantidade é numérica (garantia anti-locale).
- Sem teste de UI/JS interop (custo alto para POC); validação manual do download.

## Impacto / arquivos tocados

- `CosmosPro.ML.DemandForCast.Web/CosmosPro.ML.DemandForCast.Web.csproj` — pacote ClosedXML.
- `CosmosPro.ML.DemandForCast.Web/**/PurchasingExcelExporter.cs` — novo.
- `CosmosPro.ML.DemandForCast.Web/wwwroot/app.js` — função `downloadFile`.
- `CosmosPro.ML.DemandForCast.Web/Components/Pages/SugestaoCompra.razor` — botões + handlers.
- Projeto de testes — testes do exportador.
