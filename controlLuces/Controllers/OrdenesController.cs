using controlLuces.Models;
using controlLuces.permisos;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Font = iTextSharp.text.Font;
using ClosedXML.Excel;

namespace controlLuces.Controllers
{
    public class OrdenesController : Controller
    {
        SqlConnection con = new SqlConnection();
        SqlCommand com = new SqlCommand();
        SqlDataReader dr;

        void connectionString()
        {
            con.ConnectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
        }

        // ===== Helpers de sesión / municipio =====
        private bool EsAdminLocal()
        {
            return (Session["EsAdminLocal"] as bool?) ?? false;
        }

        private string GetMunicipioSesion()
        {
            var m = Session["Municipio"] as string;
            if (!string.IsNullOrWhiteSpace(m)) return m;

            var u = Session["Usuario"] as UsuarioModel;
            return u?.Municipio;
        }

        private static string PrefixDesdeMunicipio(string municipio)
        {
            var year = DateTime.Now.Year;
            if (string.IsNullOrWhiteSpace(municipio)) return $"ODSLC{year}";

            var k = municipio.Trim().ToLowerInvariant();
            if (k.Contains("madrid")) return $"ODSMA{year}";
            if (k.Contains("chía") || k.Contains("chia")) return $"ODSCH{year}";
            return $"ODSLC{year}";
        }

        // ====== MAPPER REUTILIZABLE PARA ORDEN ======
        private OrdenModel MapOrden(IDataRecord rd)
        {
            return new OrdenModel
            {
                IdOrden = rd["id_orden"] != DBNull.Value ? Convert.ToInt32(rd["id_orden"]) : 0,
                TipoDeElemento = rd["Tipo_de_elemento"] == DBNull.Value ? "" : rd["Tipo_de_elemento"].ToString(),
                CodigoDeElemento = rd["codigo_de_elemento"] == DBNull.Value ? "" : rd["codigo_de_elemento"].ToString(),
                ElementoRelacionado = rd["elemento_relacionado"] == DBNull.Value ? "" : rd["elemento_relacionado"].ToString(),
                CodigoOrden = rd["codigo_orden"] == DBNull.Value ? "" : rd["codigo_orden"].ToString(),
                ProblemaRelacionado = rd["problema_relacionado"] == DBNull.Value ? "" : rd["problema_relacionado"].ToString(),
                ProblemaValidado = rd["problema_validado"] == DBNull.Value ? "" : rd["problema_validado"].ToString(),
                OrdenPrioridad = rd["Orden_prioridad"] == DBNull.Value ? "" : rd["Orden_prioridad"].ToString(),
                PrioridadDeRuta = rd["prioridad_de_ruta"] == DBNull.Value ? 0 : Convert.ToInt32(rd["prioridad_de_ruta"]),
                FechaARealizar = rd["fecha_a_realizar"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["fecha_a_realizar"]),
                Cuadrilla = rd["cuadrilla"] == DBNull.Value ? "" : rd["cuadrilla"].ToString(),
                TipoDeOrden = rd["tipo_de_orden"] == DBNull.Value ? "" : rd["tipo_de_orden"].ToString(),
                TipoDeSolucion = rd["tipo_de_Solucion"] == DBNull.Value ? "" : rd["tipo_de_Solucion"].ToString(),
                ClaseDeOrden = rd["clase_de_orden"] == DBNull.Value ? "" : rd["clase_de_orden"].ToString(),
                ObraRelacionada = rd["obra_relacionada"] == DBNull.Value ? "" : rd["obra_relacionada"].ToString(),
                IdEstado = rd["IdEstado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["IdEstado"]),
                EstadoNombre = rd["EstadoNombre"] == DBNull.Value ? "" : rd["EstadoNombre"].ToString(),
                observaciones = rd["observaciones"] == DBNull.Value ? "" : rd["observaciones"].ToString(),
                Trabajos = rd["Trabajos"] == DBNull.Value ? "" : rd["Trabajos"].ToString(),
                Imagenes = new List<ImagenOrdenServicioModel>()
            };
        }

        // =========================================================
        // VISTAS BÁSICAS
        // =========================================================

        public ActionResult monitorear()
        {
            // la vista espera List<OrdenModel>, aquí la cargamos vacía
            return View(new List<OrdenModel>());
        }

        // ================== VER INFO (DETALLE ORDEN) ==================
        public ActionResult verinfo(int id)
        {
            var orden = new OrdenModel
            {
                Imagenes = new List<ImagenOrdenServicioModel>()
            };

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            {
                cn.Open();

                // Datos de la orden + nombre del estado
                using (var cmd = new SqlCommand(@"
            SELECT o.*, e.Nombre AS EstadoNombre
            FROM ordenes_de_servicio o
            LEFT JOIN Estado e ON e.IdEstado = o.IdEstado
            WHERE o.id_orden = @Id;", cn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            orden = MapOrden(rd);
                        }
                    }
                }

                // === NUEVO: traer código de la PQRS relacionada (CodigoPqrs) ===
                int idPqrs;
                if (!string.IsNullOrWhiteSpace(orden.ElementoRelacionado) &&
                    int.TryParse(orden.ElementoRelacionado, out idPqrs))
                {
                    using (var cmdP = new SqlCommand(
                        "SELECT CodigoPqrs FROM dbo.pqrs WHERE Idpqrs = @id", cn))
                    {
                        cmdP.Parameters.AddWithValue("@id", idPqrs);
                        var result = cmdP.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            ViewBag.CodigoPqrs = Convert.ToString(result);
                        }
                    }
                }

                // Imágenes asociadas
                using (var cmd = new SqlCommand(@"
            SELECT id_imagen, id_orden, Id_Orden_Cerrada, imagen, fecha_subida
            FROM ImagenesOrdenesDeServicio
            WHERE id_orden = @Id
            ORDER BY fecha_subida DESC;", cn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            orden.Imagenes.Add(new ImagenOrdenServicioModel
                            {
                                IdImagen = rd["id_imagen"] != DBNull.Value ? Convert.ToInt32(rd["id_imagen"]) : 0,
                                IdOrden = rd["id_orden"] != DBNull.Value ? Convert.ToInt32(rd["id_orden"]) : 0,
                                Id_Orden_Cerrada = rd["Id_Orden_Cerrada"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["Id_Orden_Cerrada"]),
                                Imagen = rd["imagen"] == DBNull.Value ? null : (byte[])rd["imagen"],
                                FechaSubida = rd["fecha_subida"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_subida"])
                            });
                        }
                    }
                }
            }

