using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using controlLuces.Models;

namespace controlLuces.Controllers
{
    public class ArchivoController : Controller
    {
        SqlConnection con = new SqlConnection();

        void connectionString()
        {
            con.ConnectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
        }

        // Carpeta física donde se guardan los archivos
        private string ObtenerCarpetaBase()
        {
            string basePath = Server.MapPath("~/Uploads/Archivos");

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            return basePath;
        }

        // ======================= SUBIR / NUEVO ARCHIVO =======================
        [HttpGet]
        public ActionResult NuevoArchivo()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NuevoArchivo(HttpPostedFileBase archivo)
        {
            if (archivo == null || archivo.ContentLength == 0)
            {
                ViewBag.Error = "Debes seleccionar un archivo para cargar.";
                return View();
            }

            try
            {
                string carpeta = ObtenerCarpetaBase();

                string extension = Path.GetExtension(archivo.FileName).ToLower();
                string nombreBase = Path.GetFileNameWithoutExtension(archivo.FileName);

                // Determinar tipo de archivo
                string tipoArchivo = ObtenerTipoArchivo(extension);

                // Nombre único
                string nombreFinal = nombreBase + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                string rutaFisica = Path.Combine(carpeta, nombreFinal);

                // Guardar archivo físico
                archivo.SaveAs(rutaFisica);

                // Guardar en base de datos
                string rutaRelativa = "~/Uploads/Archivos/" + nombreFinal;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [lightcon_lumin].[archivos]
                        (NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga)
                        VALUES (@NombreArchivo, @RutaArchivo, @TipoArchivo, @FechaCarga)", cx))
                    {
                        cmd.Parameters.AddWithValue("@NombreArchivo", archivo.FileName);
                        cmd.Parameters.AddWithValue("@RutaArchivo", rutaRelativa);
                        cmd.Parameters.AddWithValue("@TipoArchivo", tipoArchivo);
                        cmd.Parameters.AddWithValue("@FechaCarga", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["MensajeExito"] = "El archivo se cargó correctamente.";
                return RedirectToAction("VerArchivo");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Ocurrió un error al guardar el archivo: " + ex.Message;
                return View();
            }
        }

        private string ObtenerTipoArchivo(string extension)
        {
            switch (extension)
            {
                case ".pdf":
                    return "PDF";
                case ".doc":
                case ".docx":
                    return "Word";
                case ".xls":
                case ".xlsx":
                    return "Excel";
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    return "Imagen";
                default:
                    return "Otro";
            }
        }

        // ======================= LISTAR ARCHIVOS =======================
        [HttpGet]
        public ActionResult VerArchivo(string busqueda, string tipo)
        {
            var archivos = new List<ArchivoModel>();

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();

                string query = @"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[archivos]
                    WHERE 1=1";

                if (!string.IsNullOrEmpty(busqueda))
                {
                    query += " AND NombreArchivo LIKE @Busqueda";
                }

                if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                {
                    query += " AND TipoArchivo = @Tipo";
                }

                query += " ORDER BY FechaCarga DESC";

                using (var cmd = new SqlCommand(query, cx))
                {
                    if (!string.IsNullOrEmpty(busqueda))
                    {
                        cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                    }

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                    {
                        cmd.Parameters.AddWithValue("@Tipo", tipo);
                    }

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            archivos.Add(new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;

            return View(archivos);
        }

        // ======================= MOSTRAR / DESCARGAR ARCHIVO =======================
        [HttpGet]
        public ActionResult MostrarArchivo(int id)
        {
            ArchivoModel archivo = null;

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[archivos]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            archivo = new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }

            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);

            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }

        // ======================= DESCARGAR ARCHIVO =======================
        [HttpGet]
        public ActionResult DescargarArchivo(int id)
        {
            ArchivoModel archivo = null;

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[archivos]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            archivo = new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }

            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);

            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType, archivo.NombreArchivo);
        }

        // ======================= ELIMINAR ARCHIVO =======================
        [HttpPost]
        public ActionResult EliminarArchivo(int id)
        {
            try
            {
                ArchivoModel archivo = null;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    // Obtener info del archivo
                    using (var cmd = new SqlCommand(@"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos]
                        WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                archivo = new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString()
                                };
                            }
                        }
                    }

                    if (archivo == null)
                    {
                        return Json(new { success = false, message = "Archivo no encontrado." });
                    }

