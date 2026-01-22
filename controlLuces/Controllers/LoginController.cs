using controlLuces.Models;
using controlLuces.permisos;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using iTextSharp.text.pdf.qrcode;

namespace controlLuces.Controllers
{
    public class LoginController : Controller
    {
        SqlConnection con = new SqlConnection();
        SqlCommand com = new SqlCommand();
        SqlDataReader dr;

        void connectionString()
        {
            //con.ConnectionString = "data source =luminarias.mssql.somee.com ;initial catalog=luminarias;user id=JonathanFLF_SQLLogin_1;pwd=zgya9woz
            con.ConnectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
            //con.ConnectionString = "data source = luminaria.mssql.somee.com; initial catalog = luminaria; user id = hhjhhshsgsg_SQLLogin_2; pwd = cdrf7hhrrl";
        }

        // GET: Login
        public ActionResult Login()
        {
            con.Close();
            return View();
        }

        public ActionResult NuevaContra()
        {
            return View();
        }

        public ActionResult RestablecerContraseña(String nuevaContraseña)
        {
            string correo = Session["destinatario"] as string;
            connectionString();
            con.Open();
            com.Connection = con;
            com.CommandText = "UPDATE usuarios SET  Clave = @Clave WHERE Correo ='" + correo + "'";
            com.Parameters.AddWithValue("@Clave", nuevaContraseña);

            try
            {
                int rowsAffected = com.ExecuteNonQuery();
                con.Close();

                if (rowsAffected > 0)
                {
                    ViewBag.EditMessage = "contraseña Cambiada Correctamente";
                    return RedirectToAction("Login");
                }
                else
                {
                    ViewBag.EditMessage = "No se pudo cambiar";
                }
            }
            catch (Exception ex)
            {
                ViewBag.EditMessage = "Error al intentar editar el usuario: " + ex.Message;
            }

            return null;
        }

        public ActionResult ingresarCodigo(string destinatario)
        {
            return View();
        }

        public ActionResult VerificarCodigo(string codigo)
        {
            string codigoEnviado = Session["CodigoEnviado"] as string;

            if (codigo == codigoEnviado)
            {
                return View("NuevaContra");
            }
            else
            {
                ViewBag.ErrorMessage = "El código ingresado no es válido.";
                return View("ingresarCodigo");
            }
        }

