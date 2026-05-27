-- Mestre de lojas (pontos de venda da rede).
CREATE TABLE dbo.Lojas
(
    LojaId             INT             NOT NULL,
    Nome               NVARCHAR(120)   NOT NULL,
    UF                 CHAR(2)         NOT NULL,
    Cidade             NVARCHAR(100)   NOT NULL,
    Regiao             NVARCHAR(50)    NULL,
    Perfil             NVARCHAR(30)    NULL, -- 'rua', 'shopping', 'popular', 'premium'
    DiasOperacaoSemana TINYINT         NOT NULL CONSTRAINT DF_Lojas_DiasOperacaoSemana DEFAULT 7,
    DataAbertura       DATE            NULL,
    Ativo              BIT             NOT NULL CONSTRAINT DF_Lojas_Ativo DEFAULT 1,

    CONSTRAINT PK_Lojas PRIMARY KEY (LojaId)
);
