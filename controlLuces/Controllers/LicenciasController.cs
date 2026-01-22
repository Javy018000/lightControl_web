using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using controlLuces.Models;
using controlLuces.permisos;

namespace controlLuces.Controllers
{
    [PermisosRol(Rol.Administrador, Rol.Administrador_Local)]
    public class LicenciasController : Controller
    {
        SqlConnection con = new SqlConnection();

        void connectionString()
        {
            con.ConnectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
        }

        // Obtener usuario actual de sesión
        private UsuarioModel ObtenerUsuarioActual()
        {
            return Session["Usuario"] as UsuarioModel;
        }

        // Verificar si es administrador global (sin municipio)
        private bool EsAdminGlobal()
        {
            var usuario = ObtenerUsuarioActual();
            return usuario != null && usuario.IdRol == Rol.Administrador;
        }

        // Obtener municipio del usuario actual
        private string ObtenerMunicipioActual()
        {
            var usuario = ObtenerUsuarioActual();
            return usuario?.Municipio;
        }

        // Verificar si el usuario puede acceder a una licencia específica
        private bool PuedeAccederLicencia(string municipioLicencia)
        {
            if (EsAdminGlobal()) return true;
            var miMunicipio = ObtenerMunicipioActual();
            return !string.IsNullOrEmpty(miMunicipio) &&
                   miMunicipio.Equals(municipioLicencia, StringComparison.OrdinalIgnoreCase);
        }