        public ActionResult EnviarCodigo(string destinatario)
        {
            connectionString();

            string query = "SELECT Correo FROM usuarios WHERE Correo = @Correo";
            com.Connection = con;
            com.CommandText = query;
            com.Parameters.AddWithValue("@Correo", destinatario);

            try
            {
                con.Open();
                string correoEncontrado = (string)com.ExecuteScalar();
                if (correoEncontrado != null)
                {
                    EnviarCorreo(destinatario);
                    return View("ingresarCodigo");
                }
                else
                {
                    ViewBag.ErrorMessage = "El correo electrónico no está registrado";
                    return View("recuperarcontra");
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error al conectar con la base de datos: " + ex.Message;
                return View("recuperarcontra");
            }
            finally
            {
                con.Close();
            }
            return null;
        }

        public ActionResult EnviarCorreo(string destinatario)
        {
            string urlDomain = "https://localhost:44360/";
            string EmailOrigen = "ferney5585@gmail.com";
            string Password = "jjak vuuq ciyd dyze";
            string url = urlDomain;

            // Generar código de 4 dígitos aleatorio
            Random random = new Random();
            int codigo = random.Next(1000, 9999);
            Session["CodigoEnviado"] = codigo.ToString();
            Session["destinatario"] = destinatario.ToString();

            string Nombre = "Parking";
            string Cuerpo = $"Hola,<br/><br/>Su código de verificación es: <strong>{codigo}</strong><br/><br/>" +
                            $"Puede ingresar el código en el siguiente enlace: <a href='{url}'>Restablecer contraseña</a>";
            string Asunto = "Reestablecer Contraseña";

            var mail = new MailMessage()
            {
                From = new MailAddress(EmailOrigen, Nombre),
                Subject = Asunto,
                Body = Cuerpo,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.Default,
                IsBodyHtml = true,
            };
            mail.To.Add(destinatario.ToLower().Trim());

            var client = new SmtpClient()
            {
                EnableSsl = true,
                Port = 587,
                Host = "smtp.gmail.com",
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(EmailOrigen, Password)
            };

            try
            {
                client.Send(mail);
                return RedirectToAction("ingresarCodigo");
            }
            catch (Exception)
            {
                return View("Error");
            }
            finally
            {
                client.Dispose();
            }
        }

        public ActionResult recuperarcontra()
        {
            return View();
        }

        public ActionResult inicio(PqrsModel pqrs, OrdenModel ordenes_de_servicio)
        {
            int cantidadPqrsEstado1 = 0;
            int cantidadPqrsEstado2 = 0;
            int cantidadPqrsEstado3 = 0;
            int cantidadOrdenesEstado2 = 0;
            int cantidadTrabajos = 0;

            bool esAdminLocal = (Session["EsAdminLocal"] as bool?) ?? false;
            string municipio = Session["Municipio"] as string;

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            if (esAdminLocal && !string.IsNullOrWhiteSpace(municipio))
            {
                // ====== PQRS por estado, SOLO del municipio del Admin_Local ======
                com.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 1 AND Municipio = @M";
                com.Parameters.AddWithValue("@M", municipio);
                cantidadPqrsEstado1 = (int)com.ExecuteScalar();

                com.Parameters.Clear();
                com.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 2 AND Municipio = @M";
                com.Parameters.AddWithValue("@M", municipio);
                cantidadPqrsEstado2 = (int)com.ExecuteScalar();

                com.Parameters.Clear();
                com.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 3 AND Municipio = @M";
                com.Parameters.AddWithValue("@M", municipio);
                cantidadPqrsEstado3 = (int)com.ExecuteScalar();

                // Tu lógica original sumaba E1+E2 para “ordenes activas”
                cantidadOrdenesEstado2 = cantidadPqrsEstado1 + cantidadPqrsEstado2;

                // ====== Trabajos realizados SOLO del municipio (vía join con orden) ======
                // Cuenta trabajos asociados a órdenes cuyo municipio coincide
                com.Parameters.Clear();
                com.CommandText = @"
            SELECT COUNT(*) 
            FROM dbo.Trabajos_Realizados tr
            INNER JOIN dbo.Ordenes_Cerradas oc  ON tr.IdOrdenCerrada = oc.Id_Orden_Cerrada
            INNER JOIN dbo.ordenes_de_servicio o ON oc.Id_Orden_Servicio = o.id_orden
            WHERE o.Municipio = @M";
                com.Parameters.AddWithValue("@M", municipio);
                cantidadTrabajos = (int)com.ExecuteScalar();
            }
            else
            {
                // ====== Vista global (Administrador/Técnico) ======
                com.CommandText = "SELECT COUNT(*) FROM pqrs WHERE Estado = 1";
                cantidadPqrsEstado1 = (int)com.ExecuteScalar();

                com.CommandText = "SELECT COUNT(*) FROM pqrs WHERE Estado = 2";
                cantidadPqrsEstado2 = (int)com.ExecuteScalar();

                com.CommandText = "SELECT COUNT(*) FROM pqrs WHERE Estado = 3";
                cantidadPqrsEstado3 = (int)com.ExecuteScalar();

                cantidadOrdenesEstado2 = cantidadPqrsEstado1 + cantidadPqrsEstado2;

                com.CommandText = "SELECT COUNT(*) FROM dbo.Trabajos_Realizados";
                cantidadTrabajos = (int)com.ExecuteScalar();
            }

            con.Close();

            ViewBag.CantidadPqrsEstado1 = cantidadPqrsEstado1;
            ViewBag.CantidadPqrsEstado2 = cantidadPqrsEstado2;
            ViewBag.CantidadPqrsEstado3 = cantidadPqrsEstado3;
            ViewBag.CantidadOrdenesEstado2 = cantidadOrdenesEstado2;
            ViewBag.CantidadTrabajos = cantidadTrabajos;

            return View();
        }

        [System.Web.Http.HttpPost]
        public ActionResult verificar(LoginModel modelo)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "SELECT * FROM usuarios WHERE Correo=@Correo AND Clave=@Clave";
            com.Parameters.AddWithValue("@Correo", modelo.Correo);
            com.Parameters.AddWithValue("@Clave", modelo.Clave);

            dr = com.ExecuteReader();
            if (dr.Read())
            {
                var usuario = new UsuarioModel
                {
                    IdUsuario = Convert.ToInt32(dr["IdUsuario"]),
                    Nombre = dr["Nombre"].ToString(),
                    Apellido = dr["Apellido"].ToString(),
                    Correo = dr["Correo"].ToString(),
                    IdRol = (Rol)Convert.ToInt32(dr["IdRol"]),
                    Clave = dr["Clave"].ToString(),
                    Municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString()
                };

                dr.Close();

                // ========== VALIDACIÓN DE LICENCIA ==========
                var validacionLicencia = ValidarLicenciaMunicipio(usuario.Municipio, usuario.Correo);
                if (!validacionLicencia.Valida)
                {
                    con.Close();
                    ViewBag.ErrorMessage = validacionLicencia.Mensaje;
                    ViewBag.LicenciaExpirada = true;
                    return View("Login");
                }

                // Guardar info de licencia en sesión
                Session["LicenciaValida"] = true;
                Session["LicenciaExpiracion"] = validacionLicencia.FechaExpiracion;
                Session["LicenciaCliente"] = validacionLicencia.NombreCliente;
                Session["ModulosPermitidos"] = validacionLicencia.ModulosPermitidos;
                // ============================================

                // Persistencia de sesión
                Session["Usuario"] = usuario;
                Session["Municipio"] = usuario.Municipio;
                Session["EsAdminLocal"] = (usuario.IdRol == Rol.Administrador_Local);

                con.Close();

                // Redirige según rol
                if (usuario.IdRol == Rol.Administrador_Local)
                    return RedirectToAction("InicioLocal", "Login");
                return RedirectToAction("inicio", "Login");
            }

            dr.Close();
            con.Close();
            ViewBag.ErrorMessage = "Credenciales inválidas. Inténtelo de nuevo.";
            return View("Login");
        }

