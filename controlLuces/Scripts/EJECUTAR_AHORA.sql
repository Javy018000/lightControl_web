-- =====================================================
-- EJECUTAR ESTE SCRIPT EN SQL SERVER MANAGEMENT STUDIO
-- Base de datos: lightcon_luminaria
-- Seleccionar todo (Ctrl+A) y ejecutar (F5)
-- =====================================================

-- =====================================================
-- PASO 1: ELIMINAR TABLA VIEJA (consumos_facturacion)
-- =====================================================
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'consumos_facturacion')
BEGIN
    DROP TABLE dbo.consumos_facturacion
    PRINT 'Tabla consumos_facturacion eliminada'
END

-- =====================================================
-- PASO 2: CREAR NUEVA TABLA (archivos_consumos)
-- Esta tabla es para guardar archivos de facturas
-- igual que la tabla archivos existente
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'archivos_consumos')
BEGIN
    CREATE TABLE [lightcon_lumin].[archivos_consumos] (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        NombreArchivo NVARCHAR(500) NOT NULL,
        RutaArchivo NVARCHAR(1000) NOT NULL,
        TipoArchivo NVARCHAR(50),
        FechaCarga DATETIME DEFAULT GETDATE()
    )
    PRINT 'Tabla archivos_consumos creada exitosamente'
END
ELSE
BEGIN
    PRINT 'Tabla archivos_consumos ya existe'
END

-- =====================================================
-- VERIFICAR RESULTADO
-- =====================================================
SELECT 'LISTO - Tabla archivos_consumos creada correctamente' AS Resultado

-- Ver estructura de la tabla
SELECT
    c.name AS Columna,
    t.name AS Tipo,
    c.max_length AS Tamano,
    c.is_nullable AS Nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('[lightcon_lumin].[archivos_consumos]')
ORDER BY c.column_id