                    // Eliminar de la base de datos
                    using (var cmd = new SqlCommand("DELETE FROM [lightcon_lumin].[archivos] WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }

                    // Eliminar archivo físico
                    string rutaFisica = Server.MapPath(archivo.RutaArchivo);
                    if (System.IO.File.Exists(rutaFisica))
                    {
                        System.IO.File.Delete(rutaFisica);
                    }
                }

                return Json(new { success = true, message = "Archivo eliminado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        // =====================================================================
        // =================== CONSUMOS RETILAP ================================
        // =====================================================================

        private string ObtenerCarpetaConsumos()
        {
            string basePath = Server.MapPath("~/Uploads/Consumos");
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            return basePath;
        }

        private UsuarioModel ObtenerUsuarioActual()
        {
            return Session["Usuario"] as UsuarioModel;
        }

        private bool EsAdminOAdminLocal()
        {
            var usuario = ObtenerUsuarioActual();
            return usuario != null && (usuario.IdRol == Rol.Administrador || usuario.IdRol == Rol.Administrador_Local);
        }

        // ======================= VER CONSUMOS =======================
        [HttpGet]
        public ActionResult VerConsumos(string busqueda, string tipo)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var archivos = new List<ArchivoModel>();

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    string query = @"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos_consumos]
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(busqueda))
                    {
                        query += " AND NombreArchivo LIKE @Busqueda";
                    }

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                    {
                        query += " AND TipoArchivo = @Tipo";
                    }

                    query += " ORDER BY FechaCarga DESC";

                    using (var cmd = new SqlCommand(query, cx))
                    {
                        if (!string.IsNullOrEmpty(busqueda))
                        {
                            cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                        }

                        if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                        {
                            cmd.Parameters.AddWithValue("@Tipo", tipo);
                        }

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                archivos.Add(new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    TipoArchivo = dr["TipoArchivo"].ToString(),
                                    FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Number == 208)
                {
                    ViewBag.TablaNoExiste = true;
                }
                else
                {
                    ViewBag.Error = "Error al consultar: " + ex.Message;
                }
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.EsAdmin = EsAdminOAdminLocal();

            return View(archivos);
        }

        // ======================= NUEVO CONSUMO =======================
        [HttpGet]
        public ActionResult NuevoConsumo()
        {
            if (!EsAdminOAdminLocal())
            {
                TempData["Error"] = "No tiene permisos para cargar archivos.";
                return RedirectToAction("VerConsumos");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NuevoConsumo(HttpPostedFileBase archivo)
        {
            if (!EsAdminOAdminLocal())
            {
                TempData["Error"] = "No tiene permisos para cargar archivos.";
                return RedirectToAction("VerConsumos");
            }

            if (archivo == null || archivo.ContentLength == 0)
            {
                ViewBag.Error = "Debes seleccionar un archivo para cargar.";
                return View();
            }

            try
            {
                string carpeta = ObtenerCarpetaConsumos();

                string extension = Path.GetExtension(archivo.FileName).ToLower();
                string nombreBase = Path.GetFileNameWithoutExtension(archivo.FileName);

                string tipoArchivo = ObtenerTipoArchivo(extension);

                string nombreFinal = nombreBase + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                string rutaFisica = Path.Combine(carpeta, nombreFinal);

                archivo.SaveAs(rutaFisica);

                string rutaRelativa = "~/Uploads/Consumos/" + nombreFinal;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [lightcon_lumin].[archivos_consumos]
                        (NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga)
                        VALUES (@NombreArchivo, @RutaArchivo, @TipoArchivo, @FechaCarga)", cx))
                    {
                        cmd.Parameters.AddWithValue("@NombreArchivo", archivo.FileName);
                        cmd.Parameters.AddWithValue("@RutaArchivo", rutaRelativa);
                        cmd.Parameters.AddWithValue("@TipoArchivo", tipoArchivo);
                        cmd.Parameters.AddWithValue("@FechaCarga", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["MensajeExito"] = "El archivo se cargó correctamente.";
                return RedirectToAction("VerConsumos");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Ocurrió un error al guardar el archivo: " + ex.Message;
                return View();
            }
        }

        // ======================= MOSTRAR CONSUMO =======================
        [HttpGet]
        public ActionResult MostrarConsumo(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = null;

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[archivos_consumos]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            archivo = new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }

            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);

            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }

        // ======================= DESCARGAR CONSUMO =======================
        [HttpGet]
        public ActionResult DescargarConsumo(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = null;

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[archivos_consumos]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            archivo = new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }

            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);

            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType, archivo.NombreArchivo);
        }

        // ======================= ELIMINAR CONSUMO =======================
        [HttpPost]
        public ActionResult EliminarConsumo(int id)
        {
            if (!EsAdminOAdminLocal())
            {
                return Json(new { success = false, message = "No tiene permisos para eliminar archivos." });
            }

            try
            {
                ArchivoModel archivo = null;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    using (var cmd = new SqlCommand(@"
                        SELECT Id, NombreArchivo, RutaArchivo
                        FROM [lightcon_lumin].[archivos_consumos]
                        WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                archivo = new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString()
                                };
                            }
                        }
                    }

                    if (archivo == null)
                    {
                        return Json(new { success = false, message = "Archivo no encontrado." });
                    }

                    using (var cmd = new SqlCommand("DELETE FROM [lightcon_lumin].[archivos_consumos] WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }

                    string rutaFisica = Server.MapPath(archivo.RutaArchivo);
                    if (System.IO.File.Exists(rutaFisica))
                    {
                        System.IO.File.Delete(rutaFisica);
                    }
                }

                return Json(new { success = true, message = "Archivo eliminado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        // ======================= DASHBOARD CONSUMOS =======================
        [HttpGet]
        public ActionResult DashboardConsumos(string busqueda, string tipo)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var archivos = new List<ArchivoModel>();

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    string query = @"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos_consumos]
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(busqueda))
                    {
                        query += " AND NombreArchivo LIKE @Busqueda";
                    }

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                    {
                        query += " AND TipoArchivo = @Tipo";
                    }

                    query += " ORDER BY FechaCarga DESC";

                    using (var cmd = new SqlCommand(query, cx))
                    {
                        if (!string.IsNullOrEmpty(busqueda))
                        {
                            cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                        }

                        if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                        {
                            cmd.Parameters.AddWithValue("@Tipo", tipo);
                        }

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                archivos.Add(new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    TipoArchivo = dr["TipoArchivo"].ToString(),
                                    FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Number == 208)
                {
                    ViewBag.TablaNoExiste = true;
                }
                else
                {
                    ViewBag.Error = "Error al consultar: " + ex.Message;
                }
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.EsAdmin = EsAdminOAdminLocal();

            return View(archivos);
        }

        // ======================= SUBIR CONSUMO AJAX =======================
        [HttpPost]
        public ActionResult SubirConsumoAjax(HttpPostedFileBase archivo)
        {
            if (!EsAdminOAdminLocal())
            {
                return Json(new { success = false, message = "No tiene permisos para cargar archivos." });
            }

            if (archivo == null || archivo.ContentLength == 0)
            {
                return Json(new { success = false, message = "Debes seleccionar un archivo para cargar." });
            }

            try
            {
                string carpeta = ObtenerCarpetaConsumos();

                string extension = Path.GetExtension(archivo.FileName).ToLower();
                string nombreBase = Path.GetFileNameWithoutExtension(archivo.FileName);

                string tipoArchivo = ObtenerTipoArchivo(extension);

                string nombreFinal = nombreBase + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                string rutaFisica = Path.Combine(carpeta, nombreFinal);

                archivo.SaveAs(rutaFisica);

                string rutaRelativa = "~/Uploads/Consumos/" + nombreFinal;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [lightcon_lumin].[archivos_consumos]
                        (NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga)
                        VALUES (@NombreArchivo, @RutaArchivo, @TipoArchivo, @FechaCarga)", cx))
                    {
                        cmd.Parameters.AddWithValue("@NombreArchivo", archivo.FileName);
                        cmd.Parameters.AddWithValue("@RutaArchivo", rutaRelativa);
                        cmd.Parameters.AddWithValue("@TipoArchivo", tipoArchivo);
                        cmd.Parameters.AddWithValue("@FechaCarga", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Archivo cargado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar el archivo: " + ex.Message });
            }
        }

        // =====================================================================
        // =================== FACTURACIÓN =====================================
        // =====================================================================

        private string ObtenerCarpetaFacturacion()
        {
            string basePath = Server.MapPath("~/Uploads/Facturacion");
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);
            return basePath;
        }

        [HttpGet]
        public ActionResult DashboardFacturacion(string busqueda, string tipo)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var archivos = new List<ArchivoModel>();

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    string query = @"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos_facturacion]
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(busqueda))
                        query += " AND NombreArchivo LIKE @Busqueda";

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                        query += " AND TipoArchivo = @Tipo";

                    query += " ORDER BY FechaCarga DESC";

                    using (var cmd = new SqlCommand(query, cx))
                    {
                        if (!string.IsNullOrEmpty(busqueda))
                            cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                        if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                            cmd.Parameters.AddWithValue("@Tipo", tipo);

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                archivos.Add(new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    TipoArchivo = dr["TipoArchivo"].ToString(),
                                    FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Number == 208)
                    ViewBag.TablaNoExiste = true;
                else
                    ViewBag.Error = "Error al consultar: " + ex.Message;
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.EsAdmin = EsAdminOAdminLocal();

            return View(archivos);
        }

        [HttpGet]
        public ActionResult MostrarFacturacion(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_facturacion");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }

        [HttpGet]
        public ActionResult DescargarFacturacion(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_facturacion");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType, archivo.NombreArchivo);
        }

        [HttpPost]
        public ActionResult EliminarFacturacion(int id)
        {
            return EliminarArchivoDeTabla(id, "archivos_facturacion");
        }

        [HttpPost]
        public ActionResult SubirFacturacionAjax(HttpPostedFileBase archivo)
        {
            return SubirArchivoATabla(archivo, "archivos_facturacion", ObtenerCarpetaFacturacion(), "~/Uploads/Facturacion/");
        }

        // =====================================================================
        // =================== RECAUDOS ========================================
        // =====================================================================

        private string ObtenerCarpetaRecaudos()
        {
            string basePath = Server.MapPath("~/Uploads/Recaudos");
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);
            return basePath;
        }

        [HttpGet]
        public ActionResult DashboardRecaudos(string busqueda, string tipo)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var archivos = new List<ArchivoModel>();

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    string query = @"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos_recaudos]
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(busqueda))
                        query += " AND NombreArchivo LIKE @Busqueda";

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                        query += " AND TipoArchivo = @Tipo";

                    query += " ORDER BY FechaCarga DESC";

                    using (var cmd = new SqlCommand(query, cx))
                    {
                        if (!string.IsNullOrEmpty(busqueda))
                            cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                        if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                            cmd.Parameters.AddWithValue("@Tipo", tipo);

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                archivos.Add(new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    TipoArchivo = dr["TipoArchivo"].ToString(),
                                    FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Number == 208)
                    ViewBag.TablaNoExiste = true;
                else
                    ViewBag.Error = "Error al consultar: " + ex.Message;
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.EsAdmin = EsAdminOAdminLocal();

            return View(archivos);
        }

        [HttpGet]
        public ActionResult MostrarRecaudo(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_recaudos");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }

        [HttpGet]
        public ActionResult DescargarRecaudo(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_recaudos");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType, archivo.NombreArchivo);
        }

        [HttpPost]
        public ActionResult EliminarRecaudo(int id)
        {
            return EliminarArchivoDeTabla(id, "archivos_recaudos");
        }

        [HttpPost]
        public ActionResult SubirRecaudoAjax(HttpPostedFileBase archivo)
        {
            return SubirArchivoATabla(archivo, "archivos_recaudos", ObtenerCarpetaRecaudos(), "~/Uploads/Recaudos/");
        }

        // =====================================================================
        // =================== PAGOS DE ENERGÍA ================================
        // =====================================================================

        private string ObtenerCarpetaPagosEnergia()
        {
            string basePath = Server.MapPath("~/Uploads/PagosEnergia");
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);
            return basePath;
        }

        [HttpGet]
        public ActionResult DashboardPagosEnergia(string busqueda, string tipo)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var archivos = new List<ArchivoModel>();

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    string query = @"
                        SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                        FROM [lightcon_lumin].[archivos_pagos_energia]
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(busqueda))
                        query += " AND NombreArchivo LIKE @Busqueda";

                    if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                        query += " AND TipoArchivo = @Tipo";

                    query += " ORDER BY FechaCarga DESC";

                    using (var cmd = new SqlCommand(query, cx))
                    {
                        if (!string.IsNullOrEmpty(busqueda))
                            cmd.Parameters.AddWithValue("@Busqueda", "%" + busqueda + "%");
                        if (!string.IsNullOrEmpty(tipo) && tipo != "Todos")
                            cmd.Parameters.AddWithValue("@Tipo", tipo);

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                archivos.Add(new ArchivoModel
                                {
                                    Id = Convert.ToInt32(dr["Id"]),
                                    NombreArchivo = dr["NombreArchivo"].ToString(),
                                    RutaArchivo = dr["RutaArchivo"].ToString(),
                                    TipoArchivo = dr["TipoArchivo"].ToString(),
                                    FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Number == 208)
                    ViewBag.TablaNoExiste = true;
                else
                    ViewBag.Error = "Error al consultar: " + ex.Message;
            }

            ViewBag.MensajeExito = TempData["MensajeExito"] as string;
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.EsAdmin = EsAdminOAdminLocal();

            return View(archivos);
        }

        [HttpGet]
        public ActionResult MostrarPagoEnergia(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_pagos_energia");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }

        [HttpGet]
        public ActionResult DescargarPagoEnergia(int id)
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            ArchivoModel archivo = ObtenerArchivoDeTabla(id, "archivos_pagos_energia");
            if (archivo == null)
                return HttpNotFound();

            string rutaFisica = Server.MapPath(archivo.RutaArchivo);
            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType, archivo.NombreArchivo);
        }