        // ========== MÉTODOS DE VALIDACIÓN DE LICENCIA ==========
        private ValidacionLicenciaResult ValidarLicenciaMunicipio(string municipio, string usuario)
        {
            var resultado = new ValidacionLicenciaResult();

            if (string.IsNullOrEmpty(municipio))
            {
                // Si no tiene municipio asignado, permitir acceso (admin global)
                resultado.Valida = true;
                resultado.Mensaje = "Acceso sin restricción de municipio";
                return resultado;
            }

            try
            {
                string ip = Request.UserHostAddress;

                using (var cmd = new SqlCommand("lightcon_lumin.sp_ValidarLicencia", con))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Municipio", municipio);
                    cmd.Parameters.AddWithValue("@IP", ip ?? "");
                    cmd.Parameters.AddWithValue("@Usuario", usuario ?? "");

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            resultado.Valida = Convert.ToBoolean(reader["Valida"]);
                            resultado.Resultado = reader["Resultado"].ToString();
                            resultado.Mensaje = reader["Mensaje"].ToString();
                            resultado.FechaExpiracion = reader["FechaExpiracion"] != DBNull.Value
                                ? Convert.ToDateTime(reader["FechaExpiracion"])
                                : (DateTime?)null;

                            if (resultado.Valida)
                            {
                                resultado.NombreCliente = reader["NombreCliente"].ToString();
                                resultado.ModulosPermitidos = reader["ModulosPermitidos"].ToString();
                                resultado.MaxUsuarios = Convert.ToInt32(reader["MaxUsuarios"]);
                            }
                        }
                        else
                        {
                            resultado.Valida = false;
                            resultado.Mensaje = "No se pudo validar la licencia";
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Si el procedimiento no existe, permitir acceso (compatibilidad)
                if (ex.Number == 2812 || ex.Message.Contains("Could not find stored procedure"))
                {
                    resultado.Valida = true;
                    resultado.Mensaje = "Sistema de licencias no configurado";
                }
                else
                {
                    resultado.Valida = false;
                    resultado.Mensaje = "Error al validar licencia: " + ex.Message;
                }
            }

            return resultado;
        }

