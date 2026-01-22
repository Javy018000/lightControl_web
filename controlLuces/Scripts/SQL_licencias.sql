-- =====================================================
-- SISTEMA DE LICENCIAMIENTO COMERCIAL
-- Base de datos: lightcon_luminaria
-- Ejecutar en SQL Server Management Studio
-- =====================================================

-- IMPORTANTE: Seleccionar la base de datos correcta
USE lightcon_luminaria
GO

-- =====================================================
-- PASO 1: CREAR TABLA DE LICENCIAS
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'licencias')
BEGIN
    CREATE TABLE [lightcon_lumin].[licencias] (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClaveLicencia NVARCHAR(100) NOT NULL UNIQUE,
        NombreCliente NVARCHAR(200) NOT NULL,
        Municipio NVARCHAR(100) NOT NULL,
        NIT NVARCHAR(50),
        Contacto NVARCHAR(200),
        Email NVARCHAR(200),
        Telefono NVARCHAR(50),
        FechaInicio DATETIME NOT NULL,
        FechaExpiracion DATETIME NOT NULL,
        MaxUsuarios INT DEFAULT 10,
        Estado NVARCHAR(20) DEFAULT 'Activa', -- Activa, Suspendida, Expirada, Cancelada
        ModulosPermitidos NVARCHAR(500), -- Lista de módulos separados por coma
        FechaCreacion DATETIME DEFAULT GETDATE(),
        FechaUltimaValidacion DATETIME,
        Observaciones NVARCHAR(1000)
    )
    PRINT 'Tabla licencias creada exitosamente'
END
ELSE
BEGIN
    PRINT 'Tabla licencias ya existe'
END

-- =====================================================
-- PASO 2: CREAR TABLA DE HISTORIAL DE VALIDACIONES
-- Para auditoría y control de accesos
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'licencias_historial')
BEGIN
    CREATE TABLE [lightcon_lumin].[licencias_historial] (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdLicencia INT NOT NULL,
        FechaValidacion DATETIME DEFAULT GETDATE(),
        IP NVARCHAR(50),
        Usuario NVARCHAR(100),
        Resultado NVARCHAR(50), -- Exitosa, Fallida, Expirada
        Detalle NVARCHAR(500),
        FOREIGN KEY (IdLicencia) REFERENCES [lightcon_lumin].[licencias](Id)
    )
    PRINT 'Tabla licencias_historial creada exitosamente'
END
ELSE
BEGIN
    PRINT 'Tabla licencias_historial ya existe'
END

-- =====================================================
-- PASO 3: INSERTAR LICENCIAS DE PRUEBA (MADRID Y CHÍA)
-- =====================================================

-- Licencia para Madrid
IF NOT EXISTS (SELECT * FROM [lightcon_lumin].[licencias] WHERE Municipio = 'Madrid')
BEGIN
    INSERT INTO [lightcon_lumin].[licencias]
    (ClaveLicencia, NombreCliente, Municipio, NIT, Contacto, Email, Telefono,
     FechaInicio, FechaExpiracion, MaxUsuarios, Estado, ModulosPermitidos, Observaciones)
    VALUES
    ('MADRID-' + CAST(YEAR(GETDATE()) AS VARCHAR) + '-LIGHTCONTROL-001',
     'Alcaldía Municipal de Madrid',
     'Madrid',
     '800.000.000-1',
     'Administrador Municipal',
     'alumbrado@madrid-cundinamarca.gov.co',
     '(601) 000-0000',
     GETDATE(),
     DATEADD(YEAR, 1, GETDATE()), -- 1 año de licencia
     20,
     'Activa',
     'PQRS,Ordenes,Infraestructura,Postes,Cajas,Archivos,Consumos,Usuarios',
     'Licencia inicial - Municipio Madrid Cundinamarca')
    PRINT 'Licencia de Madrid creada exitosamente'
END

