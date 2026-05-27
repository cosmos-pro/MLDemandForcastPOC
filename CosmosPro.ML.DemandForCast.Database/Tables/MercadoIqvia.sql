-- Sinal exógeno de mercado farma (IQVIA-like). Granularidade mensal por
-- princípio ativo × UF. Para TCC sem licença IQVIA real, este schema é
-- compatível com geração sintética calibrada.
-- Mes: primeiro dia do mês de referência (yyyy-MM-01).
-- DemandaMercadoUnidades: total de unidades estimadas vendidas no mercado.
-- MarketShareCategoria: 0-1, fração da categoria representada por este
--   princípio ativo na UF.
CREATE TABLE dbo.MercadoIqvia
(
    Mes                    DATE          NOT NULL,
    PrincipioAtivo         NVARCHAR(200) NOT NULL,
    UF                     CHAR(2)       NOT NULL,
    DemandaMercadoUnidades DECIMAL(18,3) NOT NULL,
    MarketShareCategoria   DECIMAL(6,4)  NULL,

    CONSTRAINT PK_MercadoIqvia PRIMARY KEY (Mes, PrincipioAtivo, UF),
    CONSTRAINT CK_MercadoIqvia_MarketShare CHECK (MarketShareCategoria IS NULL OR (MarketShareCategoria >= 0 AND MarketShareCategoria <= 1)),
    CONSTRAINT CK_MercadoIqvia_DiaUm CHECK (DAY(Mes) = 1)
);
