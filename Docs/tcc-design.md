# TCC — Visão, Análise e Design

> **Status:** documento de visão / decisões de projeto. **Não é** estado de implementação atual — para isso, ver §6 do [README.md](../README.md) (roadmap).

Análise crítica do escopo, escolhas de algoritmo, dados necessários e arquitetura para o TCC de **comparação entre algoritmos tradicionais de sugestão de compra (eMax, eSeg) e um sistema empoderado por ML + dados de mercado (IQVIA)** em varejo farmacêutico.

---

## Sumário

1. [Sanity check da visão original](#1-sanity-check-da-visão-original)
2. [Algoritmos propostos (3 níveis)](#2-algoritmos-propostos-3-níveis)
3. [Dados necessários](#3-dados-necessários)
4. [Arquitetura proposta](#4-arquitetura-proposta)
5. [Próximos passos](#5-próximos-passos)
6. [Riscos e perguntas abertas](#6-riscos-e-perguntas-abertas)

---

## 1. Sanity check da visão original

A visão inicial está **majoritariamente correta**, com 3 pontos a calibrar:

### O que está certo

- Comparar baseline (eMax/eSeg) vs ML é exatamente a pergunta acadêmica certa, e é o tipo de comparação que rende um TCC defensável.
- UI Blazor SSR para importação + UI para sugestão + Worker para treino é a arquitetura padrão para esse tipo de prova de conceito.
- A noção de "dados de mercado (IQVIA)" como diferencial é correta — é genuinamente o que separa farma de outras verticais.

### O que vale calibrar

- **"Modelo ML treinado" não é um único binário do tipo "antes/depois".** O fluxo correto é treinar **um único modelo global** (não um por SKU) que aprende padrões de todos os SKUs juntos, usando SKU/categoria/loja como features. Tentar treinar um modelo por SKU em farma (dezenas de milhares) é inviável e é um anti-pattern documentado no [CLAUDE.md §6](../CLAUDE.md).
- **IQVIA entra como feature, não como base de treino paralela.** Não se treina "um modelo com IQVIA e outro sem"; treina-se o ML com IQVIA como **covariável exógena** e mede-se quanto ela contribui (feature importance). Isso é mais elegante academicamente.
- **"Importar e treinar" não é um único botão.** Treinar consome features que vêm de **junções entre vendas, estoque, promoção, master de produto e calendário** — não é "leio o Excel de vendas e treino". A pipeline tem etapas (validação → feature engineering → split treino/teste → backtest → registro do modelo). A UI guia por esse fluxo.

---

## 2. Algoritmos propostos (3 níveis)

A ideia é que o TCC compare os 3 níveis na mesma janela de backtest:

| Nível | Algoritmo | Implementação | Justificativa acadêmica |
|---|---|---|---|
| **N1 — Baseline (sem ML)** | **eMax** (estoque-alvo = N dias de média móvel) | C# puro, ~50 linhas | "Estado da arte do mercado tradicional" — controle. |
| **N1 — Baseline (sem ML)** | **eSeg** (média + safety stock por σ × √LT × z) | C# puro, ~80 linhas | Variante mais sofisticada; ainda regra-based. |
| **N2 — ML clássico** | **LightGBM regressão** sobre features engenheiradas | ML.NET (`Microsoft.ML.LightGbm`) | **Mesma família de algoritmo que venceu o M5 Forecasting Competition (Walmart, Kaggle 2020).** Maduro, explicável, rápido. |
| **N3 — Opcional** | Croston/TSB para itens intermitentes | C# manual (~30 linhas) | Apenas se a cauda de baixo giro for relevante na análise final. Pode ficar como "trabalho futuro" do TCC. |

**Recomendação:** entregar **N1 + N2 inteiros**, mencionar N3 como "extensão futura". Honesto academicamente (reconhece a limitação para intermitentes) e cabe no escopo de um TCC.

### Por que não Prophet, N-BEATS, TFT, etc

- Prophet é univariado (igual SSA — não dá pra defender isso em banca de farma).
- N-BEATS/TFT são neurais — vão **funcionar pior** que LightGBM com dados reais de farma (volumes médios, poucas features), e exigem tuning que vira distração do mérito da tese. M5 confirmou isso: GBM sobre features venceu modelos neurais profundos.

---

## 3. Dados necessários

### Mandatórios

| Arquivo | Campos mínimos | Granularidade | Volume típico |
|---|---|---|---|
| **vendas.csv** | `data, sku, loja, qtd_vendida, preco_unit, valor_total` | Diário, por SKU × loja | 1-3 anos = milhões de linhas |
| **estoque.csv** | `data, sku, loja, qtd_em_estoque` | Diário (snapshot fim-do-dia) | **Crítico** — usado para identificar dias de **ruptura** (não tratar venda=0 como demanda=0) |
| **compras.csv** | `data_pedido, data_recebimento, sku, loja, qtd_comprada, fornecedor` | Por pedido | Permite calcular **lead time real** por fornecedor × SKU |
| **mestre_produtos.csv** | `sku, nome, categoria, subcategoria, fabricante, principio_ativo, apresentacao, ean, registro_anvisa, lista_controle, classe_terapeutica` | Snapshot atual | 10k–50k SKUs típico |
| **mestre_lojas.csv** | `loja, nome, uf, cidade, regiao, perfil, dias_operacao_semana` | Snapshot atual | dezenas a centenas |

### Altamente recomendados

| Arquivo | Campos | Por quê |
|---|---|---|
| **promocao.csv** | `data_inicio, data_fim, sku, loja_ou_todas, tipo, desconto_pct` | Feature mais informativa depois de lags. Sem isso, ML confunde venda promocional com venda regular. |
| **iqvia.csv** | `mes, principio_ativo (ou molecula), uf, demanda_mercado_unidades, market_share_categoria` | Sinal exógeno de mercado — destrava cold-start (itens novos) e captura tendências antes da loja sentir. |
| **calendario_farma.csv** | `data, feriado_nacional, feriado_estadual_uf, campanha_vacinacao, semana_epidemiologica, evento_comercial` | Pode ser gerado proceduralmente em C# (não precisa import). |

### Geradas pelo sistema (não importadas)

- **Features de lag/rolling** (lags 1, 7, 14, 28 + médias móveis) — derivadas das vendas
- **Ruptura flag** — derivada do estoque (`qtd_em_estoque < threshold OR == 0`)
- **Preço relativo à categoria** — derivado do preço vs média da categoria
- **Hierarquia / target encoding** — derivado do master

### Para backtest

**Mínimo de 18 meses contínuos de vendas + estoque.** Idealmente 24-36 meses. Sem isso, o walk-forward (3 folds de validação ~3 meses cada) não tem dados suficientes para conclusões estatisticamente defensáveis.

---

## 4. Arquitetura proposta

Sobre o que já existe no AppHost (F1 concluído — SQL Server + ClickHouse + DbGate + DACPAC + OlapSchema, ver [README.md §3](../README.md)), falta adicionar:

```
┌────────────────────────────────────────────────────────────────────────┐
│ AppHost (Aspire) — F5 sobe tudo, já temos                              │
│                                                                        │
│  webfrontend (Blazor SSR) — já temos shell                             │
│   ├─ /importar       ← upload CSV/XLSX, validação schema, preview      │
│   ├─ /datasets       ← lista datasets importados                       │
│   ├─ /treinar        ← formulário: dataset + horizonte + algoritmo     │
│   ├─ /jobs           ← lista de jobs de treino + status + métricas     │
│   ├─ /comparar       ← backtest visualizado: eMax vs eSeg vs ML        │
│   └─ /sugerir        ← entrada (lojas, período, dias cobertura)        │
│                        saída: tabela com 3 colunas (eMax, eSeg, ML)    │
│                                                                        │
│  apiservice (.NET API) — já temos                                      │
│   ├─ POST /datasets/import         (com validação)                     │
│   ├─ POST /jobs/training                                               │
│   ├─ GET  /jobs/{id}                                                   │
│   ├─ GET  /models                                                      │
│   └─ POST /infer (executa eMax + eSeg + ML em paralelo)                │
│                                                                        │
│  ★ worker (.NET BackgroundService) — A CRIAR                           │
│   ├─ Lê fila TrainingJob da DB engine (1 job por vez)                  │
│   ├─ Pipeline: load → features → split → train → backtest → save      │
│   ├─ Treina os 3 algoritmos em paralelo e registra métricas            │
│   └─ Publica progresso via OTEL (visível no Aspire Dashboard)          │
│                                                                        │
│  sql                                                                   │
│   ├─ vendas  ← dados importados (cresce com cada import)               │
│   └─ engine  ← Dataset, TrainingJob, Model, Metric, FeatureImportance │
│                                                                        │
│  clickhouse                                                            │
│   └─ vendas-olap ← histórico denso para varredura ao gerar features    │
└────────────────────────────────────────────────────────────────────────┘
```

### Decisões implícitas

- **Worker é dedicado, não BackgroundService dentro do apiservice** — separação de responsabilidades, e dá pra escalar/parar independente. Mais defensável academicamente também.
- **Os 3 algoritmos treinam no mesmo job** — comparação justa, mesmo split, mesmo backtest, métricas alinhadas.
- **Persistência do modelo:** ML.NET salva em `.zip` (formato proprietário); o caminho é registrado na tabela `Model` da DB `engine`. Para `eMax`/`eSeg` o "modelo" é só um JSON com hiperparâmetros (`dias_cobertura`, `z_safety`, etc) — também persistido para reproducibilidade.

---

## 5. Próximos passos

Em ordem, do mais barato ao mais caro:

| Fase | Entrega | Esforço estimado |
|---|---|---|
| **F2.1** | Schemas de dados no SQL Project: tabelas para vendas, estoque, compras, mestre_produtos, mestre_lojas, promocao, iqvia | ~1 dia |
| **F2.2** | Templates de planilha + UI de importação no Blazor (uma página por arquivo, com validação de schema) | ~2 dias |
| **F2.3** | Dataset sintético farma realista (com ruptura, promoção, sazonalidade) — destrava dev sem dados reais | ~1 dia |
| **F2.4** | Projeto `Engine` + `EngineDbContext` (jobs/models/metrics). EF Core migrations no AppHost (`Aspire.Hosting.EntityFrameworkCore` quando publicado) ou fallback manual | ~1 dia |
| **F2.5** | Projeto `Worker` + fila simples | ~1 dia |
| **F3** | Baseline eMax/eSeg — implementação fiel ao varejo farma | ~2 dias |
| **F4** | Pipeline LightGBM com features mínimas (lags + calendar + hierarquia) | ~3 dias |
| **F5** | IQVIA como feature (depois que pipeline base estiver maduro) | ~2 dias |
| **F6** | UI de comparação + sugestão | ~3 dias |

**Total estimado:** ~16 dias úteis (∼3 semanas focadas) para algo apresentável. Reservar 2-3 semanas extras para escrita da tese, ajuste fino e improvisação.

---

## 6. Riscos e perguntas abertas

| Risco | Pergunta | Por quê importa |
|---|---|---|
| **Dados reais vs sintéticos** | Vai usar dados de uma farmácia real (com NDA?) ou só sintéticos? | Dados reais = TCC mais forte mas mais lento. Sintéticos = mais rápido mas resultado pode parecer "fácil demais". Híbrido (sintético calibrado em distribuição real) é o caminho do meio. |
| **Acesso a IQVIA** | IQVIA é caro. Sua universidade ou empresa tem licença? | Sem IQVIA real, dá para simular um sinal "tipo IQVIA" sintético — academicamente honesto se documentado. |
| **Granularidade temporal** | Prever **diário** ou **semanal**? | Diário = mais difícil mas mais útil para sugestão. Semanal = mais fácil de defender estatisticamente e reduz problema de intermitentes. Para TCC, **semanal** com agregação diária na hora da sugestão é o caminho defensável. |
| **Horizonte de previsão** | Quantos dias/semanas para frente? | Sugestão de compra típica cobre o lead time + ciclo de revisão. Ex.: LT=7 dias + cobertura=14 dias → horizonte=21 dias. Define o `horizon` do pipeline. |
| **Escopo de SKUs** | Treinar para **todos** os SKUs ou um recorte (ex.: top 1000 por receita)? | Para TCC, **filtrar top N por giro** simplifica análise e remove ruído da cauda intermitente. |

Decisões nessas 5 áreas calibram tudo abaixo (granularidade dos schemas, complexidade do worker, escopo do dataset sintético). Se houver dúvida, optar por: **dataset híbrido (sintético calibrado)**, **IQVIA simulada**, **granularidade semanal**, **horizonte 4 semanas**, **top 500 SKUs por receita**. Esse default é defensável e cabe no prazo.

---

## Documentos relacionados

- [README.md](../README.md) — visão geral do repositório e roadmap atual
- [CLAUDE.md](../CLAUDE.md) — convenções, decisões técnicas e anti-patterns
- [Docs/schema.md](schema.md) — schema completo dos bancos `Stage` e `engine` (Mermaid ER + descrição de cada tabela)
- [Docs/import-use-case.md](import-use-case.md) — fluxo de importação de dados (API → MinIO → CargasStage → Worker)
- [Docs/olap-schema-migrations.md](olap-schema-migrations.md) — mecanismo de migração do banco OLAP