-- Licencia para Chía
IF NOT EXISTS (SELECT * FROM [lightcon_lumin].[licencias] WHERE Municipio = 'Chia')
BEGIN
    INSERT INTO [lightcon_lumin].[licencias]
    (ClaveLicencia, NombreCliente, Municipio, NIT, Contacto, Email, Telefono,
     FechaInicio, FechaExpiracion, MaxUsuarios, Estado, ModulosPermitidos, Observaciones)
    VALUES
    ('CHIA-' + CAST(YEAR(GETDATE()) AS VARCHAR) + '-LIGHTCONTROL-001',
     'Alcaldía Municipal de Chía',
     'Chia',
     '800.000.000-2',
     'Administrador Municipal',
     'alumbrado@chia-cundinamarca.gov.co',
     '(601) 000-0000',
     GETDATE(),
     DATEADD(YEAR, 1, GETDATE()), -- 1 año de licencia
     20,
     'Activa',
     'PQRS,Ordenes,Infraestructura,Postes,Cajas,Archivos,Consumos,Usuarios',
     'Licencia inicial - Municipio Chía Cundinamarca')
    PRINT 'Licencia de Chía creada exitosamente'
END

-- =====================================================
-- PASO 4: PROCEDIMIENTO PARA VALIDAR LICENCIA
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_ValidarLicencia')
    DROP PROCEDURE [lightcon_lumin].[sp_ValidarLicencia]
GO

CREATE PROCEDURE [lightcon_lumin].[sp_ValidarLicencia]
    @Municipio NVARCHAR(100),
    @IP NVARCHAR(50) = NULL,
    @Usuario NVARCHAR(100) = NULL
AS
BEGIN
    DECLARE @IdLicencia INT
    DECLARE @Estado NVARCHAR(20)
    DECLARE @FechaExpiracion DATETIME
    DECLARE @Resultado NVARCHAR(50)
    DECLARE @Detalle NVARCHAR(500)

    -- Buscar licencia del municipio
    SELECT @IdLicencia = Id, @Estado = Estado, @FechaExpiracion = FechaExpiracion
    FROM [lightcon_lumin].[licencias]
    WHERE Municipio = @Municipio

    IF @IdLicencia IS NULL
    BEGIN
        SET @Resultado = 'Fallida'
        SET @Detalle = 'No existe licencia para este municipio'

        SELECT 0 AS Valida, @Resultado AS Resultado, @Detalle AS Mensaje, NULL AS FechaExpiracion
        RETURN
    END

    -- Verificar si está expirada
    IF @FechaExpiracion < GETDATE()
    BEGIN
        SET @Resultado = 'Expirada'
        SET @Detalle = 'La licencia ha expirado el ' + CONVERT(NVARCHAR, @FechaExpiracion, 103)

        -- Actualizar estado
        UPDATE [lightcon_lumin].[licencias] SET Estado = 'Expirada' WHERE Id = @IdLicencia
    END
    ELSE IF @Estado != 'Activa'
    BEGIN
        SET @Resultado = 'Fallida'
        SET @Detalle = 'La licencia está ' + @Estado
    END
    ELSE
    BEGIN
        SET @Resultado = 'Exitosa'
        SET @Detalle = 'Licencia válida'

        -- Actualizar última validación
        UPDATE [lightcon_lumin].[licencias]
        SET FechaUltimaValidacion = GETDATE()
        WHERE Id = @IdLicencia
    END

    -- Registrar en historial
    INSERT INTO [lightcon_lumin].[licencias_historial]
    (IdLicencia, IP, Usuario, Resultado, Detalle)
    VALUES (@IdLicencia, @IP, @Usuario, @Resultado, @Detalle)

    -- Retornar resultado
    SELECT
        CASE WHEN @Resultado = 'Exitosa' THEN 1 ELSE 0 END AS Valida,
        @Resultado AS Resultado,
        @Detalle AS Mensaje,
        @FechaExpiracion AS FechaExpiracion,
        l.NombreCliente,
        l.ModulosPermitidos,
        l.MaxUsuarios
    FROM [lightcon_lumin].[licencias] l
    WHERE l.Id = @IdLicencia
END
GO

PRINT 'Procedimiento sp_ValidarLicencia creado exitosamente'

-- =====================================================
-- VERIFICAR RESULTADO
-- =====================================================
SELECT 'Sistema de Licenciamiento configurado correctamente' AS Resultado

-- Ver licencias existentes
SELECT Id, ClaveLicencia, NombreCliente, Municipio, Estado,
       FechaInicio, FechaExpiracion, MaxUsuarios
FROM [lightcon_lumin].[licencias]