        // Clase para resultado de validación
        public class ValidacionLicenciaResult
        {
            public bool Valida { get; set; }
            public string Resultado { get; set; }
            public string Mensaje { get; set; }
            public DateTime? FechaExpiracion { get; set; }
            public string NombreCliente { get; set; }
            public string ModulosPermitidos { get; set; }
            public int MaxUsuarios { get; set; }
        }

        public ActionResult CerrarSesion()
        {
            con.Close();
            Session.Clear();
            return RedirectToAction("Login", "Login");
        }
        public ActionResult InicioLocal()
        {
            var u = Session["Usuario"] as UsuarioModel;
            if (u == null) return RedirectToAction("Login"); // mejor ir al login
            string municipio = (Session["Municipio"] as string) ?? u.Municipio ?? "";
            connectionString();
            int pqrs1 = 0, pqrs2 = 0, pqrs3 = 0, ordenesActivas = 0;
            using (var cn = new SqlConnection(con.ConnectionString))
            using (var cmd = new SqlCommand("", cn))
            {
                cn.Open();
                // PQRS sin asignar (Estado = 1)
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 1 AND Municipio = @M";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@M", municipio);
                pqrs1 = Convert.ToInt32(cmd.ExecuteScalar());
                // PQRS en proceso (Estado = 2)
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 2 AND Municipio = @M";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@M", municipio);
                pqrs2 = Convert.ToInt32(cmd.ExecuteScalar());
                // PQRS cerradas (Estado = 3)
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.pqrs WHERE Estado = 3 AND Municipio = @M";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@M", municipio);
                pqrs3 = Convert.ToInt32(cmd.ExecuteScalar());
                // ==========================
                // ÓRDENES ACTIVAS = PQRS sin asignar + PQRS en proceso
                // ==========================
                ordenesActivas = pqrs1 + pqrs2;
            }
            ViewBag.PqrsSinAsignar = pqrs1;
            ViewBag.PqrsProceso = pqrs2;
            ViewBag.PqrsCerradas = pqrs3;
            ViewBag.OrdenesActivas = ordenesActivas;
            return View("inicio_local");
        }
        private bool TablaTieneColumna(SqlConnection cn, string tabla, string columna)
        {
            using (var chk = new SqlCommand(@"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_SCHEMA = PARSENAME(@T, 2) OR (CHARINDEX('.', @T) = 0 AND TABLE_SCHEMA='dbo'))
  AND TABLE_NAME   = PARSENAME(@T, 1)
  AND COLUMN_NAME  = @C;", cn))
            {
                chk.Parameters.AddWithValue("@T", tabla);
                chk.Parameters.AddWithValue("@C", columna);
                var o = chk.ExecuteScalar();
                return o != null;
            }
        }

        [PermisosRol(Rol.Administrador, Rol.Tecnico, Rol.Administrador_Local)]
        public ActionResult Usuarios()
        {
            var usuarioSesion = Session["Usuario"] as UsuarioModel;
            if (usuarioSesion == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var rolUsuario = usuarioSesion.IdRol;
            var municipioUsuario = usuarioSesion.Municipio;

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            string query = @"SELECT IdUsuario, Nombre, Apellido, Correo, IdRol, Clave, Municipio, Cuadrilla 
                     FROM usuarios";

            if (rolUsuario == Rol.Administrador_Local && !string.IsNullOrWhiteSpace(municipioUsuario))
            {
                query += " WHERE Municipio = @Municipio";
                com.Parameters.AddWithValue("@Municipio", municipioUsuario);
            }

            com.CommandText = query;

            var usuarios = new List<UsuarioModel>();

            using (var dr = com.ExecuteReader())
            {
                while (dr.Read())
                {
                    usuarios.Add(new UsuarioModel
                    {
                        IdUsuario = Convert.ToInt32(dr["IdUsuario"]),
                        Nombre = dr["Nombre"].ToString(),
                        Apellido = dr["Apellido"].ToString(),
                        Correo = dr["Correo"].ToString(),
                        IdRol = (Rol)Convert.ToInt32(dr["IdRol"]),
                        Clave = dr["Clave"].ToString(),
                        Municipio = dr["Municipio"] == DBNull.Value ? null : dr["Municipio"].ToString(),
                        Cuadrilla = dr["Cuadrilla"] == DBNull.Value ? null : dr["Cuadrilla"].ToString()
                    });
                }
            }

            con.Close();
            return View(usuarios);
        }


        public ActionResult Registrar(UsuarioModel regis)
        {
            // VALIDACIÓN mínima: Admin_Local requiere Municipio
            if ((Rol)regis.IdRol == Rol.Administrador_Local && string.IsNullOrWhiteSpace(regis.Municipio))
            {
                ViewBag.ErrorMessage = "Para el rol Administrador_Local debes seleccionar un Municipio.";
                return View("Registro", regis);
            }

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = @"INSERT INTO usuarios
                (IdUsuario, Nombre, Clave, Apellido, Correo, IdRol, Municipio, Cuadrilla)
                VALUES (@IdUsuario, @Nombre, @Clave, @Apellido, @Correo, @IdRol, @Municipio, @Cuadrilla)";

            com.Parameters.AddWithValue("@IdUsuario", regis.IdUsuario);
            com.Parameters.AddWithValue("@Nombre", regis.Nombre ?? "");
            com.Parameters.AddWithValue("@Clave", regis.Clave ?? "");
            com.Parameters.AddWithValue("@Apellido", regis.Apellido ?? "");
            com.Parameters.AddWithValue("@Correo", regis.Correo ?? "");
            com.Parameters.AddWithValue("@IdRol", (int)regis.IdRol);
            com.Parameters.AddWithValue("@Municipio", (object)(regis.Municipio ?? (string)null) ?? DBNull.Value);
            com.Parameters.AddWithValue("@Cuadrilla", (object)(regis.Cuadrilla ?? (string)null) ?? DBNull.Value);

            try
            {
                int rows = com.ExecuteNonQuery();
                con.Close();
                return rows > 0 ? RedirectToAction("inicio", "Login")
                                : (ActionResult)View("Error");
            }
            catch (Exception ex)
            {
                con.Close();
                ViewBag.ErrorMessage = "Error en el registro: " + ex.Message;
                return View("Error");
            }
        }
        public ActionResult Registro()
        {
            return View();
        }
        // NUEVO MÉTODO: Obtener cuadrillas por municipio (AJAX)
        // ========================================
        
        public JsonResult ObtenerCuadrillasPorMunicipio(string municipio)
        {
            var cuadrillas = new List<object>();

            if (string.IsNullOrWhiteSpace(municipio))
            {
                return Json(cuadrillas, JsonRequestBehavior.AllowGet);
            }

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = "SELECT Nombre FROM [dbo].[Cuadrillas] WHERE Municipio = @Municipio ORDER BY Nombre";
            com.Parameters.AddWithValue("@Municipio", municipio);

            try
            {
                using (var dr = com.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        cuadrillas.Add(new
                        {
                            nombre = dr["Nombre"].ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error si es necesario
                System.Diagnostics.Debug.WriteLine("Error al cargar cuadrillas: " + ex.Message);
            }
            finally
            {
                con.Close();
            }

            return Json(cuadrillas, JsonRequestBehavior.AllowGet);
        }
        public ActionResult EditarUsuarios(int id)
        {
            var usuario = ObtenerUsuarioPorId(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        public ActionResult Registrousuarios()
        {
            return View();
        }

        public ActionResult EditarUsuario(int id, UsuarioModel registro)
        {
            // VALIDACIÓN mínima: Admin_Local requiere Municipio
            if ((Rol)registro.IdRol == Rol.Administrador_Local && string.IsNullOrWhiteSpace(registro.Municipio))
            {
                TempData["EditMessage"] = "Para el rol Administrador_Local debes seleccionar un Municipio.";
                return RedirectToAction("EditarUsuarios", new { id });
            }

            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();

            com.CommandText = @"
        UPDATE usuarios
           SET Nombre   = @Nombre,
               Clave    = @Clave,
               Apellido = @Apellido,
               Correo   = @Correo,
               IdRol    = @IdRol,
               Municipio= @Municipio
         WHERE IdUsuario = @IdUsuario";

            com.Parameters.AddWithValue("@Nombre", registro.Nombre ?? "");
            com.Parameters.AddWithValue("@Clave", registro.Clave ?? "");
            com.Parameters.AddWithValue("@Apellido", registro.Apellido ?? "");
            com.Parameters.AddWithValue("@Correo", registro.Correo ?? "");
            com.Parameters.AddWithValue("@IdRol", (int)registro.IdRol);
            com.Parameters.AddWithValue("@Municipio", (object)(registro.Municipio ?? (string)null) ?? DBNull.Value);
            com.Parameters.AddWithValue("@IdUsuario", id);

            try
            {
                var rows = com.ExecuteNonQuery();
                con.Close();
                TempData["EditMessage"] = rows > 0 ? "Usuario editado correctamente." : "No se encontró el usuario.";
                return RedirectToAction("Usuarios");
            }
            catch (Exception ex)
            {
                con.Close();
                TempData["EditMessage"] = "Error al editar: " + ex.Message;
                return RedirectToAction("Usuarios");
            }
        }

        public ActionResult RegistrarUsuarios(UsuarioModel registro)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.CommandText = "INSERT INTO usuarios (IdUsuario, Nombre, Clave, Apellido, Correo, IdRol) " +
                   "VALUES ('" + registro.IdUsuario + "','" + registro.Nombre + "','" + registro.Clave + "', '" +
                   registro.Apellido + "', '" + registro.Correo + "', 3)";

            try
            {
                int rowsAffected = com.ExecuteNonQuery();
                con.Close();

                if (rowsAffected > 0)
                {
                    TempData["RegistroExitoso"] = true;
                    return RedirectToAction("Login");
                }
                else
                {
                    ViewBag.ErrorMessage = "Error en el registro";
                    return View("Error");
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error en el registro: " + ex.Message;
                return View("Error");
            }
        }

        public ActionResult EliminarUsuario(int id)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.CommandText = "DELETE FROM usuarios WHERE IdUsuario = @IdUsuario";
            com.Parameters.AddWithValue("@IdUsuario", id);

            try
            {
                int rowsAffected = com.ExecuteNonQuery();
                con.Close();

                if (rowsAffected > 0)
                {
                    ViewBag.DeleteMessage = "Usuario eliminado correctamente.";
                    return RedirectToAction("Usuarios");
                }
                else
                {
                    ViewBag.DeleteMessage = "No se encontró ningún usuario con el IdUsuario proporcionado.";
                    return RedirectToAction("Usuarios");
                }
            }
            catch (Exception ex)
            {
                ViewBag.DeleteMessage = "Error al intentar eliminar el usuario: " + ex.Message;
                return RedirectToAction("Usuarios");
            }
        }

        public UsuarioModel ObtenerUsuarioPorId(int id)
        {
            connectionString();
            con.Open();
            com.Connection = con;
            com.Parameters.Clear();
            com.CommandText = @"SELECT IdUsuario, Nombre, Apellido, Correo, IdRol, Clave, Municipio
                          FROM usuarios
                         WHERE IdUsuario = @IdUsuario";
            com.Parameters.AddWithValue("@IdUsuario", id);

            UsuarioModel usuario = null;
            try
            {
                using (var rdr = com.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        usuario = new UsuarioModel
                        {
                            IdUsuario = Convert.ToInt32(rdr["IdUsuario"]),
                            Nombre = rdr["Nombre"].ToString(),
                            Apellido = rdr["Apellido"].ToString(),
                            Correo = rdr["Correo"].ToString(),
                            IdRol = (controlLuces.Models.Rol)Convert.ToInt32(rdr["IdRol"]),
                            Clave = rdr["Clave"].ToString(),
                            Municipio = rdr["Municipio"] == DBNull.Value ? null : rdr["Municipio"].ToString()
                        };
                    }
                }
            }
            finally
            {
                con.Close();
            }
            return usuario;
        }


      
    }
}
