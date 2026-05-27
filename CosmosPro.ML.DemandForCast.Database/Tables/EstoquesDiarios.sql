-- Snapshots diários de estoque por (Data, LojaId, Sku). Usado para identificar
-- ruptura (QuantidadeEmEstoque <= 0 ou abaixo de threshold) — crítico para
-- evitar viés "venda=0 ⇒ demanda=0" no treino.
CREATE TABLE dbo.EstoquesDiarios
(
    Data                DATE            NOT NULL,
    LojaId              INT             NOT NULL,
    Sku                 NVARCHAR(30)    NOT NULL,
    QuantidadeEmEstoque DECIMAL(12,3)   NOT NULL,

    CONSTRAINT PK_EstoquesDiarios PRIMARY KEY (Data, LojaId, Sku),
    CONSTRAINT FK_EstoquesDiarios_Produtos FOREIGN KEY (Sku)    REFERENCES dbo.Produtos(Sku),
    CONSTRAINT FK_EstoquesDiarios_Lojas    FOREIGN KEY (LojaId) REFERENCES dbo.Lojas(LojaId),

    INDEX IX_EstoquesDiarios_Sku_Data NONCLUSTERED (Sku, Data) INCLUDE (LojaId, QuantidadeEmEstoque)
);