            return View(orden);
        }



        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult verOrdenes()
        {
            bool esAdminLocal = EsAdminLocal();
            string municipio = GetMunicipioSesion();

            var ordenesList = new List<OrdenModel>();
            var pqrsList = new List<PqrsModel>();

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cn.Open();

                // ========== TRAER ÓRDENES ACTIVAS ==========
                string sql = @"
            SELECT
                o.*,
                e.Nombre AS EstadoNombre,

                CASE
                    WHEN o.codigo_orden IS NULL THEN ''
                    WHEN CHARINDEX(':', o.codigo_orden) > 0
                        THEN LEFT(o.codigo_orden, CHARINDEX(':', o.codigo_orden) - 1)
                    WHEN CHARINDEX('-', o.codigo_orden) > 0
                        THEN LEFT(o.codigo_orden, CHARINDEX('-', o.codigo_orden) - 1)
                    ELSE o.codigo_orden
                END AS PrefijoCodigo,

                TRY_CONVERT(int,
                    CASE
                        WHEN o.codigo_orden IS NULL THEN '0'
                        WHEN CHARINDEX(':', o.codigo_orden) > 0
                            THEN RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX(':', o.codigo_orden))
                        WHEN CHARINDEX('-', o.codigo_orden) > 0
                            THEN RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX('-', o.codigo_orden))
                        ELSE '0'
                    END
                ) AS SufijoCodigo
            FROM ordenes_de_servicio o
            LEFT JOIN Estado e ON e.IdEstado = o.IdEstado
            WHERE 1 = 1
              AND e.Nombre IN ('ACTIVA', 'EN PROCESO')";

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sql += " AND o.Municipio = @M";
                    cmd.Parameters.AddWithValue("@M", municipio);
                }

                sql += @"
            ORDER BY
                PrefijoCodigo,
                SufijoCodigo";

                cmd.CommandText = sql;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        ordenesList.Add(MapOrden(rd));
                    }
                }

                // ========== TRAER PQRS (ESTADO 1 Y 2) ==========
                cmd.Parameters.Clear();
                string sqlPqrs = "SELECT * FROM dbo.pqrs WHERE Estado IN (1, 2)";

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sqlPqrs += " AND Municipio = @M2";
                    cmd.Parameters.AddWithValue("@M2", municipio);
                }

                sqlPqrs += " ORDER BY Idpqrs ASC";

                cmd.CommandText = sqlPqrs;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        pqrsList.Add(new PqrsModel
                        {
                            Idpqrs = rd["Idpqrs"] != DBNull.Value ? Convert.ToInt32(rd["Idpqrs"]) : 0,
                            FechaRegistro = rd["FechaRegistro"] == DBNull.Value ? "" : rd["FechaRegistro"].ToString(),
                            Tipopqrs = rd["Tipopqrs"] == DBNull.Value ? "" : rd["Tipopqrs"].ToString(),
                            Canal = rd["Canal"] == DBNull.Value ? "" : rd["Canal"].ToString(),
                            Nombre = rd["Nombre"] == DBNull.Value ? "" : rd["Nombre"].ToString(),
                            Apellido = rd["Apellido"] == DBNull.Value ? "" : rd["Apellido"].ToString(),
                            TipoDoc = rd["TipoDoc"] == DBNull.Value ? "" : rd["TipoDoc"].ToString(),
                            Documento = rd["Documento"] == DBNull.Value ? "" : rd["Documento"].ToString(),
                            Correo = rd["Correo"] == DBNull.Value ? "" : rd["Correo"].ToString(),
                            Referencia = rd["Referencia"] == DBNull.Value ? "" : rd["Referencia"].ToString(),
                            DireccionAfectacion = rd["DireccionAfectacion"] == DBNull.Value ? "" : rd["DireccionAfectacion"].ToString(),
                            BarrioAfectacion = rd["BarrioAfectacion"] == DBNull.Value ? "" : rd["BarrioAfectacion"].ToString(),
                            TipoAlumbrado = rd["TipoAlumbrado"] == DBNull.Value ? "" : rd["TipoAlumbrado"].ToString(),
                            DescripcionAfectacion = rd["DescripcionAfectacion"] == DBNull.Value ? "" : rd["DescripcionAfectacion"].ToString(),
                            Estado = rd["Estado"] != DBNull.Value ? Convert.ToInt32(rd["Estado"]) : 0,
                            EstadoNombre = rd["Estado"] != DBNull.Value && Convert.ToInt32(rd["Estado"]) == 1 ? "Sin asignar" :
                                          rd["Estado"] != DBNull.Value && Convert.ToInt32(rd["Estado"]) == 2 ? "En proceso" : "Desconocido"
                        });
                    }
                }
            }

            ViewBag.EsAdminLocal = esAdminLocal;
            ViewBag.PqrsList = pqrsList; // ✅ Pasar las PQRS a la vista
            ViewBag.Title = "Órdenes de Servicio";
            return View(ordenesList);
        }



        // ================== PDF ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult DescargarOrdenesPDF()
        {
            bool esAdminLocal = EsAdminLocal();
            string municipio = GetMunicipioSesion();
            var OrdenesList = new List<OrdenModel>();

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cn.Open();

                string sql = @"
                    SELECT o.*, e.Nombre AS EstadoNombre
                    FROM ordenes_de_servicio o
                    LEFT JOIN Estado e ON e.IdEstado = o.IdEstado";

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sql += " WHERE o.Municipio = @M";
                    cmd.Parameters.AddWithValue("@M", municipio);
                }

                cmd.CommandText = sql;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        OrdenesList.Add(MapOrden(rd));
                    }
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                var title = new Paragraph("Listado de Órdenes de Servicio",
                    new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD));
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1, 2, 3, 2, 2, 2 });

                table.AddCell("Consecutivo");
                table.AddCell("Fecha");
                table.AddCell("Descripción");
                table.AddCell("Pqrs Relacionada");
                table.AddCell("Cuadrilla");
                table.AddCell("Prioridad");

                foreach (var orden in OrdenesList)
                {
                    string consecutivoMostrar = !string.IsNullOrWhiteSpace(orden.CodigoOrden)
                        ? orden.CodigoOrden.Replace(":", "-")
                        : orden.IdOrden.ToString();

                    table.AddCell(consecutivoMostrar);
                    table.AddCell(orden.FechaARealizar.ToString("dd/MM/yyyy"));
                    table.AddCell(orden.ProblemaRelacionado);
                    table.AddCell(orden.ElementoRelacionado);
                    table.AddCell(orden.Cuadrilla);
                    table.AddCell(orden.OrdenPrioridad);
                }

                document.Add(table);
                document.Close();
                writer.Close();

                byte[] fileBytes = memoryStream.ToArray();
                return File(fileBytes, "application/pdf", "OrdenesDeServicio.pdf");
            }
        }

        // ================== EXCEL ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult DescargarOrdenesExcel()
        {
            bool esAdminLocal = EsAdminLocal();
            string municipio = GetMunicipioSesion();
            var usuario = Session["Usuario"] as UsuarioModel;
            var muni = (Session["Municipio"] as string) ?? usuario?.Municipio;
            var k = (muni ?? "").Trim().ToLower().Replace("í", "i");
            var prefijo = k == "madrid" ? "MA" : k == "chia" ? "CH" : "CH";
            var year = DateTime.Now.Year;

            var ordenesList = new List<OrdenModel>();
            var pqrsList = new List<PqrsModel>();

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cn.Open();

                // ========== TRAER ÓRDENES ==========
                string sql = @"
                SELECT o.*, e.Nombre AS EstadoNombre,
                CASE
                    WHEN o.codigo_orden IS NULL THEN NULL
                    WHEN CHARINDEX(':', o.codigo_orden) > 0
                        THEN LEFT(o.codigo_orden, CHARINDEX(':', o.codigo_orden) - 1)
                    WHEN CHARINDEX('-', o.codigo_orden) > 0
                        THEN LEFT(o.codigo_orden, CHARINDEX('-', o.codigo_orden) - 1)
                    ELSE o.codigo_orden
                END AS PrefijoCodigo,

                TRY_CONVERT(int,
                    CASE
                        WHEN o.codigo_orden IS NULL THEN '0'
                        WHEN CHARINDEX(':', o.codigo_orden) > 0
                            THEN RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX(':', o.codigo_orden))
                        WHEN CHARINDEX('-', o.codigo_orden) > 0
                            THEN RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX('-', o.codigo_orden))
                        ELSE '0'
                    END
                ) AS SufijoCodigo
            FROM ordenes_de_servicio o
            LEFT JOIN Estado e ON e.IdEstado = o.IdEstado
            WHERE 1 = 1
              AND e.Nombre IN ('ACTIVA', 'EN PROCESO')";

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sql += " AND o.Municipio = @M";
                    cmd.Parameters.AddWithValue("@M", municipio);
                }

                sql += @"
            ORDER BY
                PrefijoCodigo,
                SufijoCodigo";

                cmd.CommandText = sql;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        ordenesList.Add(MapOrden(rd));
                    }
                }

                // ========== TRAER PQRS (ESTADO 1 Y 2) ==========
                cmd.Parameters.Clear();
                string sqlPqrs = "SELECT * FROM dbo.pqrs WHERE Estado IN (1, 2)";

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sqlPqrs += " AND Municipio = @M2";
                    cmd.Parameters.AddWithValue("@M2", municipio);
                }

                sqlPqrs += " ORDER BY Idpqrs ASC";

                cmd.CommandText = sqlPqrs;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        pqrsList.Add(new PqrsModel
                        {
                            Idpqrs = rd["Idpqrs"] != DBNull.Value ? Convert.ToInt32(rd["Idpqrs"]) : 0,
                            FechaRegistro = rd["FechaRegistro"] == DBNull.Value ? "" : rd["FechaRegistro"].ToString(),
                            Tipopqrs = rd["Tipopqrs"] == DBNull.Value ? "" : rd["Tipopqrs"].ToString(),
                            Canal = rd["Canal"] == DBNull.Value ? "" : rd["Canal"].ToString(),
                            Nombre = rd["Nombre"] == DBNull.Value ? "" : rd["Nombre"].ToString(),
                            Apellido = rd["Apellido"] == DBNull.Value ? "" : rd["Apellido"].ToString(),
                            DireccionAfectacion = rd["DireccionAfectacion"] == DBNull.Value ? "" : rd["DireccionAfectacion"].ToString(),
                            DescripcionAfectacion = rd["DescripcionAfectacion"] == DBNull.Value ? "" : rd["DescripcionAfectacion"].ToString(),
                            Estado = rd["Estado"] != DBNull.Value ? Convert.ToInt32(rd["Estado"]) : 0,
                            EstadoNombre = rd["Estado"] != DBNull.Value && Convert.ToInt32(rd["Estado"]) == 1 ? "Sin asignar" :
                                          rd["Estado"] != DBNull.Value && Convert.ToInt32(rd["Estado"]) == 2 ? "En proceso" : "Desconocido"
                        });
                    }
                }
            }

            // GENERAR EXCEL CON CLOSEDXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Órdenes y PQRS");

                // ENCABEZADOS
                worksheet.Cell(1, 1).Value = "Tipo";
                worksheet.Cell(1, 2).Value = "Código";
                worksheet.Cell(1, 3).Value = "Estado";
                worksheet.Cell(1, 4).Value = "Fecha";
                worksheet.Cell(1, 5).Value = "Descripción";
                worksheet.Cell(1, 6).Value = "PQRS/Problema Relacionado";
                worksheet.Cell(1, 7).Value = "Cuadrilla/Canal";
                worksheet.Cell(1, 8).Value = "Dirección";

                // ESTILO ENCABEZADOS
                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontSize = 11;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(59, 130, 246);
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // DATOS
                int fila = 2;

                // Agregar Órdenes
                foreach (var orden in ordenesList)
                {
                    var codigoOrden = string.IsNullOrWhiteSpace(orden.CodigoOrden)
                        ? (prefijo + year + "-" + orden.IdOrden)
                        : orden.CodigoOrden;

                    worksheet.Cell(fila, 1).Value = "Orden";
                    worksheet.Cell(fila, 2).Value = codigoOrden;
                    worksheet.Cell(fila, 3).Value = orden.EstadoNombre;
                    worksheet.Cell(fila, 4).Value = orden.FechaARealizar.ToString("dd/MM/yyyy");
                    worksheet.Cell(fila, 5).Value = orden.ProblemaRelacionado;
                    worksheet.Cell(fila, 6).Value = orden.ElementoRelacionado;
                    worksheet.Cell(fila, 7).Value = orden.Cuadrilla;
                    worksheet.Cell(fila, 8).Value = "";

                    // Alternar colores
                    if (fila % 2 == 0)
                    {
                        worksheet.Range(fila, 1, fila, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
                    }

                    fila++;
                }

                // Agregar PQRS
                foreach (var pqrs in pqrsList)
                {
                    var codigoPqrs = prefijo + year + ":" + pqrs.Idpqrs;

                    worksheet.Cell(fila, 1).Value = "PQRS";
                    worksheet.Cell(fila, 2).Value = codigoPqrs;
                    worksheet.Cell(fila, 3).Value = pqrs.EstadoNombre;
                    worksheet.Cell(fila, 4).Value = pqrs.FechaRegistro;
                    worksheet.Cell(fila, 5).Value = pqrs.DescripcionAfectacion;
                    worksheet.Cell(fila, 6).Value = pqrs.Tipopqrs;
                    worksheet.Cell(fila, 7).Value = pqrs.Canal;
                    worksheet.Cell(fila, 8).Value = pqrs.DireccionAfectacion;

                    // Alternar colores
                    if (fila % 2 == 0)
                    {
                        worksheet.Range(fila, 1, fila, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
                    }

                    fila++;
                }

                // Ajustar columnas
                worksheet.Columns().AdjustToContents();

                // Congelar primera fila
                worksheet.SheetView.FreezeRows(1);

                // GUARDAR Y DESCARGAR
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var bytes = stream.ToArray();
                    var fecha = DateTime.Now;
                    var nombreArchivo = $"Ordenes_PQRS_Activas_{fecha:yyyyMMdd_HHmmss}.xlsx";

                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                }
            }
        }

        // ================== CREAR ORDEN (MANTENIMIENTO) - CORREGIDO ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult insertarOrdenMan(OrdenModel orden)
        {
            orden.ClaseDeOrden = "Mantenimiento";
            string municipio = GetMunicipioSesion();

            try
            {
                connectionString();
                con.Open();
                com.Connection = con;

                DateTime fechaUTC = orden.FechaARealizar == DateTime.MinValue
                    ? DateTime.UtcNow
                    : orden.FechaARealizar.ToUniversalTime();

                string query = @"
INSERT INTO ordenes_de_servicio 
(Tipo_de_elemento, codigo_de_elemento, elemento_relacionado, 
 problema_relacionado, problema_validado, prioridad_de_ruta, Orden_prioridad,
 fecha_a_realizar, cuadrilla, tipo_de_orden, tipo_de_Solucion, clase_de_orden, IdEstado, Municipio)
VALUES
(@TipoDeElemento, @CodigoDeElemento, @ElementoRelacionado,
 @ProblemaRelacionado, @ProblemaValidado, @PrioridadDeRuta, @OrdenPrioridad,
 @FechaARealizar, @Cuadrilla, @TipoDeOrden, @TipoDeSolucion, @ClaseDeOrden, @Idestado, @Municipio);
SELECT CAST(SCOPE_IDENTITY() AS int);";

                com.CommandText = query;
                com.Parameters.Clear();
                com.Parameters.AddWithValue("@TipoDeElemento", (object)orden.TipoDeElemento ?? DBNull.Value);
                com.Parameters.AddWithValue("@CodigoDeElemento", (object)orden.CodigoDeElemento ?? DBNull.Value);
                com.Parameters.AddWithValue("@ElementoRelacionado", (object)orden.ElementoRelacionado ?? DBNull.Value);
                com.Parameters.AddWithValue("@ProblemaRelacionado", (object)orden.ProblemaRelacionado ?? DBNull.Value);
                com.Parameters.AddWithValue("@ProblemaValidado", (object)orden.ProblemaValidado ?? DBNull.Value);
                com.Parameters.AddWithValue("@OrdenPrioridad", (object)orden.OrdenPrioridad ?? DBNull.Value);
                com.Parameters.AddWithValue("@PrioridadDeRuta", orden.PrioridadDeRuta);
                com.Parameters.AddWithValue("@FechaARealizar", fechaUTC);
                com.Parameters.AddWithValue("@Cuadrilla", (object)orden.Cuadrilla ?? DBNull.Value);
                com.Parameters.AddWithValue("@TipoDeOrden", (object)orden.TipoDeOrden ?? DBNull.Value);
                com.Parameters.AddWithValue("@TipoDeSolucion", (object)orden.TipoDeSolucion ?? DBNull.Value);
                com.Parameters.AddWithValue("@ClaseDeOrden", (object)orden.ClaseDeOrden ?? DBNull.Value);
                com.Parameters.AddWithValue("@Idestado", 2);
                com.Parameters.AddWithValue("@Municipio", (object)(municipio ?? (string)null) ?? DBNull.Value);

                int ordenId = Convert.ToInt32(com.ExecuteScalar());

                // ✅ CALCULAR CONSECUTIVO POR MUNICIPIO
                var pref = PrefixDesdeMunicipio(municipio);

                com.Parameters.Clear();
                com.CommandText = @"
            SELECT COUNT(*) 
            FROM ordenes_de_servicio 
            WHERE Municipio = @Muni OR (@Muni IS NULL AND Municipio IS NULL)";
                com.Parameters.AddWithValue("@Muni", (object)municipio ?? DBNull.Value);
                int consecutivo = Convert.ToInt32(com.ExecuteScalar());

                var codigoOrden = $"{pref}-{consecutivo}";

                // Guardar código de orden
                com.Parameters.Clear();
                com.CommandText = "UPDATE ordenes_de_servicio SET codigo_orden = @Codigo WHERE id_orden = @Id";
                com.Parameters.AddWithValue("@Codigo", codigoOrden);
                com.Parameters.AddWithValue("@Id", ordenId);
                com.ExecuteNonQuery();

                // Actualizar estado de la PQRS
                com.Parameters.Clear();
                com.CommandText = "UPDATE pqrs SET Estado = @NuevoEstado WHERE Idpqrs = @idpqrs";
                com.Parameters.AddWithValue("@NuevoEstado", 2);
                com.Parameters.AddWithValue("@idpqrs", orden.ElementoRelacionado);
                int rowsUpdated = com.ExecuteNonQuery();

                if (rowsUpdated > 0)
                {
                    return Json(new
                    {
                        success = true,
                        ordenId = ordenId,
                        consecutivo = codigoOrden
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Error al actualizar el estado en PQRS." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al crear la orden de servicio: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }


        // ================== CREAR ORDEN (MONTAJE / EXPANSIÓN) - CORREGIDO ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult insertarOrdenMon(OrdenModel orden)
        {
            orden.ClaseDeOrden = "Montaje";
            string municipio = GetMunicipioSesion();

            try
            {
                connectionString();
                con.Open();
                com.Connection = con;

                string query = @"
INSERT INTO ordenes_de_servicio 
(Tipo_de_elemento, codigo_de_elemento, elemento_relacionado, 
 problema_relacionado, problema_validado, prioridad_de_ruta, Orden_prioridad,
 fecha_a_realizar, cuadrilla, obra_relacionada, clase_de_orden, IdEstado, Municipio) 
VALUES 
(@TipoDeElemento, @CodigoDeElemento, @ElementoRelacionado, 
 @ProblemaRelacionado, @ProblemaValidado, @PrioridadDeRuta, @OrdenPrioridad,
 @FechaARealizar, @Cuadrilla, @ObraRelacionada, @ClaseDeOrden, @IdEstado, @Municipio);
SELECT CAST(SCOPE_IDENTITY() AS int);";

                com.CommandText = query;
                com.Parameters.Clear();
                com.Parameters.AddWithValue("@TipoDeElemento", (object)orden.TipoDeElemento ?? DBNull.Value);
                com.Parameters.AddWithValue("@CodigoDeElemento", (object)orden.CodigoDeElemento ?? DBNull.Value);
                com.Parameters.AddWithValue("@ElementoRelacionado", (object)orden.ElementoRelacionado ?? DBNull.Value);
                com.Parameters.AddWithValue("@ProblemaRelacionado", (object)orden.ProblemaRelacionado ?? DBNull.Value);
                com.Parameters.AddWithValue("@ProblemaValidado", (object)orden.ProblemaValidado ?? DBNull.Value);
                com.Parameters.AddWithValue("@OrdenPrioridad", (object)orden.OrdenPrioridad ?? DBNull.Value);
                com.Parameters.AddWithValue("@PrioridadDeRuta", orden.PrioridadDeRuta == 0 ? (object)DBNull.Value : orden.PrioridadDeRuta);
                com.Parameters.AddWithValue("@FechaARealizar", orden.FechaARealizar == DateTime.MinValue ? (object)DateTime.UtcNow : orden.FechaARealizar);
                com.Parameters.AddWithValue("@Cuadrilla", (object)orden.Cuadrilla ?? DBNull.Value);
                com.Parameters.AddWithValue("@ObraRelacionada", (object)orden.ObraRelacionada ?? DBNull.Value);
                com.Parameters.AddWithValue("@ClaseDeOrden", (object)orden.ClaseDeOrden ?? DBNull.Value);
                com.Parameters.AddWithValue("@IdEstado", 2);
                com.Parameters.AddWithValue("@Municipio", (object)(municipio ?? (string)null) ?? DBNull.Value);

                int newOrderId = (int)com.ExecuteScalar();

                if (newOrderId > 0)
                {
                    // ✅ CALCULAR CONSECUTIVO POR MUNICIPIO
                    var pref = PrefixDesdeMunicipio(municipio);

                    com.Parameters.Clear();
                    com.CommandText = @"
                SELECT COUNT(*) 
                FROM ordenes_de_servicio 
                WHERE Municipio = @Muni OR (@Muni IS NULL AND Municipio IS NULL)";
                    com.Parameters.AddWithValue("@Muni", (object)municipio ?? DBNull.Value);
                    int consecutivo = Convert.ToInt32(com.ExecuteScalar());

                    var codigoOrden = $"{pref}-{consecutivo}";

                    // Guardar código de orden
                    com.Parameters.Clear();
                    com.CommandText = "UPDATE ordenes_de_servicio SET codigo_orden = @Codigo WHERE id_orden = @Id";
                    com.Parameters.AddWithValue("@Codigo", codigoOrden);
                    com.Parameters.AddWithValue("@Id", newOrderId);
                    com.ExecuteNonQuery();

                    // Actualizar estado PQRS
                    com.Parameters.Clear();
                    com.CommandText = "UPDATE pqrs SET Estado = @NuevoEstado WHERE Idpqrs = @idpqrs";
                    com.Parameters.AddWithValue("@NuevoEstado", 2);
                    com.Parameters.AddWithValue("@idpqrs", orden.ElementoRelacionado);
                    com.ExecuteNonQuery();

                    return Json(new { success = true, ordenId = newOrderId, consecutivo = codigoOrden });
                }
                else
                {
                    return Json(new { success = false, message = "Error al crear la orden de servicio" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al crear la orden de servicio: " + ex.Message });
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }
        public ActionResult generar()
        {
            return View();
        }

        // ====== TRAE REFERENCIA DE PQRS PARA PRECARGAR CÓDIGO DE ELEMENTO ======
        // ====== AQUÍ MODIFIQUÉ PARA TRAER TAMBIÉN CodigoPqrs (CH2025:8) ======
        public ActionResult CrearOrdenDeServicio(int idPqrs, string descripcionAfectacion)
        {
            ViewBag.IdPqrs = idPqrs;
            ViewBag.DescripcionAfectacion = descripcionAfectacion;
            ViewBag.descripcionAfectacion = descripcionAfectacion;

            string codigoElemento = null;
            string codigoPqrs = null;

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand(
                "SELECT Referencia, CodigoPqrs FROM dbo.pqrs WHERE Idpqrs=@id", cn))
            {
                cmd.Parameters.AddWithValue("@id", idPqrs);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        if (rd["Referencia"] != DBNull.Value)
                            codigoElemento = rd["Referencia"].ToString();

                        if (rd["CodigoPqrs"] != DBNull.Value)
                            codigoPqrs = rd["CodigoPqrs"].ToString();
                    }
                }
            }

            ViewBag.CodigoElemento = codigoElemento ?? string.Empty;
            ViewBag.CodigoPqrs = codigoPqrs ?? string.Empty;   // <-- NUEVO

            return View();
        }

        // ================== ELIMINAR ORDEN ==================
        public ActionResult eliminarOrden(int id, int idpqrs)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "DELETE FROM ordenes_de_servicio WHERE id_orden = @Idorden";
            com.Parameters.AddWithValue("@Idorden", id);

            try
            {
                int rowsAffected = com.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    com.Parameters.Clear();
                    com.CommandText = "UPDATE pqrs SET Estado = @NuevoEstado WHERE Idpqrs = @idpqrs";
                    com.Parameters.AddWithValue("@NuevoEstado", 1);
                    com.Parameters.AddWithValue("@idpqrs", idpqrs);
                    com.ExecuteNonQuery();

                    return Json(new { success = true, message = "Orden eliminada correctamente." }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "No se encontró ninguna Orden." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al intentar eliminar la orden: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        // ================== VERIFICAR CÓDIGO DE ELEMENTO ==================
        [HttpPost]
        public ActionResult verificarElemento(float codigo)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "select 1 from infraestructura donde codigo = @codigo";
            com.Parameters.AddWithValue("@codigo", codigo);

            var exists = com.ExecuteScalar();
            con.Close();
            return Json(new { success = exists != null, message = exists != null ? "El elemento existe." : "El elemento no existe." });
        }

        // ================== CUADRILLAS PARA LOS FORMULARIOS ==================
        public ActionResult ObtenerCuadrilla()
        {
            List<CuadrillaModel> cuadrillas = new List<CuadrillaModel>();

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "SELECT * FROM Cuadrillas";

            using (dr = com.ExecuteReader())
            {
                while (dr.Read())
                {
                    cuadrillas.Add(new CuadrillaModel
                    {
                        Id_Cuadrilla = Convert.ToInt32(dr["id_cuadrilla"]),
                        Nombre = dr["Nombre"].ToString(),
                        Municipio = dr["Municipio"].ToString()
                    });
                }
            }

            con.Close();

            return ListarCuadrillas();
        }

        // ================== BUSCADOR ==================
        [HttpPost]
        public ActionResult BuscarOrden(string tipoBusqueda, string desde, string hasta, string IdOrden)
        {
            bool esAdminLocal = EsAdminLocal();
            string municipio = GetMunicipioSesion();
            var ordenes = new List<OrdenModel>();

            connectionString();
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cn.Open();

                string sql = @"
            SELECT o.*, e.Nombre AS EstadoNombre
            FROM ordenes_de_servicio o
            LEFT JOIN Estado e ON e.IdEstado = o.IdEstado
            WHERE 1 = 1";

                if (tipoBusqueda == "consecutivo" && !string.IsNullOrWhiteSpace(IdOrden))
                {
                    int idInt;
                    if (int.TryParse(IdOrden, out idInt))
                    {
                        // Número solo: busca por id_orden y por sufijo en código (después de '-' o ':')
                        sql += @"
                    AND (
                        o.id_orden = @IdOrden
                        OR
                        (
                            (CHARINDEX('-', o.codigo_orden) > 0 
                                AND RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX('-', o.codigo_orden)) = @Sufijo)
                            OR
                            (CHARINDEX(':', o.codigo_orden) > 0 
                                AND RIGHT(o.codigo_orden, LEN(o.codigo_orden) - CHARINDEX(':', o.codigo_orden)) = @Sufijo)
                        )
                    )";

                        cmd.Parameters.AddWithValue("@IdOrden", idInt);
                        cmd.Parameters.AddWithValue("@Sufijo", IdOrden.Trim());
                    }
                    else
                    {
                        // Texto: normaliza CH2025-9 a CH2025:9 para coincidir con la BD
                        string codigoNormalizado = IdOrden.Trim().Replace('-', ':');
                        sql += " AND o.codigo_orden = @CodigoOrden";
                        cmd.Parameters.AddWithValue("@CodigoOrden", codigoNormalizado);
                    }
                }
                else if (tipoBusqueda == "fecha" &&
                         !string.IsNullOrWhiteSpace(desde) &&
                         !string.IsNullOrWhiteSpace(hasta))
                {
                    sql += " AND o.fecha_a_realizar BETWEEN @Desde AND @Hasta";
                    cmd.Parameters.AddWithValue("@Desde", DateTime.Parse(desde));
                    cmd.Parameters.AddWithValue("@Hasta", DateTime.Parse(hasta));
                }

                if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
                {
                    sql += " AND o.Municipio = @M";
                    cmd.Parameters.AddWithValue("@M", municipio);
                }

                cmd.CommandText = sql;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        ordenes.Add(MapOrden(rd)); // NO toco tu mapeo
                    }
                }
            }

            ViewBag.Title = "Historial de Órdenes - Búsqueda";
            return View("monitorear", ordenes);
        }

        // ====== FORMULARIOS PARCIALES ======
        // ====== AQUÍ TAMBIÉN TRAIGO CodigoPqrs PARA LOS PARCIALES ======
        public ActionResult ObtenerFormulario(string opcion, string idPqrs, string descripcionAfectacion)
        {
            // Dejas tu llamada si la usas para popular algo visual
            ObtenerCuadrilla();

            // Precargar Código del elemento desde Referencia de PQRS
            string codigoElemento = string.Empty;
            string codigoPqrs = string.Empty;

            if (!string.IsNullOrWhiteSpace(idPqrs) && int.TryParse(idPqrs, out var idPqrsInt))
            {
                connectionString();
                using (var cn = new SqlConnection(con.ConnectionString))
                using (var cmd = new SqlCommand(
                    "SELECT Referencia, CodigoPqrs FROM dbo.pqrs WHERE Idpqrs=@id", cn))
                {
                    cmd.Parameters.AddWithValue("@id", idPqrsInt);
                    cn.Open();

                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            if (rd["Referencia"] != DBNull.Value)
                                codigoElemento = rd["Referencia"].ToString();

                            if (rd["CodigoPqrs"] != DBNull.Value)
                                codigoPqrs = rd["CodigoPqrs"].ToString();
                        }
                    }
                }
            }

            ViewBag.IdPqrs = idPqrs;
            ViewBag.descripcionAfectacion = descripcionAfectacion;
            ViewBag.CodigoElemento = codigoElemento ?? string.Empty;
            ViewBag.CodigoPqrs = codigoPqrs ?? string.Empty;   // <-- NUEVO

            switch (opcion)
            {
                case "mantenimiento":
                    return PartialView("_FormularioMantenimiento");
                case "expansion":
                    return PartialView("_FormularioMontaje");
                case "modernizacion":
                    return PartialView("_FormularioModernizacion");
                default:
                    return Content("Opción de formulario no válida");
            }
        }

        // ================== CUADRILLAS: CREAR ==================
        public ActionResult crearCuadrilla()
        {
            ViewBag.MostrarMensajeExito = false;
            return View();
        }

        [HttpPost]
        public ActionResult crearNuevaCuadrilla(CuadrillaModel cuadrilla)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.MostrarMensajeExito = false;
                return View("crearCuadrilla", cuadrilla);
            }

            try
            {
                connectionString();
                con.Open();
                com.Connection = con;
                com.Parameters.Clear();

                var municipioSesion = GetMunicipioSesion();
                if (EsAdminLocal() && !string.IsNullOrWhiteSpace(municipioSesion))
                    cuadrilla.Municipio = municipioSesion;

                com.CommandText = @"
            INSERT INTO Cuadrillas (Nombre, Municipio, Clave)
            VALUES (@Nombre, @Municipio, @Clave);
            SELECT CAST(SCOPE_IDENTITY() AS int);";
                com.Parameters.AddWithValue("@Nombre", (object)cuadrilla.Nombre ?? DBNull.Value);
                com.Parameters.AddWithValue("@Municipio", (object)cuadrilla.Municipio ?? DBNull.Value);
                com.Parameters.AddWithValue("@Clave", (object)cuadrilla.Clave ?? DBNull.Value);

                var newId = (int)com.ExecuteScalar();

                var year = DateTime.Now.Year;
                string prefijo;
                var k = (cuadrilla.Municipio ?? "").Trim().ToLowerInvariant();
                if (k.Contains("madrid")) prefijo = $"MA{year}";
                else if (k.Contains("chía") || k.Contains("chia")) prefijo = $"CH{year}";
                else prefijo = $"LC{year}";

                ViewBag.MostrarMensajeExito = true;
                ViewBag.PrefijoCreado = prefijo;

                return View("crearCuadrilla");
            }
            catch (Exception ex)
            {
                ViewBag.MostrarMensajeExito = false;
                ViewBag.ErrorMessage = "Error al crear la cuadrilla: " + ex.Message;
                return View("crearCuadrilla", cuadrilla);
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }
        }

        public ActionResult Graficas()
        {
            return View();
        }

        // ================== TRABAJOS REALIZADOS ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult VerTrabajosRealizados(int? idOrdenCerrada)
        {
            var lista = new List<TrabajoRealizadoModel>();

            try
            {
                connectionString();
                if (con.State != ConnectionState.Open) con.Open();
                com.Connection = con;
                com.Parameters.Clear();

                if (idOrdenCerrada.HasValue)
                {
                    com.CommandText = @"
                        SELECT IdTrabajoRealizado, IdOrdenCerrada, Descripcion, Detalle, Cantidad
                        FROM [dbo].[Trabajos_Realizados]
                        WHERE IdOrdenCerrada = @idOrdenCerrada
                        ORDER BY IdTrabajoRealizado";
                    com.Parameters.AddWithValue("@idOrdenCerrada", idOrdenCerrada.Value);
                }
                else
                {
                    com.CommandText = @"
                        SELECT IdTrabajoRealizado, IdOrdenCerrada, Descripcion, Detalle, Cantidad
                        FROM [dbo].[Trabajos_Realizados]
                        ORDER BY IdOrdenCerrada, IdTrabajoRealizado";
                }

                using (var rdr = com.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        lista.Add(new TrabajoRealizadoModel
                        {
                            IdTrabajoRealizado = rdr["IdTrabajoRealizado"] != DBNull.Value ? Convert.ToInt32(rdr["IdTrabajoRealizado"]) : 0,
                            IdOrdenCerrada = rdr["IdOrdenCerrada"] != DBNull.Value ? Convert.ToInt32(rdr["IdOrdenCerrada"]) : 0,
                            Descripcion = rdr["Descripcion"] == DBNull.Value ? "" : rdr["Descripcion"].ToString(),
                            Detalle = rdr["Detalle"] == DBNull.Value ? "" : rdr["Detalle"].ToString(),
                            Cantidad = rdr["Cantidad"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["Cantidad"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al consultar Trabajos_Realizados: " + ex.Message;
            }
            finally
            {
                com.Parameters.Clear();
                if (con.State == ConnectionState.Open) con.Close();
            }

            ViewBag.IdOrdenCerradaSeleccionada = idOrdenCerrada;
            return View(lista);
        }

        // ================== CUADRILLAS: LISTAR + CONTAR ÓRDENES ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult ListarCuadrillas(string mun = null, string q = null)
        {
            bool esAdminLocal = EsAdminLocal();
            string municipioSesion = GetMunicipioSesion();

            string municipioFiltro = esAdminLocal ? municipioSesion : (string.IsNullOrWhiteSpace(mun) ? municipioSesion : mun);

            var lista = new List<CuadrillaModel>();

            try
            {
                connectionString();
                if (con.State != ConnectionState.Open) con.Open();
                com.Connection = con;
                com.Parameters.Clear();

                string sql = @"
            SELECT c.id_cuadrilla, c.Nombre, c.Municipio, c.Clave
            FROM dbo.Cuadrillas c
            WHERE (@Muni IS NULL OR c.Municipio = @Muni)
              AND (@Q IS NULL OR c.Nombre LIKE @Qlike OR c.Clave LIKE @Qlike)
            ORDER BY c.Municipio, c.Nombre;";

                com.CommandText = sql;
                com.Parameters.AddWithValue("@Muni", (object)municipioFiltro ?? DBNull.Value);
                com.Parameters.AddWithValue("@Q", string.IsNullOrWhiteSpace(q) ? (object)DBNull.Value : q);
                com.Parameters.AddWithValue("@Qlike", string.IsNullOrWhiteSpace(q) ? (object)DBNull.Value : $"%{q}%");

                using (var rdr = com.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        lista.Add(new CuadrillaModel
                        {
                            Id_Cuadrilla = Convert.ToInt32(rdr["id_cuadrilla"]),
                            Nombre = rdr["Nombre"].ToString(),
                            Municipio = rdr["Municipio"].ToString(),
                            Clave = rdr["Clave"].ToString()
                        });
                    }
                }

                foreach (var c in lista)
                {
                    using (var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM dbo.ordenes_de_servicio o
                WHERE o.cuadrilla = @N
                  AND (@M2 IS NULL OR o.Municipio = @M2);", con))
                    {
                        cmd.Parameters.AddWithValue("@N", c.Nombre ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@M2", (object)municipioFiltro ?? DBNull.Value);
                        c.OrdenesAsociadas = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al consultar cuadrillas: " + ex.Message;
            }
            finally
            {
                com.Parameters.Clear();
                if (con.State == ConnectionState.Open) con.Close();
            }

            ViewBag.MunicipioSeleccionado = municipioFiltro;
            ViewBag.Query = q;
            return View("Cuadrillas", lista);
        }

        // ================== DETALLE ORDEN CERRADA ==================
        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult DetalleOrdenCerrada(int id)
        {
            var vm = new controlLuces.Models.OrdenCerradaDetalleVM
            {
                Orden = new controlLuces.Models.OrdenModel(),
                Cierre = new controlLuces.Models.OrdenCerradaModel(),
                Trabajos = new List<controlLuces.Models.TrabajoRealizadoModel>(),
                Imagenes = new List<controlLuces.Models.ImagenOrdenServicioModel>()
            };

            connectionString();
            if (con.State != ConnectionState.Open) con.Open();

            try
            {
                using (var cmd = new SqlCommand(@"
                    SELECT o.*, e.Nombre AS EstadoNombre
                    FROM ordenes_de_servicio o
                    INNER JOIN Estado e ON e.IdEstado = o.IdEstado
                    WHERE o.id_orden = @id;", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            var o = vm.Orden;
                            o.IdOrden = rd["id_orden"] != DBNull.Value ? Convert.ToInt32(rd["id_orden"]) : 0;
                            o.CodigoOrden = rd["codigo_orden"] == DBNull.Value ? "" : rd["codigo_orden"].ToString();
                            o.TipoDeElemento = rd["Tipo_de_elemento"] == DBNull.Value ? "" : rd["Tipo_de_elemento"].ToString();
                            o.CodigoDeElemento = rd["codigo_de_elemento"] == DBNull.Value ? "" : rd["codigo_de_elemento"].ToString();
                            o.ElementoRelacionado = rd["elemento_relacionado"] == DBNull.Value ? "" : rd["elemento_relacionado"].ToString();
                            o.ProblemaRelacionado = rd["problema_relacionado"] == DBNull.Value ? "" : rd["problema_relacionado"].ToString();
                            o.ProblemaValidado = rd["problema_validado"] == DBNull.Value ? "" : rd["problema_validado"].ToString();
                            o.OrdenPrioridad = rd["Orden_prioridad"] == DBNull.Value ? "" : rd["Orden_prioridad"].ToString();
                            o.PrioridadDeRuta = rd["prioridad_de_ruta"] == DBNull.Value ? 0 : Convert.ToInt32(rd["prioridad_de_ruta"]);
                            o.FechaARealizar = rd["fecha_a_realizar"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["fecha_a_realizar"]);
                            o.Cuadrilla = rd["cuadrilla"] == DBNull.Value ? "" : rd["cuadrilla"].ToString();
                            o.TipoDeOrden = rd["tipo_de_orden"] == DBNull.Value ? "" : rd["tipo_de_orden"].ToString();
                            o.TipoDeSolucion = rd["tipo_de_Solucion"] == DBNull.Value ? "" : rd["tipo_de_Solucion"].ToString();
                            o.ClaseDeOrden = rd["clase_de_orden"] == DBNull.Value ? "" : rd["clase_de_orden"].ToString();
                            o.ObraRelacionada = rd["obra_relacionada"] == DBNull.Value ? "" : rd["obra_relacionada"].ToString();
                            o.IdEstado = rd["IdEstado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["IdEstado"]);
                            o.EstadoNombre = rd["EstadoNombre"] == DBNull.Value ? "" : rd["EstadoNombre"].ToString();
                            o.observaciones = rd["observaciones"] == DBNull.Value ? "" : rd["observaciones"].ToString();
                            o.Trabajos = rd["Trabajos"] == DBNull.Value ? "" : rd["Trabajos"].ToString();

                            if (int.TryParse(o.ElementoRelacionado, out var idPqrsParsed))
                                o.Idpqrs = idPqrsParsed;
                        }
                    }
                }

                int? idCierre = null;
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 Id_Orden_Cerrada, Id_Orden_Servicio, Observacion, Respuesta, MaterialesUsados
                    FROM Ordenes_Cerradas
                    WHERE Id_Orden_Servicio = @id
                    ORDER BY Id_Orden_Cerrada DESC;", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            var c = vm.Cierre;
                            c.Id_Orden_Cerrada = Convert.ToInt32(rd["Id_Orden_Cerrada"]);
                            c.Id_Orden_Servicio = Convert.ToInt32(rd["Id_Orden_Servicio"]);
                            c.Observacion = rd["Observacion"] == DBNull.Value ? null : rd["Observacion"].ToString();
                            c.Respuesta = rd["Respuesta"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["Respuesta"]);
                            c.MaterialesUsados = rd["MaterialesUsados"] == DBNull.Value ? null : rd["MaterialesUsados"].ToString();
                            idCierre = c.Id_Orden_Cerrada;
                        }
                    }
                }

                using (var cmd = new SqlCommand(@"
                    SELECT id_imagen, id_orden, Id_Orden_Cerrada, imagen, fecha_subida
                    FROM ImagenesOrdenesDeServicio
                    WHERE id_orden = @id
                    ORDER BY fecha_subida DESC;", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            vm.Imagenes.Add(new ImagenOrdenServicioModel
                            {
                                IdImagen = Convert.ToInt32(rd["id_imagen"]),
                                IdOrden = Convert.ToInt32(rd["id_orden"]),
                                Id_Orden_Cerrada = rd["Id_Orden_Cerrada"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["Id_Orden_Cerrada"]),
                                Imagen = rd["imagen"] == DBNull.Value ? null : (byte[])rd["imagen"],
                                FechaSubida = rd["fecha_subida"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_subida"])
                            });
                        }
                    }
                }

                if (idCierre.HasValue)
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT IdTrabajoRealizado, IdOrdenCerrada, Descripcion, Detalle, Cantidad
                        FROM Trabajos_Realizados
                        WHERE IdOrdenCerrada = @idc
                        ORDER BY IdTrabajoRealizado;", con))
                    {
                        cmd.Parameters.AddWithValue("@idc", idCierre.Value);
                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                vm.Trabajos.Add(new TrabajoRealizadoModel
                                {
                                    IdTrabajoRealizado = Convert.ToInt32(rd["IdTrabajoRealizado"]),
                                    IdOrdenCerrada = Convert.ToInt32(rd["IdOrdenCerrada"]),
                                    Descripcion = rd["Descripcion"] == DBNull.Value ? null : rd["Descripcion"].ToString(),
                                    Detalle = rd["Detalle"] == DBNull.Value ? null : rd["Detalle"].ToString(),
                                    Cantidad = rd["Cantidad"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["Cantidad"])
                                });
                            }
                        }
                    }
                }
            }
            finally
            {
                if (con.State == ConnectionState.Open) con.Close();
            }

            return View("DetalleOrdenCerrada", vm);
        }
    }
}
