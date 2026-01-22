using ClosedXML.Excel;
using controlLuces.Models;
using controlLuces.permisos;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;

namespace controlLuces.Controllers
{
    //[Authorize]
    public class pqrsController : Controller
    {
        SqlConnection con = new SqlConnection();
        SqlCommand com = new SqlCommand();
        SqlDataReader dr;

        void connectionString()
        {
            con.ConnectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
        }

        // ===== Helpers de rol/municipio =====
        private bool EsAdminLocal()
        {
            var usuario = Session["Usuario"] as UsuarioModel;
            if (usuario != null) return usuario.IdRol == Rol.Administrador_Local;

            if (Session["EsAdminLocal"] is bool b) return b;
            return false;
        }

        private string MunicipioSesion()
        {
            var m = Session["Municipio"] as string;
            if (!string.IsNullOrWhiteSpace(m)) return m;

            var u = Session["Usuario"] as UsuarioModel;
            return u?.Municipio ?? string.Empty;
        }

        private static string PrefixDesdeMunicipio(string municipio)
        {
            var year = DateTime.Now.Year; // 2025
            if (string.IsNullOrWhiteSpace(municipio)) return $"LC{year}";
            var k = municipio.Trim().ToLowerInvariant();
            if (k.Contains("madrid")) return $"MA{year}";
            if (k.Contains("chía") || k.Contains("chia")) return $"CH{year}";
            return $"LC{year}";
        }

        // NUEVO: genera consecutivo + código por municipio
        private (int consecutivo, string codigo) GenerarConsecutivoPorMunicipio(string municipio, DateTime fechaRegistro)
        {
            if (string.IsNullOrWhiteSpace(municipio))
                municipio = null;

            connectionString();
            int nuevoConsecutivo = 1;

            using (var cx = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(ConsecutivoMunicipio), 0)
                FROM dbo.pqrs
                WHERE (@Municipio IS NULL AND Municipio IS NULL)
                   OR (Municipio = @Municipio);", cx))
            {
                cmd.Parameters.AddWithValue("@Municipio", (object)municipio ?? DBNull.Value);
                cx.Open();
                var result = cmd.ExecuteScalar();
                int ultimo = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                nuevoConsecutivo = ultimo + 1;
            }

            string prefijo = PrefixDesdeMunicipio(municipio);
            string codigo = $"{prefijo}:{nuevoConsecutivo}";

            return (nuevoConsecutivo, codigo);
        }

        // GET: pqrs
        public ActionResult pqrs()
        {
            return View();
        }

        public ActionResult EliminarPqrs(int id)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "DELETE FROM pqrs WHERE Idpqrs = @Idpqrs";
            com.Parameters.AddWithValue("@Idpqrs", id);

            try
            {
                int rowsAffected = com.ExecuteNonQuery();
                con.Close();

                if (rowsAffected > 0)
                    ViewBag.DeleteMessage = "PQRS eliminado correctamente.";
                else
                    ViewBag.DeleteMessage = "No se encontró ningún PQRS con el Id proporcionado.";

                return RedirectToAction("MostrarPqrs");
            }
            catch (Exception ex)
            {
                ViewBag.DeleteMessage = "Error al intentar eliminar el PQRS: " + ex.Message;
                return RedirectToAction("MostrarPqrs");
            }
        }

