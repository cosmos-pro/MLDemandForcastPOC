# 06 — Glossário

Definições curtas dos termos usados nas docs anteriores, em ordem alfabética. Cada verbete remete ao doc onde está desenvolvido em profundidade.

---

### ABC (curva ABC) {#abc}
Classificação de SKUs por concentração de volume: classe **A** (top, ~70% do volume), **B** (~20%), **C** (cauda longa, ~10% do volume em muitos itens). Recalculada a cada treino e usada como feature. → [01 — Dataset](01-dataset-sintetico.md#abc) · [05 — Pipeline](05-pipeline-treino-completo.md#abc-dinamica-e-reproduzivel)

### Anti-leakage
Práticas que impedem o modelo de "ver o futuro" no treino. No nosso pipeline: lags respeitam **lead time** de 7 dias e features de promoção exigem ser conhecidas no momento da decisão. → [02 — Features](02-feature-engineering.md#anti-leakage)

### Backtest
Avaliação retrospectiva de um modelo: simula que o modelo foi usado em datas passadas, compara previsões com vendas reais. Nosso backtest usa **walk-forward**.

### Baseline
Modelo simples, usado como "régua mínima" — o modelo de ML só vale o esforço se vence o baseline com folga. Usamos **Naïve Sazonal** e **Média Móvel**. → [03 — Engines](03-engines-previsao.md)

### Boosting (gradient boosting)
Técnica de ensemble onde árvores de decisão são treinadas em **sequência**, cada uma corrigindo erros das anteriores. Base do LightGBM. → [03 — Engines](03-engines-previsao.md#lightgbm)

### Categórica (feature)
Atributo que assume valores discretos sem ordem natural (e.g., `Sku`, `Categoria`, `UF`). Convertida para numérica via **OneHotEncoding** antes de entrar no modelo. → [02 — Features](02-feature-engineering.md) · [03 — Engines](03-engines-previsao.md#lightgbm)

### Cobertura (dias)
Estoque médio dividido pela demanda diária média — quantos dias o estoque cobriria sem reposição. Menor = capital trabalha mais (sem cair em ruptura). → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### Computus
Algoritmo (Meeus/Jones/Butcher) que calcula a data da **Páscoa** em qualquer ano. Base para gerar feriados móveis BR (Carnaval, Sexta Santa, Corpus Christi). → [01 — Dataset](01-dataset-sintetico.md#sazonalidade)

### Custo total (simulação)
KPI sintético da simulação de compras: `α × Σ estoque-dia + β × venda perdida`. Combina custo de carregamento (capital parado, armazenagem) e custo de ruptura (margem perdida, cliente insatisfeito). α e β são parâmetros. → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### Cycle service level
Probabilidade de não haver ruptura durante um ciclo de revisão — calculado como fração de dias-SKU-loja sem ruptura na simulação. Sensível a SKUs de baixo giro (vs **fill rate**, sensível a alto giro). → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### Decision Tree (árvore de decisão)
Estrutura onde cada nó pergunta "feature X > valor?" e cada folha tem uma previsão. Modelo único é fraco; **ensembles** (random forest, gradient boosting) são fortes. → [03 — Engines](03-engines-previsao.md#lightgbm)

### Densificação (de série temporal)
Preencher dias sem venda com `Quantidade=0`. Necessário porque `(SKU, Loja, Dia)` que não vendeu **não aparece** no CSV de vendas; sem densificar, lags ficam errados. → [02 — Features](02-feature-engineering.md)

### Drill-down
Quebrar uma métrica global por dimensão (categoria, classe ABC, loja, UF) para detectar **regressões locais** que a média global esconde. → [04 — Avaliação](04-avaliacao-metricas.md#drill-down)

### eMax / eSeg
Política clássica de sugestão de compra usada em varejo farma: define **estoque máximo** (eMax = S) e **estoque de segurança** (eSeg) por SKU/loja. Quando posição cai abaixo de `s = eSeg + μ × LT`, repõe até eMax. Equivalente a um modelo **(s, S)** de inventário. **Não é previsão de demanda** — é regra de decisão sobre quanto comprar. O TCC compara essa política com a derivada do forecast ML. → [07 — Sugestão de compra](07-sugestao-compra.md#emax-eseg)

### Engine (motor de previsão)
Classe que implementa `IForecastEngine` e sabe treinar/prever. Três no POC: Naïve, MA, LightGBM. → [03 — Engines](03-engines-previsao.md)

### Fator de serviço (z)
Quantil da distribuição Normal usado para dimensionar safety stock. Valores comuns: z = 1,28 (90%), 1,65 (95%), 2,05 (98%). Maior z = mais segurança, mais estoque. Em farma, 95% é o ponto de partida padrão. → [07 — Sugestão de compra](07-sugestao-compra.md#emax-eseg)

### Feature
Atributo que entra no modelo. Sinônimo de "variável independente" ou "X". Construir boas features = engenharia de atributos / **feature engineering**. → [02 — Features](02-feature-engineering.md)

### Fill rate
Fração da demanda total **atendida** em unidades (1 − venda perdida / demanda total). KPI primário do varejo: capta o impacto em unidades vendidas, ponderando naturalmente SKUs de alto giro. Sinônimo de "nível de serviço (unidades)" na nossa UI. → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### Fold (em walk-forward)
Uma divisão treino/teste dentro do backtest. Usamos 4 folds × 14 dias de teste. → [04 — Avaliação](04-avaliacao-metricas.md#walk-forward)

### Giro (turnover)
Demanda no período dividida pelo estoque médio. Mais giro = capital trabalha mais (saudável); pode indicar risco de ruptura se muito alto. → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### Gradient Boosting
Ver **Boosting**.

### Hierarquia
Estrutura que agrupa SKUs (categoria, subcategoria, princípio ativo) e lojas (UF, região). Entra como feature e como dimensão de drill-down.

### Hiperparâmetro
Parâmetro que controla **como** o modelo aprende, e que **não** é aprendido a partir dos dados — você define antes do treino. Ex.: `NumberOfLeaves`, `LearningRate`. → [03 — Engines](03-engines-previsao.md#lightgbm)

### IQVIA
Provedor de dados de mercado farmacêutico. Fornece volume de mercado por princípio ativo × geografia × período. No POC, é gerado sinteticamente. → [01 — Dataset](01-dataset-sintetico.md)

### Lag (defasagem)
Valor da venda em um dia anterior, usado como feature. `Lag7` = venda há 7 dias. Precisa respeitar **lead time** (lag mínimo = lead time). → [02 — Features](02-feature-engineering.md)

### Lead time {#lead-time}
Tempo entre **fazer o pedido** e **ter o produto disponível para vender**. No POC, 7 dias. Define o lag mínimo das features para evitar leakage. → [02 — Features](02-feature-engineering.md#lead-time)

### Leakage (vazamento)
Quando o modelo recebe no treino informação que **só existiria no futuro** em produção. Resultado: métrica boa no papel, fracasso em produção. Razão #1 de modelos "ótimos" que falham. → [02 — Features](02-feature-engineering.md#anti-leakage)

### LightGBM
Implementação eficiente de gradient boosted decision trees pela Microsoft Research. Vencedora de muitas competições Kaggle / M5 em forecasting de varejo. Nosso engine principal. → [03 — Engines](03-engines-previsao.md#lightgbm)

### MAE — Mean Absolute Error
Média dos erros absolutos. Unidade igual à venda. Não comparável entre SKUs de volumes diferentes. → [04 — Avaliação](04-avaliacao-metricas.md#metricas)

### MAPE — Mean Absolute Percentage Error
Média dos erros percentuais. **Quebra com vendas zero ou pequenas**. Reportada por tradição acadêmica, mas WAPE é preferida. → [04 — Avaliação](04-avaliacao-metricas.md#metricas)

### Masking de ruptura
Não treinar com observações onde houve **stockout** (estoque zero), porque venda zero por falta de produto ≠ demanda zero. Sem este tratamento, modelo subestima sistematicamente. → [05 — Pipeline](05-pipeline-treino-completo.md#ruptura-mask)

### Média Móvel
Engine baseline que usa `RollMean28` (média dos últimos 28 dias respeitando lead time) como previsão. → [03 — Engines](03-engines-previsao.md#media-movel)

### MinIO
Object storage compatível com S3, usado no POC para armazenar modelos treinados (`.zip`) e arquivos de importação (ZIPs com CSVs).

### ML.NET
Framework de Machine Learning do .NET. Provê pipelines tipados, treinadores (LightGBM, FastTree, etc.) e serialização de modelos. Versão 4.0.2 no POC.

### Modelo global vs por SKU
**Global:** um único modelo treinado com todos os SKUs simultaneamente, com `Sku` como feature. **Por SKU:** um modelo por série. POC usa global (padrão moderno; ver M5). → [05 — Pipeline](05-pipeline-treino-completo.md#modelo-global)

### Naïve Sazonal
Engine baseline trivial: previsão = `Lag7` (venda do mesmo dia da semana passada). → [03 — Engines](03-engines-previsao.md)

### OneHotEncoding
Codificação de categórica em N colunas binárias (uma por valor único). Necessária porque LightGBM e maioria dos modelos numéricos não consomem strings. → [03 — Engines](03-engines-previsao.md#lightgbm)

### Pareto (regra 80/20, power-law)
Distribuição onde poucos elementos concentram a maior parte do volume. Em farma, top 20% dos SKUs costuma responder por ~70-80% das vendas. Modelada como power-law no gerador sintético. → [01 — Dataset](01-dataset-sintetico.md#abc)

### Poisson (distribuição)
Distribuição estatística de contagem. Modela demanda diária: valores inteiros ≥ 0, variância proporcional à média. Usada para gerar vendas sintéticas. → [01 — Dataset](01-dataset-sintetico.md#sazonalidade)

### Política (s, S)
Modelo clássico de inventário: quando a **posição de estoque** (físico + em trânsito) cai abaixo de **s** (reorder point), pede o suficiente para chegar em **S** (order-up-to-level). Quantidade pedida = max(0, S − posição). Tanto **eMax/eSeg** quanto **ROP+forecast** instanciam (s, S) — mudam só de onde s e S vêm. → [07 — Sugestão de compra](07-sugestao-compra.md)

### Polling
Padrão UI: a página consulta o servidor periodicamente (a cada 5s) até detectar mudança de estado. Usado para acompanhar progresso de jobs de import e treino.

### Posição de estoque
Soma do estoque físico (na prateleira) + em trânsito (pedidos lançados que ainda não chegaram). É o que a política compara contra `s` — evita o "pânico de pedir de novo" entre o pedido e a chegada. → [07 — Sugestão de compra](07-sugestao-compra.md#simulador)

### Quantil (forecast probabilístico)
Em vez de prever só a média, prever que existe X% de chance da demanda ser ≤ Y. Útil para safety stock. Não usado no POC, candidato a iteração futura.

### Replay de compras
Simulação determinística que aplica uma política de compra ao histórico real de vendas, dia-a-dia, série-por-série. Permite comparar políticas no mesmo terreno (mesmo estoque inicial, mesma demanda real). É o protocolo central de F8. → [07 — Sugestão de compra](07-sugestao-compra.md#simulador)

### ROP (reorder point)
Ponto de reabastecimento: nível de estoque abaixo do qual a política dispara um novo pedido. Na política clássica, ROP = μ·LT + safety. Na forecast-based, ROP = Σ forecast(LT) + safety. É o "s" do modelo (s, S). → [07 — Sugestão de compra](07-sugestao-compra.md#rop-forecast)

### Regressão (no contexto do drill-down)
**Atenção:** termo ambíguo. No nosso contexto = "**LightGBM regrediu**" significa que **piorou em relação ao baseline** numa dimensão específica (categoria/loja/etc.). Não confundir com "regressão" do ML (que é o tipo de modelo que prevê números, oposto de classificação). → [04 — Avaliação](04-avaliacao-metricas.md#drill-down)

### Rolling (mean, std, max)
Estatísticas calculadas em janela móvel. `RollMean28` = média dos últimos 28 dias (respeitando lead time). Suaviza ruído e captura nível. → [02 — Features](02-feature-engineering.md)

### RMSE — Root Mean Squared Error
Raiz da média dos erros ao quadrado. **Pune outliers**. Útil quando subestimativa grande é mais cara que pequena. → [04 — Avaliação](04-avaliacao-metricas.md#metricas)

### Ruptura (stockout)
Período em que o produto **não está disponível** na loja. Venda observada = 0 mesmo havendo demanda. Sinal enganador para modelos ingênuos. → [01 — Dataset](01-dataset-sintetico.md#ruptura) · [05 — Pipeline](05-pipeline-treino-completo.md#ruptura-mask)

### Safety stock
Estoque de segurança — quantidade adicional mantida para absorver variabilidade da demanda durante o lead time. Fórmula clássica: `z · σ · √LT`. Na nossa política forecast-based, σ é o desvio do **resíduo do forecast** (não da demanda bruta) — encolhe quando o modelo é bom. → [07 — Sugestão de compra](07-sugestao-compra.md#rop-forecast)

### Sazonalidade
Padrão recorrente em série temporal. **Semanal** (fim-de-semana diferente de dia útil), **anual** (verão vs inverno), **eventos** (Carnaval, Black Friday). Capturada via features de calendário + lag de 7 dias. → [01 — Dataset](01-dataset-sintetico.md#sazonalidade)

### SimulacaoCompra
Entidade no banco `engine` que registra um job de simulação de compras: TreinoJob origem, janela, lead time, ciclo, fator z, status, resultado JSON. Análoga ao **TreinoJob** mas para F8. → [07 — Sugestão de compra](07-sugestao-compra.md)

### Seed
Valor inicial do gerador pseudoaleatório, fixado para reprodutibilidade. Usado no gerador de dataset (`SyntheticDatasetOptions.Seed`) e no LightGBM (`LightGbmHyperparameters.Seed`).

### Série temporal
Sequência de valores indexada por tempo, com **dependência entre observações** consecutivas. Diferente de dataset tabular comum: **não se pode embaralhar treino/teste**. → [02 — Features](02-feature-engineering.md#serie-temporal)

### SKU (Stock Keeping Unit)
Unidade mínima de produto controlada por estoque. No farma típico: cada apresentação (princípio ativo × dosagem × forma × marca) é um SKU.

### Splits (em decision tree)
Cortes em features (e.g., `Lag7 > 10?`) que dividem o espaço de dados nos nós. LightGBM escolhe splits que maximizam o ganho (redução do erro). → [03 — Engines](03-engines-previsao.md#lightgbm)

### Stage (banco)
Schema SQL Server gerenciado por **DACPAC** com as tabelas de dados importados pelo usuário (Lojas, Produtos, Vendas, EstoquesDiarios, Compras, Promocoes, MercadoIqvia). Engine **só lê**, nunca escreve. → [CLAUDE.md §4](../CLAUDE.md)

### Stockout
Ver **Ruptura**.

### TreinoJob
Entidade no banco `engine` que registra um job de treino: parâmetros, status, resultado JSON, key do modelo no MinIO. Permite auditoria e reprodutibilidade. → [05 — Pipeline](05-pipeline-treino-completo.md)

### Venda perdida (lost sales)
Demanda real que não foi atendida por falta de estoque — vira queixa do cliente e receita não realizada. Em sistemas com pedido para entrega futura (backorders), parte poderia ser recuperada; em farma de varejo, raramente. → [07 — Sugestão de compra](07-sugestao-compra.md#kpis)

### WAPE — Weighted Absolute Percentage Error
Soma dos erros absolutos / soma das vendas. Adimensional, **robusta a zeros**, ponderada por volume. Métrica **primária** do POC. → [04 — Avaliação](04-avaliacao-metricas.md#wape)

### Walk-forward (rolling origin)
Protocolo de backtest que faz várias divisões treino/teste deslizantes no tempo, simulando o uso futuro do modelo. Alternativa correta ao train/test split aleatório (que **não vale** em série temporal). → [04 — Avaliação](04-avaliacao-metricas.md#walk-forward)

### Worker
Processo de fundo (.NET Worker Service) que pega TreinoJobs pendentes no banco e executa o pipeline de treino assíncronamente. Resiliente a reinício. → [05 — Pipeline](05-pipeline-treino-completo.md)

---

## Voltar ao índice

→ [README das docs](README.md)
