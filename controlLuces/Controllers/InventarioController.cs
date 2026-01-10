using controlLuces.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Controllers
{
    public class InventarioController : Controller
    {
        // Definir la cadena de conexión
        //string connectionString = "data source =LAPTOP-VHK1MAKD\\CONEXION ;DataBase=luminarias; integrated security=SSPI";
        string connectionString = "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";
        //string connectionString = "data source = luminaria.mssql.somee.com; initial catalog = luminaria; user id = hhjhhshsgsg_SQLLogin_2; pwd = cdrf7hhrrl";
       
        //string connectionString = "data source =luminarias.mssql.somee.com ;initial catalog=luminarias;user id=JonathanFLF_SQLLogin_1;pwd=zgya9wozpl";

        // GET: Inventario
        public ActionResult VerElementos()
        {
            List<inventarioModel> invenList = new List<inventarioModel>();

            // Consultar elementos del inventario desde la base de datos
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM Inventario", con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    inventarioModel inven = new inventarioModel();
                    inven.ID = Convert.ToInt32(dr["ID"]);
                    inven.nombre_elemento = dr["nombre_elemento"].ToString();
                    inven.estado = dr["estado"].ToString();
                    inven.descripcion = dr["descripcion"].ToString();
                    inven.cantidad = Convert.ToInt32(dr["cantidad"]);
                    invenList.Add(inven);
                }
                con.Close();
            }

            return View(invenList);
        }

        // GET: Inventario/CrearElemento
        public ActionResult CrearElemento(int? id)
        {
            if (id.HasValue)
            {
                inventarioModel elemento = new inventarioModel();

                // Obtener el elemento a editar desde la base de datos
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    SqlCommand cmd = new SqlCommand("SELECT * FROM Inventario WHERE ID=@ID", con);
                    cmd.Parameters.AddWithValue("@ID", id.Value);
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        elemento.ID = Convert.ToInt32(dr["ID"]);
                        elemento.nombre_elemento = dr["nombre_elemento"].ToString();
                        elemento.estado = dr["estado"].ToString();
                        elemento.descripcion = dr["descripcion"].ToString();
                        elemento.cantidad = Convert.ToInt32(dr["cantidad"]);
                    }
                    con.Close();
                }

                return View(elemento);
            }
            else
            {
                return View(new inventarioModel());
            }
        }

        // POST: Inventario/CrearElemento
        [HttpPost]
        public ActionResult CrearElemento(inventarioModel elemento)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    SqlCommand cmd;
                    if (elemento.ID > 0)
                    {
                        // Actualizar el elemento existente
                        cmd = new SqlCommand("UPDATE Inventario SET nombre_elemento=@Nombre, cantidad=@Cantidad, estado=@Estado, descripcion=@Descripcion WHERE ID=@ID", con);
                        cmd.Parameters.AddWithValue("@ID", elemento.ID);
                    }
                    else
                    {
                        // Insertar un nuevo elemento
                        cmd = new SqlCommand("INSERT INTO Inventario (nombre_elemento, cantidad, estado, descripcion) VALUES (@Nombre, @Cantidad, @Estado, @Descripcion)", con);
                    }
                    cmd.Parameters.AddWithValue("@Nombre", elemento.nombre_elemento);
                    cmd.Parameters.AddWithValue("@Cantidad", elemento.cantidad);
                    cmd.Parameters.AddWithValue("@Estado", elemento.estado);
                    cmd.Parameters.AddWithValue("@Descripcion", elemento.descripcion);
                    cmd.ExecuteNonQuery();
                    con.Close();
                }

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Error en la validación del modelo" });
        }

        // GET: Inventario/EditarElemento/5
        public ActionResult EditarElemento(int id)
        {
            return RedirectToAction("CrearElemento", new { id = id });
        }

        // GET: Inventario/EliminarElemento/5
        public ActionResult EliminarElemento(int id)
        {
            // Eliminar el elemento de la base de datos
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM Inventario WHERE ID=@ID", con);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.ExecuteNonQuery();
                con.Close();
            }

            return RedirectToAction("VerElementos");
        }

        // GET: Inventario/GenerarPdf
        public ActionResult GenerarPdf()
        {
            List<inventarioModel> invenList = new List<inventarioModel>();

            // Consultar elementos del inventario desde la base de datos
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM Inventario", con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    inventarioModel inven = new inventarioModel();
                    inven.ID = Convert.ToInt32(dr["ID"]);
                    inven.nombre_elemento = dr["nombre_elemento"].ToString();
                    inven.estado = dr["estado"].ToString();
                    inven.descripcion = dr["descripcion"].ToString();
                    inven.cantidad = Convert.ToInt32(dr["cantidad"]);
                    invenList.Add(inven);
                }
                con.Close();
            }

            // Generar PDF
            MemoryStream workStream = new MemoryStream();
            Document document = new Document();
            PdfWriter writer = PdfWriter.GetInstance(document, workStream);
            writer.CloseStream = false;
            document.Open();

            // Título
            //document.Add(new Paragraph("Listado de Items", new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD)));
            document.Add(new Paragraph(" ")); // Espacio en blanco

            // Tabla
            PdfPTable table = new PdfPTable(5);
            table.WidthPercentage = 100;
            table.AddCell("ID");
            table.AddCell("Nombre");
            table.AddCell("Cantidad");
            table.AddCell("Estado");
            table.AddCell("Descripcion");

            foreach (var item in invenList)
            {
                table.AddCell(item.ID.ToString());
                table.AddCell(item.nombre_elemento);
                table.AddCell(item.cantidad.ToString());
                table.AddCell(item.estado);
                table.AddCell(item.descripcion);
            }

            document.Add(table);
            document.Close();

            byte[] byteArray = workStream.ToArray();
            workStream.Write(byteArray, 0, byteArray.Length);
            workStream.Position = 0;

            return File(workStream, "application/pdf", "ListadoInventario.pdf");
        }
    }
}
