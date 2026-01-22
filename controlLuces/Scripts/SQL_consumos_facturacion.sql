-- =====================================================
-- Script para crear la tabla de Consumos y Facturación
-- RETILAP 3.3.3.1.2 - Consumos, facturación y pagos
-- =====================================================

-- Crear tabla principal de consumos y facturación
CREATE TABLE dbo.consumos_facturacion (
    Id INT IDENTITY(1,1) PRIMARY KEY,

    -- Período
    Anio INT NOT NULL,
    Mes INT NOT NULL,
    Municipio NVARCHAR(100) NOT NULL,

    -- Datos de consumo
    ConsumoKwh DECIMAL(18,2) DEFAULT 0,
    TarifaKwh DECIMAL(18,4) DEFAULT 0,

    -- Datos de facturación
    NumeroFactura NVARCHAR(50),
    ValorFacturado DECIMAL(18,2) DEFAULT 0,
    FechaFactura DATE,
    FechaVencimiento DATE,

    -- Datos de pago
    ValorPagado DECIMAL(18,2) DEFAULT 0,
    FechaPago DATE,
    EstadoPago NVARCHAR(20) DEFAULT 'Pendiente', -- Pendiente, Pagado, Vencido

    -- Recaudo del servicio (RETILAP 3.3.3.1.2 - ítem 3)
    RecaudoServicio DECIMAL(18,2) DEFAULT 0,

    -- Observaciones
    Observaciones NVARCHAR(MAX),

    -- Archivo adjunto (FK a tabla archivos)
    IdArchivo INT NULL,

    -- Auditoría
    FechaRegistro DATETIME DEFAULT GETDATE(),
    UsuarioRegistro NVARCHAR(200),

    -- Índices y constraints
    CONSTRAINT UQ_consumos_periodo UNIQUE (Anio, Mes, Municipio),
    CONSTRAINT FK_consumos_archivo FOREIGN KEY (IdArchivo) REFERENCES dbo.archivos(Id)
);

-- Índices para mejorar rendimiento
CREATE INDEX IX_consumos_anio ON dbo.consumos_facturacion(Anio);
CREATE INDEX IX_consumos_municipio ON dbo.consumos_facturacion(Municipio);
CREATE INDEX IX_consumos_estado ON dbo.consumos_facturacion(EstadoPago);

-- =====================================================
-- Tabla de Recursos de Financiamiento (RETILAP ítem 4)
-- =====================================================
CREATE TABLE dbo.recursos_financiamiento (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Anio INT NOT NULL,
    Municipio NVARCHAR(100) NOT NULL,
    FuenteRecurso NVARCHAR(100) NOT NULL, -- SGP, Recursos propios, Crédito, Regalías
    TipoProyecto NVARCHAR(50) NOT NULL, -- Expansión, Modernización
    ValorRecibido DECIMAL(18,2) DEFAULT 0,
    Descripcion NVARCHAR(MAX),
    FechaRegistro DATETIME DEFAULT GETDATE(),
    UsuarioRegistro NVARCHAR(200),
    IdArchivo INT NULL,

    CONSTRAINT FK_recursos_archivo FOREIGN KEY (IdArchivo) REFERENCES dbo.archivos(Id)
);

CREATE INDEX IX_recursos_anio ON dbo.recursos_financiamiento(Anio);
CREATE INDEX IX_recursos_municipio ON dbo.recursos_financiamiento(Municipio);

-- =====================================================
-- Tabla de Indicadores de Calidad (RETILAP ítem 9)
-- =====================================================
CREATE TABLE dbo.indicadores_calidad (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Anio INT NOT NULL,
    Municipio NVARCHAR(100) NOT NULL,

    -- a. Calidad - % ahorro energía
    PorcentajeAhorroEnergia DECIMAL(5,2) DEFAULT 0,
    MetaAhorroEnergia DECIMAL(5,2) DEFAULT 0,

    -- b. Cobertura urbano/rural
    PorcentajeCoberturaUrbano DECIMAL(5,2) DEFAULT 0,
    PorcentajeCoberturaRural DECIMAL(5,2) DEFAULT 0,
    MetaCoberturaUrbano DECIMAL(5,2) DEFAULT 0,
    MetaCoberturaRural DECIMAL(5,2) DEFAULT 0,

    -- c. Eficiencia energética
    PorcentajeEficienciaEnergetica DECIMAL(5,2) DEFAULT 0,
    MetaEficienciaEnergetica DECIMAL(5,2) DEFAULT 0,

    -- Datos base para cálculos
    TotalLuminariasUrbano INT DEFAULT 0,
    TotalLuminariasRural INT DEFAULT 0,
    LuminariasExpansion INT DEFAULT 0,
    ConsumoTotalKwh DECIMAL(18,2) DEFAULT 0,
    PotenciaTotalKw DECIMAL(18,2) DEFAULT 0,

    -- Auditoría
    FechaRegistro DATETIME DEFAULT GETDATE(),
    UsuarioRegistro NVARCHAR(200),

    CONSTRAINT UQ_indicadores_periodo UNIQUE (Anio, Municipio)
);

CREATE INDEX IX_indicadores_anio ON dbo.indicadores_calidad(Anio);
CREATE INDEX IX_indicadores_municipio ON dbo.indicadores_calidad(Municipio);

-- =====================================================
-- Datos de ejemplo (opcional - comentar si no se requiere)
-- =====================================================
/*
INSERT INTO dbo.consumos_facturacion
(Anio, Mes, Municipio, ConsumoKwh, TarifaKwh, NumeroFactura, ValorFacturado, FechaFactura, FechaVencimiento, ValorPagado, FechaPago, EstadoPago, RecaudoServicio, UsuarioRegistro)
VALUES
(2024, 1, 'Madrid', 125000, 650.50, 'FAC-2024-001', 81312500, '2024-01-15', '2024-02-15', 81312500, '2024-02-10', 'Pagado', 95000000, 'Admin'),
(2024, 2, 'Madrid', 118500, 655.25, 'FAC-2024-002', 77647125, '2024-02-15', '2024-03-15', 77647125, '2024-03-12', 'Pagado', 92500000, 'Admin'),
(2024, 1, 'Chia', 85000, 648.00, 'FAC-2024-101', 55080000, '2024-01-15', '2024-02-15', 55080000, '2024-02-08', 'Pagado', 68000000, 'Admin'),
(2024, 2, 'Chia', 82300, 652.75, 'FAC-2024-102', 53721325, '2024-02-15', '2024-03-15', 53721325, '2024-03-10', 'Pagado', 65500000, 'Admin');
*/