        // ======================= LISTAR LICENCIAS =======================
        [HttpGet]
        public ActionResult Index()
        {
            var usuario = ObtenerUsuarioActual();
            if (usuario == null)
                return RedirectToAction("Login", "Login");

            var licencias = new List<LicenciaModel>();
            bool esAdminGlobal = EsAdminGlobal();
            string miMunicipio = ObtenerMunicipioActual();

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();

                string query = @"
                    SELECT Id, ClaveLicencia, NombreCliente, Municipio, NIT, Contacto, Email, Telefono,
                           FechaInicio, FechaExpiracion, MaxUsuarios, Estado, ModulosPermitidos,
                           FechaCreacion, FechaUltimaValidacion, Observaciones
                    FROM [lightcon_lumin].[licencias]";

                // Si es Admin_Local, filtrar solo su municipio
                if (!esAdminGlobal && !string.IsNullOrEmpty(miMunicipio))
                {
                    query += " WHERE Municipio = @Municipio";
                }

                query += " ORDER BY FechaCreacion DESC";

                using (var cmd = new SqlCommand(query, cx))
                {
                    if (!esAdminGlobal && !string.IsNullOrEmpty(miMunicipio))
                    {
                        cmd.Parameters.AddWithValue("@Municipio", miMunicipio);
                    }

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            licencias.Add(MapearLicencia(dr));
                        }
                    }
                }
            }

            ViewBag.EsAdminGlobal = esAdminGlobal;
            ViewBag.MiMunicipio = miMunicipio;

            return View(licencias);
        }

        // ======================= CREAR LICENCIA (Solo Admin Global) =======================
        [HttpGet]
        public ActionResult Crear()
        {
            // Solo Admin global puede crear nuevas licencias
            if (!EsAdminGlobal())
            {
                TempData["Error"] = "Solo el administrador global puede crear nuevas licencias.";
                return RedirectToAction("Index");
            }

            var modelo = new LicenciaModel
            {
                FechaInicio = DateTime.Now,
                FechaExpiracion = DateTime.Now.AddYears(1),
                MaxUsuarios = 10,
                Estado = "Activa",
                ModulosPermitidos = "PQRS,Ordenes,Infraestructura,Postes,Cajas,Archivos,Consumos,Usuarios"
            };
            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(LicenciaModel modelo)
        {
            // Solo Admin global puede crear nuevas licencias
            if (!EsAdminGlobal())
            {
                TempData["Error"] = "Solo el administrador global puede crear nuevas licencias.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(modelo.Municipio) || string.IsNullOrEmpty(modelo.NombreCliente))
            {
                ViewBag.Error = "El municipio y nombre del cliente son obligatorios.";
                return View(modelo);
            }

            try
            {
                // Generar clave de licencia única
                modelo.ClaveLicencia = GenerarClaveLicencia(modelo.Municipio);

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [lightcon_lumin].[licencias]
                        (ClaveLicencia, NombreCliente, Municipio, NIT, Contacto, Email, Telefono,
                         FechaInicio, FechaExpiracion, MaxUsuarios, Estado, ModulosPermitidos, Observaciones)
                        VALUES
                        (@ClaveLicencia, @NombreCliente, @Municipio, @NIT, @Contacto, @Email, @Telefono,
                         @FechaInicio, @FechaExpiracion, @MaxUsuarios, @Estado, @ModulosPermitidos, @Observaciones)", cx))
                    {
                        cmd.Parameters.AddWithValue("@ClaveLicencia", modelo.ClaveLicencia);
                        cmd.Parameters.AddWithValue("@NombreCliente", modelo.NombreCliente);
                        cmd.Parameters.AddWithValue("@Municipio", modelo.Municipio);
                        cmd.Parameters.AddWithValue("@NIT", (object)modelo.NIT ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Contacto", (object)modelo.Contacto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", (object)modelo.Email ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Telefono", (object)modelo.Telefono ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FechaInicio", modelo.FechaInicio);
                        cmd.Parameters.AddWithValue("@FechaExpiracion", modelo.FechaExpiracion);
                        cmd.Parameters.AddWithValue("@MaxUsuarios", modelo.MaxUsuarios);
                        cmd.Parameters.AddWithValue("@Estado", modelo.Estado ?? "Activa");
                        cmd.Parameters.AddWithValue("@ModulosPermitidos", (object)modelo.ModulosPermitidos ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Observaciones", (object)modelo.Observaciones ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["MensajeExito"] = "Licencia creada exitosamente. Clave: " + modelo.ClaveLicencia;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al crear licencia: " + ex.Message;
                return View(modelo);
            }
        }

        // ======================= EDITAR LICENCIA =======================
        [HttpGet]
        public ActionResult Editar(int id)
        {
            LicenciaModel licencia = ObtenerLicenciaPorId(id);
            if (licencia == null)
                return HttpNotFound();

            // Verificar permisos
            if (!PuedeAccederLicencia(licencia.Municipio))
            {
                TempData["Error"] = "No tiene permisos para editar esta licencia.";
                return RedirectToAction("Index");
            }

            ViewBag.EsAdminGlobal = EsAdminGlobal();
            return View(licencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(int id, LicenciaModel modelo)
        {
            // Verificar permisos
            var licenciaOriginal = ObtenerLicenciaPorId(id);
            if (licenciaOriginal == null || !PuedeAccederLicencia(licenciaOriginal.Municipio))
            {
                TempData["Error"] = "No tiene permisos para editar esta licencia.";
                return RedirectToAction("Index");
            }

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [lightcon_lumin].[licencias]
                        SET NombreCliente = @NombreCliente,
                            NIT = @NIT,
                            Contacto = @Contacto,
                            Email = @Email,
                            Telefono = @Telefono,
                            FechaExpiracion = @FechaExpiracion,
                            MaxUsuarios = @MaxUsuarios,
                            Estado = @Estado,
                            ModulosPermitidos = @ModulosPermitidos,
                            Observaciones = @Observaciones
                        WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@NombreCliente", modelo.NombreCliente);
                        cmd.Parameters.AddWithValue("@NIT", (object)modelo.NIT ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Contacto", (object)modelo.Contacto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", (object)modelo.Email ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Telefono", (object)modelo.Telefono ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FechaExpiracion", modelo.FechaExpiracion);
                        cmd.Parameters.AddWithValue("@MaxUsuarios", modelo.MaxUsuarios);
                        cmd.Parameters.AddWithValue("@Estado", modelo.Estado);
                        cmd.Parameters.AddWithValue("@ModulosPermitidos", (object)modelo.ModulosPermitidos ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Observaciones", (object)modelo.Observaciones ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["MensajeExito"] = "Licencia actualizada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al actualizar: " + ex.Message;
                ViewBag.EsAdminGlobal = EsAdminGlobal();
                return View(modelo);
            }
        }

        // ======================= SUSPENDER/ACTIVAR LICENCIA =======================
        [HttpPost]
        public ActionResult CambiarEstado(int id, string estado)
        {
            try
            {
                var licencia = ObtenerLicenciaPorId(id);
                if (licencia == null || !PuedeAccederLicencia(licencia.Municipio))
                {
                    return Json(new { success = false, message = "No tiene permisos para esta acción." });
                }

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [lightcon_lumin].[licencias]
                        SET Estado = @Estado
                        WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Estado", estado);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Estado actualizado a: " + estado });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ======================= RENOVAR LICENCIA =======================
        [HttpPost]
        public ActionResult Renovar(int id, int meses)
        {
            try
            {
                var licencia = ObtenerLicenciaPorId(id);
                if (licencia == null || !PuedeAccederLicencia(licencia.Municipio))
                {
                    return Json(new { success = false, message = "No tiene permisos para esta acción." });
                }

                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [lightcon_lumin].[licencias]
                        SET FechaExpiracion = DATEADD(MONTH, @Meses,
                            CASE WHEN FechaExpiracion < GETDATE() THEN GETDATE() ELSE FechaExpiracion END),
                            Estado = 'Activa'
                        WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Meses", meses);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Licencia renovada por " + meses + " meses." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ======================= ELIMINAR LICENCIA (Solo Admin Global) =======================
        [HttpPost]
        public ActionResult Eliminar(int id)
        {
            // Solo Admin global puede eliminar licencias
            if (!EsAdminGlobal())
            {
                return Json(new { success = false, message = "Solo el administrador global puede eliminar licencias." });
            }

            try
            {
                connectionString();
                using (var cx = new SqlConnection(con.ConnectionString))
                {
                    cx.Open();

                    // Primero eliminar historial
                    using (var cmd = new SqlCommand("DELETE FROM [lightcon_lumin].[licencias_historial] WHERE IdLicencia = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }

                    // Luego eliminar licencia
                    using (var cmd = new SqlCommand("DELETE FROM [lightcon_lumin].[licencias] WHERE Id = @Id", cx))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Licencia eliminada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ======================= VER HISTORIAL =======================
        [HttpGet]
        public ActionResult Historial(int id)
        {
            var licencia = ObtenerLicenciaPorId(id);
            if (licencia == null || !PuedeAccederLicencia(licencia.Municipio))
            {
                TempData["Error"] = "No tiene permisos para ver este historial.";
                return RedirectToAction("Index");
            }

            var historial = new List<LicenciaHistorialModel>();

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT h.Id, h.IdLicencia, h.FechaValidacion, h.IP, h.Usuario, h.Resultado, h.Detalle
                    FROM [lightcon_lumin].[licencias_historial] h
                    WHERE h.IdLicencia = @Id
                    ORDER BY h.FechaValidacion DESC", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            historial.Add(new LicenciaHistorialModel
                            {
                                Id = Convert.ToInt32(dr["Id"]),
                                IdLicencia = Convert.ToInt32(dr["IdLicencia"]),
                                FechaValidacion = Convert.ToDateTime(dr["FechaValidacion"]),
                                IP = dr["IP"].ToString(),
                                Usuario = dr["Usuario"].ToString(),
                                Resultado = dr["Resultado"].ToString(),
                                Detalle = dr["Detalle"].ToString()
                            });
                        }
                    }
                }
            }

            ViewBag.IdLicencia = id;
            ViewBag.Municipio = licencia.Municipio;
            return View(historial);
        }

        // ======================= MÉTODOS AUXILIARES =======================
        private string GenerarClaveLicencia(string municipio)
        {
            string año = DateTime.Now.Year.ToString();
            string guid = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            return $"{municipio.ToUpper().Replace(" ", "")}-{año}-{guid}";
        }

        private LicenciaModel ObtenerLicenciaPorId(int id)
        {
            LicenciaModel licencia = null;

            connectionString();
            using (var cx = new SqlConnection(con.ConnectionString))
            {
                cx.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, ClaveLicencia, NombreCliente, Municipio, NIT, Contacto, Email, Telefono,
                           FechaInicio, FechaExpiracion, MaxUsuarios, Estado, ModulosPermitidos,
                           FechaCreacion, FechaUltimaValidacion, Observaciones
                    FROM [lightcon_lumin].[licencias]
                    WHERE Id = @Id", cx))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            licencia = MapearLicencia(dr);
                        }
                    }
                }
            }

            return licencia;
        }

        private LicenciaModel MapearLicencia(SqlDataReader dr)
        {
            return new LicenciaModel
            {
                Id = Convert.ToInt32(dr["Id"]),
                ClaveLicencia = dr["ClaveLicencia"].ToString(),
                NombreCliente = dr["NombreCliente"].ToString(),
                Municipio = dr["Municipio"].ToString(),
                NIT = dr["NIT"]?.ToString(),
                Contacto = dr["Contacto"]?.ToString(),
                Email = dr["Email"]?.ToString(),
                Telefono = dr["Telefono"]?.ToString(),
                FechaInicio = Convert.ToDateTime(dr["FechaInicio"]),
                FechaExpiracion = Convert.ToDateTime(dr["FechaExpiracion"]),
                MaxUsuarios = Convert.ToInt32(dr["MaxUsuarios"]),
                Estado = dr["Estado"].ToString(),
                ModulosPermitidos = dr["ModulosPermitidos"]?.ToString(),
                FechaCreacion = dr["FechaCreacion"] != DBNull.Value ? Convert.ToDateTime(dr["FechaCreacion"]) : (DateTime?)null,
                FechaUltimaValidacion = dr["FechaUltimaValidacion"] != DBNull.Value ? Convert.ToDateTime(dr["FechaUltimaValidacion"]) : (DateTime?)null,
                Observaciones = dr["Observaciones"]?.ToString()
            };
        }
    }
}
