-- Vendas agregadas por (Data, LojaId, Sku). Assume granularidade diária —
-- detalhe de cupom não entra aqui (irrelevante para forecast).
-- Quantidade em DECIMAL(12,3) para suportar venda fracionada (manipulação,
-- fracionamento de blister, etc).
CREATE TABLE dbo.Vendas
(
    Data            DATE            NOT NULL,
    LojaId          INT             NOT NULL,
    Sku             NVARCHAR(30)    NOT NULL,
    Quantidade      DECIMAL(12,3)   NOT NULL,
    PrecoUnitario   DECIMAL(12,4)   NOT NULL,
    ValorTotal      DECIMAL(14,4)   NOT NULL,

    CONSTRAINT PK_Vendas PRIMARY KEY (Data, LojaId, Sku),
    CONSTRAINT FK_Vendas_Produtos FOREIGN KEY (Sku)    REFERENCES dbo.Produtos(Sku),
    CONSTRAINT FK_Vendas_Lojas    FOREIGN KEY (LojaId) REFERENCES dbo.Lojas(LojaId),

    -- Padrão de acesso típico de feature extraction: por SKU em janela temporal.
    INDEX IX_Vendas_Sku_Data NONCLUSTERED (Sku, Data) INCLUDE (LojaId, Quantidade)
);
