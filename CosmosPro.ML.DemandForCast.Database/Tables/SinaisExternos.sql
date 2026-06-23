-- Sinais exógenos regionais por dia. Formato longo (EAV leve) para acomodar
-- vários tipos sem mudar schema: Tipo='Clima' (temperatura °C), 'Gripe' (índice
-- de incidência 0..~120). Geografia = UF (poderia ser município/região no futuro).
--
-- Semântica de disponibilidade (consumida no feature engineering, F5):
--   * Clima: conhecido do futuro (previsão do tempo cobre o lead time) → feature do dia-alvo D.
--   * Gripe: defasado (reporte epidemiológico atrasa) → feature até D - lead time.
-- A tabela só guarda o valor por (Data, Geografia, Tipo); a regra de defasagem é
-- aplicada por quem lê.
CREATE TABLE dbo.SinaisExternos
(
    Data        DATE          NOT NULL,
    Geografia   VARCHAR(40)   NOT NULL,
    Tipo        VARCHAR(20)   NOT NULL,
    Valor       DECIMAL(10,4) NOT NULL,

    CONSTRAINT PK_SinaisExternos PRIMARY KEY (Data, Geografia, Tipo),
    INDEX IX_SinaisExternos_Tipo_Geo_Data NONCLUSTERED (Tipo, Geografia, Data)
);
