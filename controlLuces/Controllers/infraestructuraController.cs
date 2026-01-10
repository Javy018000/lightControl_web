using controlLuces.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Text;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using ClosedXML.Excel;

namespace controlLuces.Controllers
{
    public class infraestructuraController : Controller
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
            return string.IsNullOrWhiteSpace(m) ? "Madrid" : m;
        }

        private const int CHIA_ID = 2;

        private static bool EsChiaNombre(string municipio)
        {
            if (string.IsNullOrWhiteSpace(municipio)) return false;
            var k = municipio.Trim().ToLowerInvariant();
            return k.Contains("chía") || k.Contains("chia") || k.Contains("chi\u00E1");
        }

        private static (double lat, double lng, int zoom) CentroPorMunicipio(string municipio)
        {
            var k = (municipio ?? "").Trim().ToLowerInvariant();
            if (k.Contains("chía") || k.Contains("chia")) return (4.85876, -74.05860, 14);
            if (k.Contains("madrid")) return (4.73200, -74.26400, 14);
            return (4.73200, -74.26400, 12);
        }

        [HttpGet]
        public async Task<ActionResult> ExportarTodoExcel()
        {
            try
            {
                var lista = new List<infraestructuraModel>();

                string cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

                using (SqlConnection cn = new SqlConnection(cs))
                using (SqlCommand cmd = new SqlCommand())
                {
                    await cn.OpenAsync();
                    cmd.Connection = cn;
                    cmd.CommandTimeout = 300;

                    // ===== FILTRO POR MUNICIPIO SEGÚN SESIÓN =====
                    var esLocal = EsAdminLocal();
                    var municipio = MunicipioSesion();

                    string sql = @"
                SELECT 
                    codigo, latitud, longitud, direccion, configuracion,
                    fabricante, linea, barrio, potencia, tipo, municipio, IdMunicipio
                FROM [lightcon_luminaria].[dbo].[infraestructura]";

                    if (esLocal)
                    {
                        if (EsChiaNombre(municipio))
                        {
                            sql += " WHERE IdMunicipio = @IdM";
                            cmd.Parameters.AddWithValue("@IdM", CHIA_ID);
                        }
                        else
                        {
                            sql += " WHERE municipio = @Municipio";
                            cmd.Parameters.AddWithValue("@Municipio", municipio);
                        }
                    }

                    sql += " ORDER BY municipio, tipo, codigo";

                    cmd.CommandText = sql;

                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            lista.Add(new infraestructuraModel
                            {
                                codigo = dr["codigo"]?.ToString() ?? "",
                                latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                                longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                                direccion = dr["direccion"]?.ToString() ?? "",
                                configuracion = dr["configuracion"]?.ToString() ?? "",
                                fabricante = dr["fabricante"]?.ToString() ?? "",
                                linea = dr["linea"]?.ToString() ?? "",
                                barrio = dr["barrio"]?.ToString() ?? "",
                                potencia = dr["potencia"]?.ToString() ?? "",
                                tipo = dr["tipo"]?.ToString() ?? "",
                                municipio = dr["municipio"]?.ToString() ?? "",
                                IdMunicipio = dr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(dr["IdMunicipio"]) : (int?)null
                            });
                        }
                    }
                }

                if (!lista.Any())
                {
                    return Content("No hay datos para exportar en la base de datos.");
                }

                // ===== GENERAR EXCEL CON CLOSEDXML =====
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Infraestructura");

                    // ENCABEZADOS
                    worksheet.Cell(1, 1).Value = "Código";
                    worksheet.Cell(1, 2).Value = "Tipo";
                    worksheet.Cell(1, 3).Value = "Municipio";
                    worksheet.Cell(1, 4).Value = "Barrio";
                    worksheet.Cell(1, 5).Value = "Dirección";
                    worksheet.Cell(1, 6).Value = "Latitud";
                    worksheet.Cell(1, 7).Value = "Longitud";
                    worksheet.Cell(1, 8).Value = "Línea";
                    worksheet.Cell(1, 9).Value = "Configuración";
                    worksheet.Cell(1, 10).Value = "Fabricante";
                    worksheet.Cell(1, 11).Value = "Potencia";
                    worksheet.Cell(1, 12).Value = "IdMunicipio";

                    // ESTILO ENCABEZADOS
                    var headerRange = worksheet.Range(1, 1, 1, 12);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontSize = 11;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 129, 189);
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // DATOS
                    int fila = 2;
                    foreach (var item in lista)
                    {
                        worksheet.Cell(fila, 1).Value = item.codigo;
                        worksheet.Cell(fila, 2).Value = item.tipo;
                        worksheet.Cell(fila, 3).Value = item.municipio;
                        worksheet.Cell(fila, 4).Value = item.barrio;
                        worksheet.Cell(fila, 5).Value = item.direccion;
                        worksheet.Cell(fila, 6).Value = item.latitud;
                        worksheet.Cell(fila, 7).Value = item.longitud;
                        worksheet.Cell(fila, 8).Value = item.linea;
                        worksheet.Cell(fila, 9).Value = item.configuracion;
                        worksheet.Cell(fila, 10).Value = item.fabricante;
                        worksheet.Cell(fila, 11).Value = item.potencia;
                        worksheet.Cell(fila, 12).Value = item.IdMunicipio?.ToString() ?? "";

                        // Alternar colores
                        if (fila % 2 == 0)
                        {
                            worksheet.Range(fila, 1, fila, 12).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
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
                        var nombreArchivo = $"Infraestructura_Completa_{fecha:yyyyMMdd_HHmmss}.xlsx";

                        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                    }
                }
            }
            catch (Exception ex)
            {
                return Content($"Error al exportar: {ex.Message}");
            }
        }
        // ===== MÉTODO PARA SUBIR IMÁGENES =====
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SubirImagenes()
        {
            try
            {
                var codigoElemento = Request.Form["codigoElemento"];
                var archivos = Request.Files;

                if (string.IsNullOrWhiteSpace(codigoElemento))
                {
                    return Json(new { ok = false, mensaje = "El código del elemento es requerido" });
                }

                if (archivos == null || archivos.Count == 0)
                {
                    return Json(new { ok = false, mensaje = "Debe seleccionar al menos una imagen" });
                }

                string cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

                using (SqlConnection conexion = new SqlConnection(cs))
                {
                    conexion.Open();

                    string sqlVerificar = "SELECT COUNT(*) FROM dbo.infraestructura WHERE codigo = @codigo";
                    using (SqlCommand cmdVerificar = new SqlCommand(sqlVerificar, conexion))
                    {
                        cmdVerificar.Parameters.AddWithValue("@codigo", codigoElemento);
                        cmdVerificar.CommandTimeout = 120;
                        int existe = (int)cmdVerificar.ExecuteScalar();

                        if (existe == 0)
                        {
                            return Json(new { ok = false, mensaje = "El código '" + codigoElemento + "' no existe en la infraestructura" });
                        }
                    }

                    string carpeta = Server.MapPath("~/Content/imagenes_elementos/");
                    if (!Directory.Exists(carpeta))
                    {
                        Directory.CreateDirectory(carpeta);
                    }

                    int imagenesSubidas = 0;
                    List<string> errores = new List<string>();

                    for (int i = 0; i < archivos.Count; i++)
                    {
                        var archivo = archivos[i];

                        if (archivo == null || archivo.ContentLength == 0)
                            continue;

                        string extension = Path.GetExtension(archivo.FileName).ToLower();
                        string[] extensionesPermitidas = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

                        if (!extensionesPermitidas.Contains(extension))
                        {
                            errores.Add(archivo.FileName + " - formato no permitido");
                            continue;
                        }

                        if (archivo.ContentLength > 5242880)
                        {
                            errores.Add(archivo.FileName + " - tamaño excede 5MB");
                            continue;
                        }

                        try
                        {
                            string nombre = Guid.NewGuid().ToString() + extension;
                            string rutaFisica = Path.Combine(carpeta, nombre);
                            string rutaBD = "/Content/imagenes_elementos/" + nombre;

                            archivo.SaveAs(rutaFisica);

                            string sqlInsert = @"INSERT INTO Imagenes_Elementos (CodigoElemento, RutaArchivo, FechaRegistro) 
                                               VALUES (@codigo, @ruta, GETDATE())";

                            using (SqlCommand cmdInsert = new SqlCommand(sqlInsert, conexion))
                            {
                                cmdInsert.CommandTimeout = 120;
                                cmdInsert.Parameters.AddWithValue("@codigo", codigoElemento);
                                cmdInsert.Parameters.AddWithValue("@ruta", rutaBD);
                                cmdInsert.ExecuteNonQuery();
                                imagenesSubidas++;
                            }
                        }
                        catch (Exception exArchivo)
                        {
                            errores.Add(archivo.FileName + " - " + exArchivo.Message);
                        }
                    }

                    string mensaje = imagenesSubidas + " imagen(es) subida(s) correctamente";
                    if (errores.Count > 0)
                    {
                        mensaje += ". Errores: " + string.Join(", ", errores);
                    }

                    return Json(new
                    {
                        ok = imagenesSubidas > 0,
                        mensaje = mensaje,
                        subidas = imagenesSubidas,
                        errores = errores.Count
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, mensaje = "Error del servidor: " + ex.Message });
            }
        }

        // ===== MÉTODO PARA OBTENER IMÁGENES =====
        private List<ImagenElementoModel> ObtenerImagenes(string codigo)
        {
            List<ImagenElementoModel> lista = new List<ImagenElementoModel>();

            if (string.IsNullOrWhiteSpace(codigo))
                return lista;

            string cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

            try
            {
                using (SqlConnection conexion = new SqlConnection(cs))
                {
                    conexion.Open();

                    string sql = @"SELECT IdImagen, CodigoElemento, RutaArchivo, FechaRegistro 
                                  FROM Imagenes_Elementos 
                                  WHERE CodigoElemento = @codigo
                                  ORDER BY FechaRegistro DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@codigo", codigo);
                        cmd.CommandTimeout = 120;

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                lista.Add(new ImagenElementoModel
                                {
                                    IdImagen = (int)dr["IdImagen"],
                                    CodigoElemento = dr["CodigoElemento"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    FechaRegistro = (DateTime)dr["FechaRegistro"]
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Retornar lista vacía en caso de error
            }

            return lista;
        }

        // ===== RESTO DE MÉTODOS EXISTENTES =====

        public async Task<ActionResult> BuscarInfraestructura(string tipoBusqueda, string codigo, string barrio, string municipio)
        {
            connectionString();
            await con.OpenAsync();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandTimeout = 120;

            var resultados = new List<infraestructuraModel>();

            try
            {
                string sql = null;

                switch (tipoBusqueda)
                {
                    case "codigo":
                        sql = "SELECT * FROM dbo.infraestructura WHERE codigo = @codigo";
                        com.Parameters.AddWithValue("@codigo", codigo ?? "");
                        break;

                    case "barrio":
                        sql = "SELECT * FROM dbo.infraestructura WHERE barrio = @barrio";
                        com.Parameters.AddWithValue("@barrio", barrio ?? "");
                        break;

                    default:
                        con.Close();
                        return View("infraestructura", resultados);
                }

                if (EsAdminLocal())
                {
                    var muni = MunicipioSesion();
                    if (EsChiaNombre(muni))
                    {
                        sql += " AND IdMunicipio = @IdM";
                        com.Parameters.AddWithValue("@IdM", CHIA_ID);
                    }
                    else
                    {
                        sql += " AND municipio = @Municipio";
                        com.Parameters.AddWithValue("@Municipio", muni);
                    }
                }

                com.CommandText = sql;

                using (var dr = await com.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        resultados.Add(new infraestructuraModel
                        {
                            codigo = dr["codigo"]?.ToString() ?? "",
                            latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                            longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                            direccion = dr["direccion"]?.ToString() ?? "",
                            configuracion = dr["configuracion"]?.ToString() ?? "",
                            fabricante = dr["fabricante"]?.ToString() ?? "",
                            linea = dr["linea"]?.ToString() ?? "",
                            barrio = dr["barrio"]?.ToString() ?? "",
                            potencia = dr["potencia"]?.ToString() ?? "",
                            tipo = dr["tipo"]?.ToString() ?? "",
                            municipio = dr["municipio"]?.ToString() ?? "",
                            IdMunicipio = dr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(dr["IdMunicipio"]) : (int?)null
                        });
                    }
                }
                con.Close();
            }
            catch { }

            return View("infraestructura", resultados);
        }

        public ActionResult verinfraestructura() => View();
        public ActionResult infraestructura() => View();

        public async Task<ActionResult> VerInfo(string id)
        {
            var infra = new infraestructuraModel();
            connectionString();
            await con.OpenAsync();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandTimeout = 120;

            var sql = "SELECT * FROM dbo.infraestructura WHERE codigo = @id";
            if (EsAdminLocal())
            {
                var muni = MunicipioSesion();
                if (EsChiaNombre(muni))
                {
                    sql += " AND IdMunicipio = @IdM";
                    com.Parameters.AddWithValue("@IdM", CHIA_ID);
                }
                else
                {
                    sql += " AND municipio = @Municipio";
                    com.Parameters.AddWithValue("@Municipio", muni);
                }
            }
            com.CommandText = sql;
            com.Parameters.AddWithValue("@id", id ?? "");

            try
            {
                using (var dr = await com.ExecuteReaderAsync())
                {
                    if (await dr.ReadAsync())
                    {
                        infra.codigo = dr["codigo"]?.ToString() ?? "";
                        infra.latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0;
                        infra.longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0;
                        infra.barrio = dr["barrio"]?.ToString() ?? "";
                        infra.direccion = dr["direccion"]?.ToString() ?? "";
                        infra.configuracion = dr["configuracion"]?.ToString() ?? "";
                        infra.linea = dr["linea"]?.ToString() ?? "";
                        infra.potencia = dr["potencia"]?.ToString() ?? "";
                        infra.fabricante = dr["fabricante"]?.ToString() ?? "";
                        infra.tipo = dr["tipo"]?.ToString() ?? "";
                        infra.municipio = dr["municipio"]?.ToString() ?? "";
                        infra.IdMunicipio = dr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(dr["IdMunicipio"]) : (int?)null;
                    }
                }
                con.Close();
            }
            catch { }

            return View(infra);
        }

        public async Task<ActionResult> vertodainfraestructura()
        {
            connectionString();
            await con.OpenAsync();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandTimeout = 120;

            var esLocal = EsAdminLocal();
            var municipio = MunicipioSesion();

            ViewBag.EsAdminLocal = esLocal;
            ViewBag.MunicipioSesion = municipio;
            var c = CentroPorMunicipio(municipio);
            ViewBag.MapaLat = c.lat;
            ViewBag.MapaLng = c.lng;
            ViewBag.MapaZoom = c.zoom;

            string sql = "SELECT * FROM dbo.infraestructura";
            if (esLocal)
            {
                if (EsChiaNombre(municipio))
                {
                    sql += " WHERE IdMunicipio = @IdM";
                    com.Parameters.AddWithValue("@IdM", CHIA_ID);
                }
                else
                {
                    sql += " WHERE municipio = @Municipio";
                    com.Parameters.AddWithValue("@Municipio", municipio);
                }
            }
            com.CommandText = sql;

            var infraestructuraList = new List<infraestructuraModel>();
            using (var dr = await com.ExecuteReaderAsync())
            {
                while (await dr.ReadAsync())
                {
                    infraestructuraList.Add(new infraestructuraModel
                    {
                        codigo = dr["codigo"] != DBNull.Value ? dr["codigo"].ToString() : string.Empty,
                        latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                        longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                        direccion = dr["direccion"] != DBNull.Value ? dr["direccion"].ToString() : string.Empty,
                        configuracion = dr["configuracion"] != DBNull.Value ? dr["configuracion"].ToString() : string.Empty,
                        fabricante = dr["fabricante"] != DBNull.Value ? dr["fabricante"].ToString() : string.Empty,
                        linea = dr["linea"] != DBNull.Value ? dr["linea"].ToString() : string.Empty,
                        barrio = dr["barrio"] != DBNull.Value ? dr["barrio"].ToString() : string.Empty,
                        potencia = dr["potencia"] != DBNull.Value ? dr["potencia"].ToString() : string.Empty,
                        tipo = dr["tipo"] != DBNull.Value ? dr["tipo"].ToString() : string.Empty,
                        municipio = dr["municipio"] != DBNull.Value ? dr["municipio"].ToString() : string.Empty,
                        IdMunicipio = dr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(dr["IdMunicipio"]) : (int?)null
                    });
                }
            }
            con.Close();

            List<string> municipios;
            if (esLocal)
                municipios = new List<string> { municipio };
            else
                municipios = infraestructuraList
                    .Where(i => !string.IsNullOrEmpty(i.municipio))
                    .Select(i => i.municipio)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();
            ViewBag.Municipios = municipios;

            var infraestructuraListOrdenada = infraestructuraList
                .OrderByDescending(i => i.codigo)
                .ToList();

            return View(infraestructuraListOrdenada);
        }

        public async Task<ActionResult> VerInfoDetallada(string id)
        {
            connectionString();
            await con.OpenAsync();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandTimeout = 120;

            var sql = "SELECT * FROM dbo.infraestructura WHERE codigo = @codigo";
            if (EsAdminLocal())
            {
                var muni = MunicipioSesion();
                if (EsChiaNombre(muni))
                {
                    sql += " AND IdMunicipio = @IdM";
                    com.Parameters.AddWithValue("@IdM", CHIA_ID);
                }
                else
                {
                    sql += " AND municipio = @Municipio";
                    com.Parameters.AddWithValue("@Municipio", muni);
                }
            }

            com.CommandText = sql;
            com.Parameters.AddWithValue("@codigo", id ?? "");

            infraestructuraModel infraestructura = null;

            using (var dr = await com.ExecuteReaderAsync())
            {
                if (await dr.ReadAsync())
                {
                    infraestructura = new infraestructuraModel
                    {
                        codigo = dr["codigo"] != DBNull.Value ? dr["codigo"].ToString() : string.Empty,
                        latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                        longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                        direccion = dr["direccion"] != DBNull.Value ? dr["direccion"].ToString() : string.Empty,
                        configuracion = dr["configuracion"] != DBNull.Value ? dr["configuracion"].ToString() : string.Empty,
                        fabricante = dr["fabricante"] != DBNull.Value ? dr["fabricante"].ToString() : string.Empty,
                        linea = dr["linea"] != DBNull.Value ? dr["linea"].ToString() : string.Empty,
                        barrio = dr["barrio"] != DBNull.Value ? dr["barrio"].ToString() : string.Empty,
                        potencia = dr["potencia"] != DBNull.Value ? dr["potencia"].ToString() : string.Empty,
                        tipo = dr["tipo"] != DBNull.Value ? dr["tipo"].ToString() : string.Empty,
                        municipio = dr["municipio"] != DBNull.Value ? dr["municipio"].ToString() : string.Empty,
                        IdMunicipio = dr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(dr["IdMunicipio"]) : (int?)null
                    };
                }
            }

            con.Close();

            if (infraestructura != null)
            {
                ViewBag.Imagenes = ObtenerImagenes(infraestructura.codigo);
            }

            return View("verinfodetallada", infraestructura);
        }

        public async Task<ActionResult> Transformadores(string codigo = null)
        {
            var lista = new List<TransformadorModel>();
            var cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

            using (var cn = new SqlConnection(cs))
            using (var cmd = new SqlCommand())
            {
                await cn.OpenAsync();
                cmd.Connection = cn;
                cmd.Parameters.Clear();
                cmd.CommandTimeout = 120;

                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    cmd.CommandText = @"SELECT TOP 5000 * 
                                FROM dbo.transformadores 
                                WHERE codigo_apoyo = @c 
                                ORDER BY id DESC";
                    cmd.Parameters.AddWithValue("@c", codigo);
                }
                else
                {
                    cmd.CommandText = @"SELECT TOP 5000 * 
                                FROM dbo.transformadores 
                                ORDER BY id DESC";
                }

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        lista.Add(new TransformadorModel
                        {
                            id = (int)rdr["id"],
                            codigo_apoyo = rdr["codigo_apoyo"] as string,
                            pintado_apoyo = rdr["pintado_apoyo"] as string,
                            latitud = rdr["latitud"] != DBNull.Value ? Convert.ToSingle(rdr["latitud"]) : 0,
                            longitud = rdr["longitud"] != DBNull.Value ? Convert.ToSingle(rdr["longitud"]) : 0,
                            nombre_estructura = rdr["nombre_estructura"] as string,
                            tipo_fase = rdr["tipo_fase"] as string,
                            potencia_kva = rdr["potencia_kva"] as string,
                            fabricante = rdr["fabricante"] as string,
                            observaciones = rdr["observaciones"] as string
                        });
                    }
                }
            }

            return View("Transformadores", lista);
        }
        [HttpGet]
        public async Task<ActionResult> ExportarTransformadoresExcel()
        {
            try
            {
                var lista = new List<TransformadorModel>();

                string cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

                using (SqlConnection cn = new SqlConnection(cs))
                using (SqlCommand cmd = new SqlCommand())
                {
                    await cn.OpenAsync();
                    cmd.Connection = cn;
                    cmd.CommandTimeout = 300;

                    string sql = @"
                SELECT 
                    id, codigo_apoyo, pintado_apoyo, latitud, longitud, 
                    nombre_estructura, tipo_fase, potencia_kva, fabricante, observaciones
                FROM [lightcon_luminaria].[dbo].[transformadores]
                ORDER BY id DESC";

                    cmd.CommandText = sql;

                    using (SqlDataReader dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            lista.Add(new TransformadorModel
                            {
                                id = dr["id"] != DBNull.Value ? Convert.ToInt32(dr["id"]) : 0,
                                codigo_apoyo = dr["codigo_apoyo"]?.ToString() ?? "",
                                pintado_apoyo = dr["pintado_apoyo"]?.ToString() ?? "",
                                latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                                longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                                nombre_estructura = dr["nombre_estructura"]?.ToString() ?? "",
                                tipo_fase = dr["tipo_fase"]?.ToString() ?? "",
                                potencia_kva = dr["potencia_kva"]?.ToString() ?? "",
                                fabricante = dr["fabricante"]?.ToString() ?? "",
                                observaciones = dr["observaciones"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                if (!lista.Any())
                {
                    return Content("No hay transformadores para exportar en la base de datos.");
                }

                // ===== GENERAR EXCEL CON CLOSEDXML =====
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Transformadores");

                    // ENCABEZADOS
                    worksheet.Cell(1, 1).Value = "ID";
                    worksheet.Cell(1, 2).Value = "Código Apoyo";
                    worksheet.Cell(1, 3).Value = "Pintado Apoyo";
                    worksheet.Cell(1, 4).Value = "Latitud";
                    worksheet.Cell(1, 5).Value = "Longitud";
                    worksheet.Cell(1, 6).Value = "Nombre Estructura";
                    worksheet.Cell(1, 7).Value = "Tipo/Fase";
                    worksheet.Cell(1, 8).Value = "Potencia (KVA)";
                    worksheet.Cell(1, 9).Value = "Fabricante";
                    worksheet.Cell(1, 10).Value = "Observaciones";

                    // ESTILO ENCABEZADOS
                    var headerRange = worksheet.Range(1, 1, 1, 10);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontSize = 11;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 129, 189);
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // DATOS
                    int fila = 2;
                    foreach (var item in lista)
                    {
                        worksheet.Cell(fila, 1).Value = item.id;
                        worksheet.Cell(fila, 2).Value = item.codigo_apoyo;
                        worksheet.Cell(fila, 3).Value = item.pintado_apoyo;
                        worksheet.Cell(fila, 4).Value = item.latitud;
                        worksheet.Cell(fila, 5).Value = item.longitud;
                        worksheet.Cell(fila, 6).Value = item.nombre_estructura;
                        worksheet.Cell(fila, 7).Value = item.tipo_fase;
                        worksheet.Cell(fila, 8).Value = item.potencia_kva;
                        worksheet.Cell(fila, 9).Value = item.fabricante;
                        worksheet.Cell(fila, 10).Value = item.observaciones;

                        // Alternar colores
                        if (fila % 2 == 0)
                        {
                            worksheet.Range(fila, 1, fila, 10).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
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
                        var nombreArchivo = $"Transformadores_Completo_{fecha:yyyyMMdd_HHmmss}.xlsx";

                        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                    }
                }
            }
            catch (Exception ex)
            {
                return Content($"Error al exportar transformadores: {ex.Message}");
            }
        }
        public async Task<ActionResult> VerTransformador(int id)
        {
            TransformadorModel t = null;
            var cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

            using (var cn = new SqlConnection(cs))
            using (var cmd = new SqlCommand("SELECT * FROM dbo.transformadores WHERE id=@id", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.CommandTimeout = 120;
                await cn.OpenAsync();

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        t = new TransformadorModel
                        {
                            id = id,
                            codigo_apoyo = rdr["codigo_apoyo"] as string,
                            pintado_apoyo = rdr["pintado_apoyo"] as string,
                            latitud = rdr["latitud"] != DBNull.Value ? Convert.ToSingle(rdr["latitud"]) : 0,
                            longitud = rdr["longitud"] != DBNull.Value ? Convert.ToSingle(rdr["longitud"]) : 0,
                            nombre_estructura = rdr["nombre_estructura"] as string,
                            tipo_fase = rdr["tipo_fase"] as string,
                            potencia_kva = rdr["potencia_kva"] as string,
                            fabricante = rdr["fabricante"] as string,
                            observaciones = rdr["observaciones"] as string
                        };
                    }
                }
            }

            if (t == null) return HttpNotFound();
            return View("VerTransformador", t);
        }

        [HttpGet]
        public async Task<ActionResult> verinfraestructura_local(string tipo = null)
        {
            bool esAdminLocal = (Session["EsAdminLocal"] as bool?) ?? false;
            string municipio = Session["Municipio"] as string;

            if (!esAdminLocal || string.IsNullOrWhiteSpace(municipio))
            {
                return RedirectToAction("vertodainfraestructura");
            }

            connectionString();
            await con.OpenAsync();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandTimeout = 120;

            if (EsChiaNombre(municipio))
            {
                if (string.IsNullOrWhiteSpace(tipo))
                {
                    com.CommandText = "SELECT * FROM dbo.infraestructura WHERE IdMunicipio = @IdM ORDER BY codigo";
                }
                else
                {
                    com.CommandText = "SELECT * FROM dbo.infraestructura WHERE IdMunicipio = @IdM AND tipo = @T ORDER BY codigo";
                    com.Parameters.AddWithValue("@T", tipo);
                }
                com.Parameters.AddWithValue("@IdM", CHIA_ID);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tipo))
                {
                    com.CommandText = "SELECT * FROM dbo.infraestructura WHERE municipio = @M ORDER BY codigo";
                }
                else
                {
                    com.CommandText = "SELECT * FROM dbo.infraestructura WHERE municipio = @M AND tipo = @T ORDER BY codigo";
                    com.Parameters.AddWithValue("@T", tipo);
                }
                com.Parameters.AddWithValue("@M", municipio);
            }

            var lista = new List<infraestructuraModel>();
            using (var rdr = await com.ExecuteReaderAsync())
            {
                while (await rdr.ReadAsync())
                {
                    lista.Add(new infraestructuraModel
                    {
                        codigo = rdr["codigo"]?.ToString() ?? "",
                        latitud = rdr["latitud"] != DBNull.Value ? Convert.ToSingle(rdr["latitud"]) : 0,
                        longitud = rdr["longitud"] != DBNull.Value ? Convert.ToSingle(rdr["longitud"]) : 0,
                        direccion = rdr["direccion"]?.ToString() ?? "",
                        configuracion = rdr["configuracion"]?.ToString() ?? "",
                        fabricante = rdr["fabricante"]?.ToString() ?? "",
                        linea = rdr["linea"]?.ToString() ?? "",
                        barrio = rdr["barrio"]?.ToString() ?? "",
                        potencia = rdr["potencia"]?.ToString() ?? "",
                        tipo = rdr["tipo"]?.ToString() ?? "",
                        municipio = rdr["municipio"]?.ToString() ?? "",
                        IdMunicipio = rdr["IdMunicipio"] != DBNull.Value ? Convert.ToInt32(rdr["IdMunicipio"]) : (int?)null
                    });
                }
            }
            con.Close();

            ViewBag.EsAdminLocal = true;
            ViewBag.MunicipioSesion = municipio;
            var c2 = CentroPorMunicipio(municipio);
            ViewBag.MapaLat = c2.lat;
            ViewBag.MapaLng = c2.lng;
            ViewBag.MapaZoom = c2.zoom;
            ViewBag.Municipios = new List<string> { municipio };

            return View("infraestructura_local", lista);
        }

        [HttpGet]
        public ActionResult CrearTransformador()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CrearTransformador(TransformadorModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var cs = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

            using (var cn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
        INSERT INTO dbo.transformadores
        (codigo_apoyo, pintado_apoyo, latitud, longitud, nombre_estructura, tipo_fase, potencia_kva, fabricante, observaciones)
        VALUES
        (@codigo, @pintado, @lat, @lon, @nombre, @fase, @potencia, @fabricante, @obs);
        SELECT SCOPE_IDENTITY();", cn))
            {
                cmd.Parameters.AddWithValue("@codigo", (object)model.codigo_apoyo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pintado", (object)model.pintado_apoyo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lat", model.latitud);
                cmd.Parameters.AddWithValue("@lon", model.longitud);
                cmd.Parameters.AddWithValue("@nombre", (object)model.nombre_estructura ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fase", (object)model.tipo_fase ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@potencia", (object)model.potencia_kva ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fabricante", (object)model.fabricante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)model.observaciones ?? DBNull.Value);

                cmd.CommandTimeout = 120;
                await cn.OpenAsync();
                var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return RedirectToAction("VerTransformador", new { id = newId });
            }
        }
    }
}