        [HttpPost]
        public ActionResult Enviarpqrs(PqrsModel pqrs, HttpPostedFileBase imagen)
        {
            try
            {
                if (pqrs == null)
                {
                    return Json(new { success = false, message = "Datos inválidos del formulario." });
                }

                // === Validaciones mínimas ===
                if (string.IsNullOrWhiteSpace(pqrs.Referencia))
                {
                    return Json(new
                    {
                        success = false,
                        field = "Referencia",
                        message = "El campo 'Código del elemento' es obligatorio."
                    });
                }

                if (string.IsNullOrWhiteSpace(pqrs.Tipopqrs))
                {
                    return Json(new
                    {
                        success = false,
                        field = "Tipopqrs",
                        message = "Debes seleccionar el tipo de PQRS."
                    });
                }

                // Imagen
                byte[] data;
                if (imagen != null && imagen.ContentLength > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        imagen.InputStream.CopyTo(ms);
                        data = ms.ToArray();
                    }
                }
                else
                {
                    data = Encoding.UTF8.GetBytes("Sin recursos de imagen");
                }

                // Fecha Colombia
                DateTime fechaRegistro = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"));

                // Municipio y usuario actual
                var u = Session["Usuario"] as UsuarioModel;
                string municipio = u?.Municipio;
                int? createdBy = u?.IdUsuario;

                // Generar consecutivo por municipio
                var datosConsec = GenerarConsecutivoPorMunicipio(municipio, fechaRegistro);
                int consecutivoMunicipio = datosConsec.consecutivo;
                string codigoPqrs = datosConsec.codigo;   // Ej: MA2025:112

                connectionString();
                con.Open();
                com.Connection = con;
                com.Parameters.Clear();

                com.CommandText = @"
INSERT INTO dbo.pqrs
(FechaRegistro, Tipopqrs, Canal, Nombre, Apellido, TipoDoc, Documento, Telefono, Correo,
 Referencia, DireccionAfectacion, BarrioAfectacion, TipoAlumbrado, DescripcionAfectacion,
 Imagen, Estado, DatosRelacionados, Municipio, CreatedByUserId, ConsecutivoMunicipio, CodigoPqrs)
VALUES
(@FechaRegistro, @Tipopqrs, @Canal, @Nombre, @Apellido, @TipoDoc, @Documento, @Telefono, @Correo,
 @Referencia, @DireccionAfectacion, @BarrioAfectacion, @TipoAlumbrado, @DescripcionAfectacion,
 @Imagen, @Estado, @DatosRelacionados, @Municipio, @CreatedByUserId, @ConsecutivoMunicipio, @CodigoPqrs);
SELECT SCOPE_IDENTITY();";

                com.Parameters.AddWithValue("@FechaRegistro", fechaRegistro);
                com.Parameters.AddWithValue("@Tipopqrs", pqrs.Tipopqrs.Trim());
                com.Parameters.AddWithValue("@Canal", (object)(pqrs.Canal ?? "").Trim());
                com.Parameters.AddWithValue("@Nombre", (object)(pqrs.Nombre ?? "").Trim());
                com.Parameters.AddWithValue("@Apellido", (object)(pqrs.Apellido ?? "").Trim());
                com.Parameters.AddWithValue("@TipoDoc", (object)(pqrs.TipoDoc ?? "").Trim());
                com.Parameters.AddWithValue("@Documento", (object)(pqrs.Documento ?? "").Trim());
                com.Parameters.AddWithValue("@Telefono", (object)(pqrs.Telefono ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@Correo", (object)(pqrs.Correo ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@Referencia", pqrs.Referencia.Trim());
                com.Parameters.AddWithValue("@DireccionAfectacion", (object)(pqrs.DireccionAfectacion ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@BarrioAfectacion", (object)(pqrs.BarrioAfectacion ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@TipoAlumbrado", (object)(pqrs.TipoAlumbrado ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@DescripcionAfectacion", (object)(pqrs.DescripcionAfectacion ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@Imagen", data);
                com.Parameters.AddWithValue("@Estado", 1);
                com.Parameters.AddWithValue("@DatosRelacionados", (object)(pqrs.DatosRelacionados ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@Municipio", (object)(municipio ?? (string)null) ?? DBNull.Value);
                com.Parameters.AddWithValue("@CreatedByUserId", (object)createdBy ?? DBNull.Value);
                com.Parameters.AddWithValue("@ConsecutivoMunicipio", consecutivoMunicipio);
                com.Parameters.AddWithValue("@CodigoPqrs", codigoPqrs);

                int id = Convert.ToInt32(com.ExecuteScalar());
                con.Close();

                return Json(new { success = true, id, codigo = codigoPqrs });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error en el registro: " + ex.Message
                });
            }
        }

        public ActionResult pqrssinasignar()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = @"
SELECT pqrs.*, Estado.Nombre AS EstadoNombre
FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado
WHERE pqrs.Estado IN (1)";

            if (EsAdminLocal())
            {
                sql += " AND pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<PqrsModel> pqrsList = new List<PqrsModel>();

            while (dr.Read())
            {
                string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();

                string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                    ? codigoBd
                    : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                PqrsModel pq = new PqrsModel();
                pq.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                pq.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                pq.CodigoPqrs = codigoBd;
                pq.Consecutivo = consecutivoMostrar;

                pq.FechaRegistro = dr["FechaRegistro"].ToString();
                pq.Tipopqrs = dr["Tipopqrs"].ToString();
                pq.Canal = dr["Canal"].ToString();
                pq.Nombre = dr["Nombre"].ToString();
                pq.Apellido = dr["Apellido"].ToString();
                pq.TipoDoc = dr["TipoDoc"].ToString();
                pq.Documento = dr["Documento"].ToString();
                pq.Telefono = dr["Telefono"].ToString();
                pq.Correo = dr["Correo"].ToString();
                pq.Referencia = dr["Referencia"].ToString();
                pq.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                pq.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                pq.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                pq.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                pq.EstadoNombre = dr["EstadoNombre"].ToString();

                pqrsList.Add(pq);
            }

            con.Close();
            return View(pqrsList);
        }

        public ActionResult pqrsresuelta()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = @"
SELECT pqrs.*, Estado.Nombre AS EstadoNombre
FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado
WHERE pqrs.Estado IN (3)";

            if (EsAdminLocal())
            {
                sql += " AND pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<PqrsModel> pqrsList = new List<PqrsModel>();
            while (dr.Read())
            {
                string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                    ? codigoBd
                    : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                PqrsModel pq = new PqrsModel();
                pq.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                pq.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                pq.CodigoPqrs = codigoBd;
                pq.Consecutivo = consecutivoMostrar;

                pq.FechaRegistro = dr["FechaRegistro"].ToString();
                pq.Tipopqrs = dr["Tipopqrs"].ToString();
                pq.Canal = dr["Canal"].ToString();
                pq.Nombre = dr["Nombre"].ToString();
                pq.Apellido = dr["Apellido"].ToString();
                pq.TipoDoc = dr["TipoDoc"].ToString();
                pq.Documento = dr["Documento"].ToString();
                pq.Telefono = dr["Telefono"].ToString();
                pq.Correo = dr["Correo"].ToString();
                pq.Referencia = dr["Referencia"].ToString();
                pq.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                pq.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                pq.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                pq.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                pq.EstadoNombre = dr["EstadoNombre"].ToString();

                pqrsList.Add(pq);
            }

            dr.Close();

            // ========================================
            // NUEVA LÓGICA: Cargar Cuadrilla y Fecha a Realizar para cada PQRS
            // ========================================
            foreach (var pq in pqrsList)
            {
                com.Parameters.Clear();
                com.CommandText = @"
            SELECT TOP 1 cuadrilla, fecha_a_realizar
            FROM ordenes_de_servicio
            WHERE (elemento_relacionado = @IdpqrsStr)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_orden = @Ref)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_de_elemento = @Ref)
            ORDER BY id_orden DESC";

                com.Parameters.AddWithValue("@IdpqrsStr", pq.Idpqrs.ToString());
                com.Parameters.AddWithValue("@Ref", (object)(pq.Referencia ?? "") ?? DBNull.Value);

                using (SqlDataReader drOrden = com.ExecuteReader())
                {
                    if (drOrden.Read())
                    {
                        pq.Cuadrilla = drOrden["cuadrilla"] == DBNull.Value ? null : drOrden["cuadrilla"].ToString();
                        pq.FechaARealizar = drOrden["fecha_a_realizar"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(drOrden["fecha_a_realizar"]);
                    }
                }
            }

            con.Close();
            return View(pqrsList);
        }
        // ========================================
        // MÉTODO DEL CONTROLADOR (pqrsController.cs)
        // ========================================

        public ActionResult pqrsasignada()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = @"
SELECT pqrs.*, Estado.Nombre AS EstadoNombre
FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado
WHERE pqrs.Estado IN (2)";

            if (EsAdminLocal())
            {
                sql += " AND pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<PqrsModel> pqrsList = new List<PqrsModel>();
            while (dr.Read())
            {
                string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                    ? codigoBd
                    : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                PqrsModel pq = new PqrsModel();
                pq.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                pq.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                pq.CodigoPqrs = codigoBd;
                pq.Consecutivo = consecutivoMostrar;

                pq.FechaRegistro = dr["FechaRegistro"].ToString();
                pq.Tipopqrs = dr["Tipopqrs"].ToString();
                pq.Canal = dr["Canal"].ToString();
                pq.Nombre = dr["Nombre"].ToString();
                pq.Apellido = dr["Apellido"].ToString();
                pq.TipoDoc = dr["TipoDoc"].ToString();
                pq.Documento = dr["Documento"].ToString();
                pq.Telefono = dr["Telefono"].ToString();
                pq.Correo = dr["Correo"].ToString();
                pq.Referencia = dr["Referencia"].ToString();
                pq.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                pq.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                pq.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                pq.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                pq.EstadoNombre = dr["EstadoNombre"].ToString();

                pqrsList.Add(pq);
            }

            dr.Close();

            // ========================================
            // NUEVA LÓGICA: Cargar Cuadrilla y Fecha a Realizar para cada PQRS
            // ========================================
            foreach (var pq in pqrsList)
            {
                com.Parameters.Clear();
                com.CommandText = @"
            SELECT TOP 1 cuadrilla, fecha_a_realizar
            FROM ordenes_de_servicio
            WHERE (elemento_relacionado = @IdpqrsStr)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_orden = @Ref)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_de_elemento = @Ref)
            ORDER BY id_orden DESC";

                com.Parameters.AddWithValue("@IdpqrsStr", pq.Idpqrs.ToString());
                com.Parameters.AddWithValue("@Ref", (object)(pq.Referencia ?? "") ?? DBNull.Value);

                using (SqlDataReader drOrden = com.ExecuteReader())
                {
                    if (drOrden.Read())
                    {
                        pq.Cuadrilla = drOrden["cuadrilla"] == DBNull.Value ? null : drOrden["cuadrilla"].ToString();
                        pq.FechaARealizar = drOrden["fecha_a_realizar"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(drOrden["fecha_a_realizar"]);
                    }
                }
            }

            con.Close();
            return View(pqrsList);
        }
        public ActionResult VerInfoCompleto(int id)
        {
            var vm = new controlLuces.Models.PqrsOrdenCompletaVM();

            connectionString();
            con.Open();

            try
            {
                // 1) PQRS
                using (var cmd = new SqlCommand(@"
    SELECT p.*, e.Nombre AS EstadoNombre, p.Imagen AS Imagen
    FROM dbo.pqrs AS p
    INNER JOIN dbo.Estado AS e ON p.Estado = e.IdEstado
    WHERE p.Idpqrs = @Idpqrs
      AND (@SoloMuni = 0 OR p.Municipio = @Municipio);
", con))
                {
                    cmd.Parameters.AddWithValue("@Idpqrs", id);
                    cmd.Parameters.AddWithValue("@SoloMuni", EsAdminLocal() ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Municipio", MunicipioSesion());

                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            vm.Pqrs.Idpqrs = Convert.ToInt32(rdr["Idpqrs"]);
                            vm.Pqrs.FechaRegistro = rdr["FechaRegistro"]?.ToString();
                            vm.Pqrs.Tipopqrs = rdr["Tipopqrs"]?.ToString();
                            vm.Pqrs.Canal = rdr["Canal"]?.ToString();
                            vm.Pqrs.Nombre = rdr["Nombre"]?.ToString();
                            vm.Pqrs.Apellido = rdr["Apellido"]?.ToString();
                            vm.Pqrs.TipoDoc = rdr["TipoDoc"]?.ToString();
                            vm.Pqrs.Documento = rdr["Documento"]?.ToString();
                            vm.Pqrs.Telefono = rdr["Telefono"]?.ToString();
                            vm.Pqrs.Correo = rdr["Correo"]?.ToString();
                            vm.Pqrs.Referencia = rdr["Referencia"]?.ToString();
                            vm.Pqrs.DireccionAfectacion = rdr["DireccionAfectacion"]?.ToString();
                            vm.Pqrs.BarrioAfectacion = rdr["BarrioAfectacion"]?.ToString();
                            vm.Pqrs.TipoAlumbrado = rdr["TipoAlumbrado"]?.ToString();
                            vm.Pqrs.DescripcionAfectacion = rdr["DescripcionAfectacion"]?.ToString();
                            vm.Pqrs.Estado = rdr["Estado"] != DBNull.Value ? Convert.ToInt32(rdr["Estado"]) : 0;
                            vm.Pqrs.EstadoNombre = rdr["EstadoNombre"]?.ToString();
                            vm.Pqrs.DatosRelacionados = rdr["DatosRelacionados"]?.ToString();

                            vm.Pqrs.ConsecutivoMunicipio = rdr["ConsecutivoMunicipio"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(rdr["ConsecutivoMunicipio"]);
                            vm.Pqrs.CodigoPqrs = rdr["CodigoPqrs"] == DBNull.Value
                                ? null
                                : rdr["CodigoPqrs"].ToString();

                            if (rdr["Imagen"] != DBNull.Value)
                                vm.Pqrs.img = (byte[])rdr["Imagen"];

                            if (!string.IsNullOrWhiteSpace(vm.Pqrs.CodigoPqrs))
                            {
                                vm.Pqrs.Consecutivo = vm.Pqrs.CodigoPqrs;
                            }
                            else
                            {
                                var pref = PrefixDesdeMunicipio(rdr["Municipio"] == DBNull.Value ? null : rdr["Municipio"].ToString());
                                vm.Pqrs.Consecutivo = $"{pref}:{vm.Pqrs.Idpqrs}";
                            }
                        }
                        else
                        {
                            ViewBag.Error = "No se encontró la PQRS solicitada o no pertenece a tu municipio.";
                            return View("DetalleCompleto", vm);
                        }
                    }
                }

                // 2) ORDEN
                int ordenId = 0;
                using (var cmd = new SqlCommand(@"
    SELECT TOP 1 o.*, e.Nombre AS EstadoNombre
    FROM dbo.ordenes_de_servicio AS o
    LEFT JOIN dbo.Estado AS e ON o.IdEstado = e.IdEstado
    WHERE (o.elemento_relacionado = @IdpqrsStr)
       OR ( (@Ref IS NOT NULL AND @Ref <> '') AND o.codigo_orden = @Ref )
       OR ( (@Ref IS NOT NULL AND @Ref <> '') AND o.codigo_de_elemento = @Ref )
    ORDER BY o.id_orden DESC;", con))
                {
                    cmd.Parameters.AddWithValue("@IdpqrsStr", id.ToString());
                    cmd.Parameters.AddWithValue("@Ref", (object)(vm.Pqrs.Referencia ?? ""));

                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            vm.Orden.IdOrden = Convert.ToInt32(rdr["id_orden"]);
                            ordenId = vm.Orden.IdOrden;

                            vm.Orden.TipoDeElemento = rdr["Tipo_de_elemento"]?.ToString();
                            vm.Orden.CodigoDeElemento = rdr["codigo_de_elemento"]?.ToString();
                            vm.Orden.ElementoRelacionado = rdr["elemento_relacionado"]?.ToString();
                            vm.Orden.CodigoOrden = rdr["codigo_orden"]?.ToString();
                            vm.Orden.ProblemaRelacionado = rdr["problema_relacionado"]?.ToString();
                            vm.Orden.ProblemaValidado = rdr["problema_validado"]?.ToString();
                            vm.Orden.OrdenPrioridad = rdr["Orden_prioridad"]?.ToString();
                            vm.Orden.PrioridadDeRuta = rdr["prioridad_de_ruta"] != DBNull.Value ? Convert.ToInt32(rdr["prioridad_de_ruta"]) : 0;
                            vm.Orden.FechaARealizar = rdr["fecha_a_realizar"] != DBNull.Value ? Convert.ToDateTime(rdr["fecha_a_realizar"]) : DateTime.MinValue;
                            vm.Orden.Cuadrilla = rdr["cuadrilla"]?.ToString();
                            vm.Orden.TipoDeOrden = rdr["tipo_de_orden"]?.ToString();
                            vm.Orden.TipoDeSolucion = rdr["tipo_de_Solucion"]?.ToString();
                            vm.Orden.ClaseDeOrden = rdr["clase_de_orden"]?.ToString();
                            vm.Orden.ObraRelacionada = rdr["obra_relacionada"]?.ToString();
                            vm.Orden.IdEstado = rdr["IdEstado"] != DBNull.Value ? Convert.ToInt32(rdr["IdEstado"]) : 0;
                            vm.Orden.EstadoNombre = rdr["EstadoNombre"]?.ToString();
                            vm.Orden.observaciones = rdr["observaciones"]?.ToString();
                            vm.Orden.Trabajos = rdr["Trabajos"]?.ToString();
                        }
                    }
                }

                // 3) CIERRE
                int idCierre = 0;
                if (ordenId > 0)
                {
                    using (var cmd = new SqlCommand(@"
        SELECT TOP 1 *
        FROM dbo.Ordenes_Cerradas
        WHERE Id_Orden_Servicio = @IdOrden
        ORDER BY Id_Orden_Cerrada DESC;", con))
                    {
                        cmd.Parameters.AddWithValue("@IdOrden", ordenId);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                vm.Cierre.Id_Orden_Cerrada = Convert.ToInt32(rdr["Id_Orden_Cerrada"]);
                                idCierre = vm.Cierre.Id_Orden_Cerrada;

                                vm.Cierre.Id_Orden_Servicio = rdr["Id_Orden_Servicio"] != DBNull.Value ? Convert.ToInt32(rdr["Id_Orden_Servicio"]) : 0;
                                vm.Cierre.Observacion = rdr["Observacion"]?.ToString();
                                vm.Cierre.Respuesta = rdr["Respuesta"] != DBNull.Value ? (int?)Convert.ToInt32(rdr["Respuesta"]) : null;
                                vm.Cierre.Recursos = rdr["Recursos"] != DBNull.Value ? (byte[])rdr["Recursos"] : null;
                                vm.Cierre.MaterialesUsados = rdr["MaterialesUsados"]?.ToString();
                            }
                        }
                    }
                }

                // 4) TRABAJOS REALIZADOS
                vm.Trabajos = new List<controlLuces.Models.TrabajoRealizadoModel>();
                if (idCierre > 0)
                {
                    using (var cmd = new SqlCommand(@"
        SELECT t.*
        FROM dbo.Trabajos_Realizados AS t
        WHERE t.IdOrdenCerrada = @IdCierre
        ORDER BY t.IdTrabajoRealizado ASC;", con))
                    {
                        cmd.Parameters.AddWithValue("@IdCierre", idCierre);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                vm.Trabajos.Add(new controlLuces.Models.TrabajoRealizadoModel
                                {
                                    IdTrabajoRealizado = Convert.ToInt32(rdr["IdTrabajoRealizado"]),
                                    IdOrdenCerrada = Convert.ToInt32(rdr["IdOrdenCerrada"]),
                                    Descripcion = rdr["Descripcion"]?.ToString(),
                                    Detalle = rdr["Detalle"]?.ToString(),
                                    Cantidad = rdr["Cantidad"] != DBNull.Value ? (decimal?)Convert.ToDecimal(rdr["Cantidad"]) : null
                                });
                            }
                        }
                    }
                }

                // 5) INSUMOS
                vm.Insumos = new List<InsumoRealizadoModel>();
                if (idCierre > 0)
                {
                    using (var cmd = new SqlCommand(@"
        SELECT  i.IdInsumoRealizado,
                i.IdOrdenCerrada,
                COALESCE(inv.nombre_elemento, i.Descripcion) AS NombreInsumo,
                i.Cantidad
        FROM dbo.insumos_realizados AS i
        LEFT JOIN dbo.Inventario AS inv ON i.IdInventario = inv.ID
        WHERE i.IdOrdenCerrada = @IdCierre
        ORDER BY i.IdInsumoRealizado ASC;", con))
                    {
                        cmd.Parameters.AddWithValue("@IdCierre", idCierre);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                vm.Insumos.Add(new InsumoRealizadoModel
                                {
                                    IdInsumoRealizado = Convert.ToInt32(rdr["IdInsumoRealizado"]),
                                    IdOrdenCerrada = Convert.ToInt32(rdr["IdOrdenCerrada"]),
                                    NombreInsumo = rdr["NombreInsumo"]?.ToString(),
                                    Cantidad = rdr["Cantidad"] != DBNull.Value ? Convert.ToInt32(rdr["Cantidad"]) : 0
                                });
                            }
                        }
                    }
                }

                // 6) IMÁGENES
                vm.Imagenes = new List<controlLuces.Models.ImagenOrdenServicioModel>();
                using (var cmd = new SqlCommand(@"
    SELECT i.*
    FROM dbo.ImagenesOrdenesDeServicio AS i
    WHERE (@IdOrden > 0 AND i.id_orden = @IdOrden)
       OR (@IdCierre > 0 AND i.Id_Orden_Cerrada = @IdCierre)
    ORDER BY i.id_imagen ASC;", con))
                {
                    cmd.Parameters.AddWithValue("@IdOrden", ordenId);
                    cmd.Parameters.AddWithValue("@IdCierre", idCierre);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var imagen = new controlLuces.Models.ImagenOrdenServicioModel
                            {
                                IdImagen = Convert.ToInt32(rdr["id_imagen"]),
                                IdOrden = rdr["id_orden"] != DBNull.Value ? Convert.ToInt32(rdr["id_orden"]) : 0,
                                Imagen = rdr["imagen"] != DBNull.Value ? (byte[])rdr["imagen"] : null,
                                FechaSubida = rdr["fecha_subida"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rdr["fecha_subida"]) : null
                            };
                            vm.Imagenes.Add(imagen);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Ocurrió un error al cargar la información: " + ex.Message;
            }
            finally
            {
                con.Close();
            }

            return View("DetalleCompleto", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgregarInsumo(int idPqrs, int idOrdenCerrada, string nombreInsumo, int cantidad)
        {
            if (string.IsNullOrWhiteSpace(nombreInsumo) || cantidad <= 0)
            {
                TempData["ErrorInsumo"] = "Debes indicar el insumo y una cantidad mayor a 0.";
                return RedirectToAction("VerInfoCompleto", new { id = idPqrs });
            }

            connectionString();
            con.Open();

            try
            {
                using (var cmd = new SqlCommand(@"
            INSERT INTO dbo.insumos_realizados
                (IdOrdenCerrada, NombreInsumo, Cantidad)
            VALUES
                (@IdOrdenCerrada, @NombreInsumo, @Cantidad);", con))
                {
                    cmd.Parameters.AddWithValue("@IdOrdenCerrada", idOrdenCerrada);
                    cmd.Parameters.AddWithValue("@NombreInsumo", nombreInsumo);
                    cmd.Parameters.AddWithValue("@Cantidad", cantidad);
                    cmd.ExecuteNonQuery();
                }

                TempData["OkInsumo"] = "Insumo guardado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorInsumo"] = "Error al guardar insumo: " + ex.Message;
            }
            finally
            {
                con.Close();
            }

            return RedirectToAction("VerInfoCompleto", new { id = idPqrs });
        }

        public ActionResult BuscarPqrs(string tipoBusqueda, string Idpqrs, DateTime? desde, DateTime? hasta)
        {
            List<PqrsModel> resultados = new List<PqrsModel>();

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            if (tipoBusqueda == "consecutivo" && !string.IsNullOrWhiteSpace(Idpqrs))
            {
                var sql = @"
            SELECT pqrs.*, Estado.Nombre AS EstadoNombre 
            FROM pqrs 
            INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado 
            WHERE 
                (pqrs.CodigoPqrs = @Codigo
                 OR CAST(pqrs.ConsecutivoMunicipio AS NVARCHAR(50)) = @ConsecutivoTexto
                 OR CAST(pqrs.Idpqrs AS NVARCHAR(50)) = @ConsecutivoTexto)";

                if (EsAdminLocal())
                {
                    sql += " AND pqrs.Municipio = @Municipio";
                    com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
                }

                com.CommandText = sql;
                com.Parameters.AddWithValue("@Codigo", Idpqrs.Trim());
                com.Parameters.AddWithValue("@ConsecutivoTexto", Idpqrs.Trim());
            }
            else if (tipoBusqueda == "fecha" && desde.HasValue && hasta.HasValue)
            {
                var sql = "SELECT pqrs.*, Estado.Nombre AS EstadoNombre FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado WHERE pqrs.FechaRegistro BETWEEN @Desde AND @Hasta";
                if (EsAdminLocal())
                {
                    sql += " AND pqrs.Municipio = @Municipio";
                    com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
                }
                com.CommandText = sql;
                com.Parameters.AddWithValue("@Desde", desde.Value);
                com.Parameters.AddWithValue("@Hasta", hasta.Value);
            }
            else
            {
                con.Close();
                ViewBag.Resultados = resultados;
                return View("archivopqrs", resultados);
            }

            try
            {
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                    string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                    string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                        ? codigoBd
                        : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                    PqrsModel pq = new PqrsModel
                    {
                        Idpqrs = Convert.ToInt32(dr["Idpqrs"]),
                        ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]),
                        CodigoPqrs = codigoBd,
                        Consecutivo = consecutivoMostrar,
                        FechaRegistro = dr["FechaRegistro"].ToString(),
                        Tipopqrs = dr["Tipopqrs"].ToString(),
                        Canal = dr["Canal"].ToString(),
                        Nombre = dr["Nombre"].ToString(),
                        Apellido = dr["Apellido"].ToString(),
                        TipoDoc = dr["TipoDoc"].ToString(),
                        Documento = dr["Documento"].ToString(),
                        Telefono = dr["Telefono"].ToString(),
                        Correo = dr["Correo"].ToString(),
                        Referencia = dr["Referencia"].ToString(),
                        DireccionAfectacion = dr["DireccionAfectacion"].ToString(),
                        BarrioAfectacion = dr["BarrioAfectacion"].ToString(),
                        TipoAlumbrado = dr["TipoAlumbrado"].ToString(),
                        DescripcionAfectacion = dr["DescripcionAfectacion"].ToString(),
                        Estado = Convert.ToInt32(dr["Estado"]),
                        EstadoNombre = dr["EstadoNombre"].ToString()
                    };
                    resultados.Add(pq);
                }
                con.Close();
            }
            catch
            {
            }

            ViewBag.Resultados = resultados;
            return View("archivopqrs", resultados);
        }

        public ActionResult GenerarPdf()
        {
            List<PqrsModel> pqrsList = ObtenerPqrs();

            Document doc = new Document(PageSize.A4, 10, 10, 10, 10);
            MemoryStream ms = new MemoryStream();
            PdfWriter writer = PdfWriter.GetInstance(doc, ms);
            doc.Open();

            string logoPath = Server.MapPath("~/Content/img/logo.jpg");
            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
            logo.ScaleToFit(100, 100);
            logo.Alignment = Element.ALIGN_LEFT;
            doc.Add(logo);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var timestampFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            doc.Add(new Paragraph("Hora de descarga: " + timestamp, timestampFont));

            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
            var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            doc.Add(new Paragraph("LIGHT CONTROL", titleFont));
            doc.Add(new Paragraph(" ", bodyFont));

            PdfPTable table = new PdfPTable(5);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 2, 2, 3, 2 });

            PdfPCell cell = new PdfPCell(new Phrase("ID", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY };
            table.AddCell(cell);
            cell = new PdfPCell(new Phrase("Fecha Registro", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY };
            table.AddCell(cell);
            cell = new PdfPCell(new Phrase("Tipo PQRS", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY };
            table.AddCell(cell);
            cell = new PdfPCell(new Phrase("Nombre", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY };
            table.AddCell(cell);
            cell = new PdfPCell(new Phrase("Estado", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY };
            table.AddCell(cell);

            foreach (var pqrs in pqrsList)
            {
                table.AddCell(pqrs.Idpqrs.ToString());
                table.AddCell(pqrs.FechaRegistro);
                table.AddCell(pqrs.Tipopqrs);
                table.AddCell($"{pqrs.Nombre} {pqrs.Apellido}");
                table.AddCell(pqrs.EstadoNombre);
            }

            doc.Add(table);
            doc.Close();

            byte[] file = ms.ToArray();
            ms.Close();
            return File(file, "application/pdf", "ReportePQRS.pdf");
        }

        private List<PqrsModel> ObtenerPqrs()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = @"
SELECT pqrs.*, Estado.Nombre AS EstadoNombre
FROM pqrs
INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado";

            if (EsAdminLocal())
            {
                sql += " WHERE pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<PqrsModel> pqrsList = new List<PqrsModel>();
            while (dr.Read())
            {
                string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                    ? codigoBd
                    : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                PqrsModel pq = new PqrsModel();
                pq.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                pq.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                pq.CodigoPqrs = codigoBd;
                pq.Consecutivo = consecutivoMostrar;

                pq.FechaRegistro = dr["FechaRegistro"].ToString();
                pq.Tipopqrs = dr["Tipopqrs"].ToString();
                pq.Canal = dr["Canal"].ToString();
                pq.Nombre = dr["Nombre"].ToString();
                pq.Apellido = dr["Apellido"].ToString();
                pq.TipoDoc = dr["TipoDoc"].ToString();
                pq.Documento = dr["Documento"].ToString();
                pq.Telefono = dr["Telefono"].ToString();
                pq.Correo = dr["Correo"].ToString();
                pq.Referencia = dr["Referencia"].ToString();
                pq.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                pq.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                pq.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                pq.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                pq.EstadoNombre = dr["EstadoNombre"].ToString();
                pq.DatosRelacionados = dr["DatosRelacionados"] != DBNull.Value ? dr["DatosRelacionados"].ToString() : null;

                pqrsList.Add(pq);
            }
            con.Close();
            return pqrsList;
        }

        public ActionResult VerInfo_usuario(int id)
        {
            PqrsModel pqrs = new PqrsModel();

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT pqrs.*, Estado.Nombre AS EstadoNombre ,pqrs.Imagen AS Imagen FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado WHERE pqrs.Idpqrs = @Idpqrs";
            if (EsAdminLocal())
            {
                sql += " AND pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            com.CommandText = sql;
            com.Parameters.AddWithValue("@Idpqrs", id);

            try
            {
                SqlDataReader dr = com.ExecuteReader();

                if (dr.Read())
                {
                    string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                    string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                    string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                        ? codigoBd
                        : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                    pqrs.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                    pqrs.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                    pqrs.CodigoPqrs = codigoBd;
                    pqrs.Consecutivo = consecutivoMostrar;

                    pqrs.FechaRegistro = dr["FechaRegistro"].ToString();
                    pqrs.Tipopqrs = dr["Tipopqrs"].ToString();
                    pqrs.Canal = dr["Canal"].ToString();
                    pqrs.Nombre = dr["Nombre"].ToString();
                    pqrs.Apellido = dr["Apellido"].ToString();
                    pqrs.TipoDoc = dr["TipoDoc"].ToString();
                    pqrs.Documento = dr["Documento"].ToString();
                    pqrs.Telefono = dr["Telefono"].ToString();
                    pqrs.Correo = dr["Correo"].ToString();
                    pqrs.Referencia = dr["Referencia"].ToString();
                    pqrs.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                    pqrs.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                    pqrs.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                    pqrs.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                    pqrs.Estado = Convert.ToInt32(dr["Estado"]);
                    pqrs.EstadoNombre = dr["EstadoNombre"].ToString();
                    pqrs.DatosRelacionados = dr["DatosRelacionados"] != DBNull.Value ? dr["DatosRelacionados"].ToString() : null;

                    if (dr["Imagen"] != DBNull.Value)
                    {
                        pqrs.img = (byte[])dr["Imagen"];
                        string imagenDataUrl = Convert.ToBase64String(pqrs.img);
                        ViewBag.ImagenDataUrl = "data:image/jpeg;base64," + imagenDataUrl;
                    }
                }
                con.Close();
            }
            catch
            {
            }

            return View(pqrs);
        }

        public ActionResult VerInfo(int id)
        {
            PqrsModel pqrs = new PqrsModel();
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT pqrs.*, Estado.Nombre AS EstadoNombre, pqrs.Imagen AS Imagen FROM pqrs INNER JOIN Estado ON pqrs.Estado = Estado.IdEstado WHERE pqrs.Idpqrs = @Idpqrs";
            if (EsAdminLocal())
            {
                sql += " AND pqrs.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            com.CommandText = sql;
            com.Parameters.AddWithValue("@Idpqrs", id);

            try
            {
                SqlDataReader dr = com.ExecuteReader();
                if (dr.Read())
                {
                    string municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString();
                    string codigoBd = dr["CodigoPqrs"] == DBNull.Value ? null : dr["CodigoPqrs"].ToString();
                    string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                        ? codigoBd
                        : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(dr["Idpqrs"])}";

                    pqrs.Idpqrs = Convert.ToInt32(dr["Idpqrs"]);
                    pqrs.ConsecutivoMunicipio = dr["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["ConsecutivoMunicipio"]);
                    pqrs.CodigoPqrs = codigoBd;
                    pqrs.Consecutivo = consecutivoMostrar;

                    pqrs.FechaRegistro = dr["FechaRegistro"].ToString();
                    pqrs.Tipopqrs = dr["Tipopqrs"].ToString();
                    pqrs.Canal = dr["Canal"].ToString();
                    pqrs.Nombre = dr["Nombre"].ToString();
                    pqrs.Apellido = dr["Apellido"].ToString();
                    pqrs.TipoDoc = dr["TipoDoc"].ToString();
                    pqrs.Documento = dr["Documento"].ToString();
                    pqrs.Telefono = dr["Telefono"].ToString();
                    pqrs.Correo = dr["Correo"].ToString();
                    pqrs.Referencia = dr["Referencia"].ToString();
                    pqrs.DireccionAfectacion = dr["DireccionAfectacion"].ToString();
                    pqrs.BarrioAfectacion = dr["BarrioAfectacion"].ToString();
                    pqrs.TipoAlumbrado = dr["TipoAlumbrado"].ToString();
                    pqrs.DescripcionAfectacion = dr["DescripcionAfectacion"].ToString();
                    pqrs.Estado = Convert.ToInt32(dr["Estado"]);
                    pqrs.EstadoNombre = dr["EstadoNombre"].ToString();
                    pqrs.DatosRelacionados = dr["DatosRelacionados"] != DBNull.Value ? dr["DatosRelacionados"].ToString() : null;

                    if (dr["Imagen"] != DBNull.Value)
                    {
                        pqrs.img = (byte[])dr["Imagen"];
                        string imagenDataUrl = Convert.ToBase64String(pqrs.img);
                        ViewBag.ImagenDataUrl = "data:image/jpeg;base64," + imagenDataUrl;
                    }
                }
                dr.Close();

                // === NUEVA CONSULTA: BUSCAR LA ORDEN RELACIONADA ===
                com.Parameters.Clear();
                com.CommandText = @"
            SELECT TOP 1 cuadrilla, fecha_a_realizar
            FROM ordenes_de_servicio
            WHERE (elemento_relacionado = @IdpqrsStr)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_orden = @Ref)
               OR ((@Ref IS NOT NULL AND @Ref <> '') AND codigo_de_elemento = @Ref)
            ORDER BY id_orden DESC";

                com.Parameters.AddWithValue("@IdpqrsStr", id.ToString());
                com.Parameters.AddWithValue("@Ref", (object)(pqrs.Referencia ?? "") ?? DBNull.Value);

                SqlDataReader drOrden = com.ExecuteReader();
                if (drOrden.Read())
                {
                    pqrs.Cuadrilla = drOrden["cuadrilla"] == DBNull.Value ? null : drOrden["cuadrilla"].ToString();
                    pqrs.FechaARealizar = drOrden["fecha_a_realizar"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(drOrden["fecha_a_realizar"]);
                }
                drOrden.Close();

                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                ViewBag.Error = "Error al cargar datos: " + ex.Message;
            }

            return View(pqrs);
        }

        [PermisosRol(Rol.Administrador, Rol.Tecnico)]
        private List<object> ObtenerDatosPorFechaRegistro()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT CONVERT(date, FechaRegistro) AS Fecha, COUNT(*) AS Total FROM pqrs";
            if (EsAdminLocal())
            {
                sql += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            sql += " GROUP BY CONVERT(date, FechaRegistro)";

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<object> data = new List<object>();
            while (dr.Read())
            {
                var item = new
                {
                    Fecha = Convert.ToDateTime(dr["Fecha"]).ToString("yyyy-MM-dd"),
                    Total = Convert.ToInt32(dr["Total"])
                };
                data.Add(item);
            }

            con.Close();
            return data;
        }

        public ActionResult MostrarPqrs()
        {
            var lista = new List<PqrsModel>();

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var usuario = Session["Usuario"] as UsuarioModel;

            var sql = @"
        SELECT Idpqrs,
               FechaRegistro,
               Tipopqrs,
               Canal,
               DireccionAfectacion,
               DescripcionAfectacion,
               Municipio,
               ConsecutivoMunicipio,
               CodigoPqrs
        FROM dbo.pqrs";

            if (usuario != null &&
                (usuario.IdRol == Rol.Administrador_Local || usuario.IdRol == Rol.Usuario))
            {
                sql += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            sql += " ORDER BY Idpqrs DESC";

            com.CommandText = sql;

            using (var rd = com.ExecuteReader())
            {
                while (rd.Read())
                {
                    string municipio = rd["Municipio"] == DBNull.Value ? null : rd["Municipio"].ToString();
                    string codigoBd = rd["CodigoPqrs"] == DBNull.Value ? null : rd["CodigoPqrs"].ToString();

                    string consecutivoMostrar = !string.IsNullOrWhiteSpace(codigoBd)
                        ? codigoBd
                        : $"{PrefixDesdeMunicipio(municipio)}:{Convert.ToInt32(rd["Idpqrs"])}";

                    lista.Add(new PqrsModel
                    {
                        Idpqrs = Convert.ToInt32(rd["Idpqrs"]),
                        ConsecutivoMunicipio = rd["ConsecutivoMunicipio"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["ConsecutivoMunicipio"]),
                        CodigoPqrs = codigoBd,
                        Consecutivo = consecutivoMostrar,
                        FechaRegistro = rd["FechaRegistro"]?.ToString(),
                        Tipopqrs = rd["Tipopqrs"]?.ToString(),
                        Canal = rd["Canal"]?.ToString(),
                        DireccionAfectacion = rd["DireccionAfectacion"]?.ToString(),
                        DescripcionAfectacion = rd["DescripcionAfectacion"]?.ToString()
                    });
                }
            }

            con.Close();
            return View(lista);
        }

        public ActionResult ArchivoPqrs()
        {
            var listaVacia = new List<PqrsModel>();
            return View("archivopqrs", listaVacia);
        }

  
        public ActionResult GraficoTipoBarrio()
        {
            List<PqrsModel> pqrsList = ObtenerPqrs();

            var tipoBarrioCounts = pqrsList.GroupBy(p => p.BarrioAfectacion)
                                           .Select(g => new { TipoBarrio = g.Key, Count = g.Count() })
                                           .ToList();

            var labels = tipoBarrioCounts.Select(x => x.TipoBarrio).ToArray();
            var data = tipoBarrioCounts.Select(x => x.Count).ToArray();

            ViewBag.TipoBarrioLabels = labels;
            ViewBag.TipoBarrioData = data;

            return View("GraficoPqrs");
        }

        public ActionResult GraficoTipoCanal()
        {
            List<PqrsModel> pqrsList = ObtenerPqrs();

            var tipoCanalCounts = pqrsList.GroupBy(p => p.Canal)
                                          .Select(g => new { TipoCanal = g.Key, Count = g.Count() })
                                          .ToList();

            var labels = tipoCanalCounts.Select(x => x.TipoCanal).ToArray();
            var data = tipoCanalCounts.Select(x => x.Count).ToArray();

            ViewBag.TipoCanalLabels = labels;
            ViewBag.TipoCanalData = data;

            return View("GraficoPqrs");
        }

        public ActionResult GraficoPorMes()
        {
            List<PqrsModel> pqrsList = ObtenerPqrs();
            return View("GraficoPqrs");
        }

        // =============== GRAFICO GENERAL (usa helpers por municipio) ===============
        public ActionResult GraficoPqrs()
        {
            var tiposPqrs = ObtenerDatosPorTipoPqrs();
            var barriosAfectacion = ObtenerDatosPorBarrioAfectacion();
            var canales = ObtenerDatosPorCanal();
            var fechasRegistro = ObtenerDatosPorFechaRegistro();
            var estadosPqrs = ObtenerDatosPorEstadoPqrs();

            ViewBag.TiposPqrs = JsonConvert.SerializeObject(tiposPqrs);
            ViewBag.BarriosAfectacion = JsonConvert.SerializeObject(barriosAfectacion);
            ViewBag.Canales = JsonConvert.SerializeObject(canales);          // se mantiene por compatibilidad
            ViewBag.FechasRegistro = JsonConvert.SerializeObject(fechasRegistro);
            ViewBag.EstadosPqrs = JsonConvert.SerializeObject(estadosPqrs);  // NUEVO

            ViewBag.EsAdminLocal = EsAdminLocal();
            ViewBag.MunicipioSesion = MunicipioSesion();

            return View();
        }

        // Helpers estadísticos (ya con filtro de municipio)
        private List<object> ObtenerDatosPorTipoPqrs()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT Tipopqrs, COUNT(*) AS Total FROM pqrs";
            if (EsAdminLocal())
            {
                sql += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            sql += " GROUP BY Tipopqrs";

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<object> data = new List<object>();
            while (dr.Read())
            {
                var item = new
                {
                    TipoPqrs = dr["Tipopqrs"].ToString(),
                    Total = Convert.ToInt32(dr["Total"])
                };
                data.Add(item);
            }

            con.Close();
            return data;
        }

        private List<object> ObtenerDatosPorBarrioAfectacion()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT BarrioAfectacion, COUNT(*) AS Total FROM pqrs";
            if (EsAdminLocal())
            {
                sql += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            sql += " GROUP BY BarrioAfectacion";

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<object> data = new List<object>();
            while (dr.Read())
            {
                var item = new
                {
                    BarrioAfectacion = dr["BarrioAfectacion"].ToString(),
                    Total = Convert.ToInt32(dr["Total"])
                };
                data.Add(item);
            }

            con.Close();
            return data;
        }

        private List<object> ObtenerDatosPorCanal()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = "SELECT Canal, COUNT(*) AS Total FROM pqrs";
            if (EsAdminLocal())
            {
                sql += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }
            sql += " GROUP BY Canal";

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            List<object> data = new List<object>();
            while (dr.Read())
            {
                var item = new
                {
                    Canal = dr["Canal"].ToString(),
                    Total = Convert.ToInt32(dr["Total"])
                };
                data.Add(item);
            }

            con.Close();
            return data;
        }

        // NUEVO: datos por estado de la PQRS
        private List<object> ObtenerDatosPorEstadoPqrs()
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            var sql = @"
        SELECT e.Nombre AS Estado, COUNT(*) AS Total
        FROM pqrs p
        INNER JOIN Estado e ON p.Estado = e.IdEstado";

            if (EsAdminLocal())
            {
                sql += " WHERE p.Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", MunicipioSesion());
            }

            sql += " GROUP BY e.Nombre";

            com.CommandText = sql;
            SqlDataReader dr = com.ExecuteReader();

            var data = new List<object>();
            while (dr.Read())
            {
                var item = new
                {
                    Estado = dr["Estado"].ToString(),
                    Total = Convert.ToInt32(dr["Total"])
                };
                data.Add(item);
            }

            con.Close();
            return data;
        }
    }
}
