-- Promoções vigentes (intervalo de datas + SKU + opcionalmente loja).
-- LojaId NULL = promoção aplicada a todas as lojas (campanha nacional).
-- Feature crítica para o ML separar venda promocional de venda regular.
CREATE TABLE dbo.Promocoes
(
    PromocaoId  BIGINT          IDENTITY(1,1) NOT NULL,
    DataInicio  DATE            NOT NULL,
    DataFim     DATE            NOT NULL,
    Sku         NVARCHAR(30)    NOT NULL,
    LojaId      INT             NULL,
    Tipo        NVARCHAR(50)    NULL, -- ex.: 'desconto', 'leve3pague2', 'queima'
    DescontoPct DECIMAL(5,2)    NULL, -- 0-100

    CONSTRAINT PK_Promocoes          PRIMARY KEY (PromocaoId),
    CONSTRAINT FK_Promocoes_Produtos FOREIGN KEY (Sku)    REFERENCES dbo.Produtos(Sku),
    CONSTRAINT FK_Promocoes_Lojas    FOREIGN KEY (LojaId) REFERENCES dbo.Lojas(LojaId),
    CONSTRAINT CK_Promocoes_Intervalo CHECK (DataInicio <= DataFim),

    INDEX IX_Promocoes_Sku_DataInicio NONCLUSTERED (Sku, DataInicio, DataFim) INCLUDE (LojaId, DescontoPct)
);
