-- Histórico de compras / suprimento. DataRecebimento NULL = pedido em
-- trânsito. Lead time real = DATEDIFF(day, DataPedido, DataRecebimento)
-- por SKU x Fornecedor (calculado em view ou no consumidor).
CREATE TABLE dbo.Compras
(
    CompraId        BIGINT          IDENTITY(1,1) NOT NULL,
    DataPedido      DATE            NOT NULL,
    DataRecebimento DATE            NULL,
    LojaId          INT             NOT NULL,
    Sku             NVARCHAR(30)    NOT NULL,
    Quantidade      DECIMAL(12,3)   NOT NULL,
    Fornecedor      NVARCHAR(120)   NULL,

    CONSTRAINT PK_Compras PRIMARY KEY (CompraId),
    CONSTRAINT FK_Compras_Produtos FOREIGN KEY (Sku)    REFERENCES dbo.Produtos(Sku),
    CONSTRAINT FK_Compras_Lojas    FOREIGN KEY (LojaId) REFERENCES dbo.Lojas(LojaId),

    INDEX IX_Compras_Sku_DataPedido      NONCLUSTERED (Sku, DataPedido),
    INDEX IX_Compras_Sku_DataRecebimento NONCLUSTERED (Sku, DataRecebimento) WHERE DataRecebimento IS NOT NULL
);