        [HttpPost]
        public ActionResult EliminarPagoEnergia(int id)
        {
            return EliminarArchivoDeTabla(id, "archivos_pagos_energia");
        }

        [HttpPost]
        public ActionResult SubirPagoEnergiaAjax(HttpPostedFileBase archivo)
        {
            return SubirArchivoATabla(archivo, "archivos_pagos_energia", ObtenerCarpetaPagosEnergia(), "~/Uploads/PagosEnergia/");
        }

        // =====================================================================
        // =================== MÉTODOS AUXILIARES GENÉRICOS ====================
        // =====================================================================

        private ArchivoModel ObtenerArchivoDeTabla(int id, string nombreTabla)
        {
            ArchivoModel archivo = null;
            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand($@"
                    SELECT Id, NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga
                    FROM [lightcon_lumin].[{nombreTabla}]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            archivo = new ArchivoModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                NombreArchivo = dr["NombreArchivo"].ToString(),
                                RutaArchivo = dr["RutaArchivo"].ToString(),
                                TipoArchivo = dr["TipoArchivo"].ToString(),
                                FechaCarga = dr["FechaCarga"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCarga"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }
            return archivo;
        }

        private ActionResult EliminarArchivoDeTabla(int id, string nombreTabla)
        {
            if (!EsAdminOAdminLocal())
                return Json(new { success = false, message = "No tiene permisos para eliminar archivos." });

            try
            {
                ArchivoModel archivo = ObtenerArchivoDeTabla(id, nombreTabla);
                if (archivo == null)
                    return Json(new { success = false, message = "Archivo no encontrado." });

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand($"DELETE FROM [lightcon_lumin].[{nombreTabla}] WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                string rutaFisica = Server.MapPath(archivo.RutaArchivo);
                if (System.IO.File.Exists(rutaFisica))
                    System.IO.File.Delete(rutaFisica);

                return Json(new { success = true, message = "Archivo eliminado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        private ActionResult SubirArchivoATabla(HttpPostedFileBase archivo, string nombreTabla, string carpetaFisica, string rutaRelativaBase)
        {
            if (!EsAdminOAdminLocal())
                return Json(new { success = false, message = "No tiene permisos para cargar archivos." });

            if (archivo == null || archivo.ContentLength == 0)
                return Json(new { success = false, message = "Debes seleccionar un archivo para cargar." });

            try
            {
                string extension = Path.GetExtension(archivo.FileName).ToLower();
                string nombreBase = Path.GetFileNameWithoutExtension(archivo.FileName);
                string tipoArchivo = ObtenerTipoArchivo(extension);

                string nombreFinal = nombreBase + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                string rutaFisica = Path.Combine(carpetaFisica, nombreFinal);

                archivo.SaveAs(rutaFisica);

                string rutaRelativa = rutaRelativaBase + nombreFinal;

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand($@"
                        INSERT INTO [lightcon_lumin].[{nombreTabla}]
                        (NombreArchivo, RutaArchivo, TipoArchivo, FechaCarga)
                        VALUES (@NombreArchivo, @RutaArchivo, @TipoArchivo, @FechaCarga)", cx))
                    {
                        cmd.Parameters.AddWithValue("@NombreArchivo", archivo.FileName);
                        cmd.Parameters.AddWithValue("@RutaArchivo", rutaRelativa);
                        cmd.Parameters.AddWithValue("@TipoArchivo", tipoArchivo);
                        cmd.Parameters.AddWithValue("@FechaCarga", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Archivo cargado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar el archivo: " + ex.Message });
            }
        }
    }
}
