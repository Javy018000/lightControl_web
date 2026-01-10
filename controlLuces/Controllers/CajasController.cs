using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;

using controlLuces.Models;
using System.Data.SqlClient;
using System.Threading.Tasks;
using ClosedXML.Excel;


namespace controlLuces.Controllers
{
    public class CajasController : Controller
    {
        private string GetConnectionString() =>
            "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

        // GET: /Cajas/verTodasCajas
        public async Task<ActionResult> verTodasCajas()
        {
            var lista = new List<CajaModel>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var com = new SqlCommand("SELECT * FROM Cajas", con))
            {
                await con.OpenAsync();
                using (var dr = await com.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        lista.Add(new CajaModel
                        {
                            Codigo_CajaID = dr["Codigo_CajaID"] as string ?? "",
                            ID_CAJA = dr["ID_CAJA"] as string ?? "",
                            Codigo_Poste = dr["Codigo_Poste"] as string ?? "",
                            Codigo_Caja = dr["Codigo_Caja"] as string ?? "",
                            ID_Apoyo_Lum = dr["ID_Apoyo_Lum"] as string ?? "",
                            ID_Tranformador_LUM = dr["ID_Tranformador_LUM"] as string ?? "",
                            Latitud = dr["Latitud"] != DBNull.Value ? Convert.ToDecimal(dr["Latitud"]) : 0,
                            Longitud = dr["Longitud"] != DBNull.Value ? Convert.ToDecimal(dr["Longitud"]) : 0,
                            Interdistancia = dr["Interdistancia"] != DBNull.Value ? (int?)Convert.ToInt32(dr["Interdistancia"]) : null,
                            Conectado_Con = dr["Conectado_Con"] as string ?? "",
                            Ducteria = dr["Ducteria"] as string ?? "",
                            BARRIO = dr["BARRIO"] as string ?? "",
                            DIRECCION = dr["DIRECCION"] as string ?? "",
                            Archivo = dr["Archivo"] as string ?? "",
                            Observaciones = dr["Observaciones"] as string ?? ""
                        });
                    }
                }
            }

            ViewBag.Barrios = lista.Where(x => !string.IsNullOrWhiteSpace(x.BARRIO))
                                   .Select(x => x.BARRIO).Distinct().OrderBy(x => x).ToList();

            return View(lista.OrderBy(x => x.Codigo_CajaID).ToList());
        }

        public ActionResult VerInfoDetalladaCaja(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return HttpNotFound();

            var caja = ObtenerCajaPorCodigo(id);

            if (caja == null)
                return HttpNotFound();

            return View("VerInfoDetalladaCaja", caja);
        }

        private CajaModel ObtenerCajaPorCodigo(string id)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var com = new SqlCommand("SELECT TOP 1 * FROM Cajas WHERE Codigo_CajaID = @id", con))
            {
                com.Parameters.AddWithValue("@id", id);
                con.Open();
                using (var dr = com.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        return new CajaModel
                        {
                            Codigo_CajaID = dr["Codigo_CajaID"] as string ?? "",
                            ID_CAJA = dr["ID_CAJA"] as string ?? "",
                            Codigo_Poste = dr["Codigo_Poste"] as string ?? "",
                            Codigo_Caja = dr["Codigo_Caja"] as string ?? "",
                            ID_Apoyo_Lum = dr["ID_Apoyo_Lum"] as string ?? "",
                            ID_Tranformador_LUM = dr["ID_Tranformador_LUM"] as string ?? "",
                            Latitud = dr["Latitud"] != DBNull.Value ? Convert.ToDecimal(dr["Latitud"]) : 0,
                            Longitud = dr["Longitud"] != DBNull.Value ? Convert.ToDecimal(dr["Longitud"]) : 0,
                            Interdistancia = dr["Interdistancia"] != DBNull.Value ? (int?)Convert.ToInt32(dr["Interdistancia"]) : null,
                            Conectado_Con = dr["Conectado_Con"] as string ?? "",
                            Ducteria = dr["Ducteria"] as string ?? "",
                            BARRIO = dr["BARRIO"] as string ?? "",
                            DIRECCION = dr["DIRECCION"] as string ?? "",
                            Archivo = dr["Archivo"] as string ?? "",
                            Observaciones = dr["Observaciones"] as string ?? ""
                        };
                    }
                }
            }
            return null;
        }

        // EXPORTAR A EXCEL CON CLOSEDXML
        [HttpGet]
        public async Task<ActionResult> ExportarCajasExcel()
        {
            try
            {
                var lista = new List<CajaModel>();

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand())
                {
                    await con.OpenAsync();
                    cmd.Connection = con;
                    cmd.CommandTimeout = 300;

                    // TRAER TODAS LAS CAJAS
                    cmd.CommandText = @"
                        SELECT 
                            Codigo_CajaID, ID_CAJA, Codigo_Poste, Codigo_Caja,
                            ID_Apoyo_Lum, ID_Tranformador_LUM, Latitud, Longitud,
                            Interdistancia, Conectado_Con, Ducteria, BARRIO,
                            DIRECCION, Archivo, Observaciones
                        FROM Cajas
                        ORDER BY Codigo_CajaID";

                    using (var dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            lista.Add(new CajaModel
                            {
                                Codigo_CajaID = dr["Codigo_CajaID"] as string ?? "",
                                ID_CAJA = dr["ID_CAJA"] as string ?? "",
                                Codigo_Poste = dr["Codigo_Poste"] as string ?? "",
                                Codigo_Caja = dr["Codigo_Caja"] as string ?? "",
                                ID_Apoyo_Lum = dr["ID_Apoyo_Lum"] as string ?? "",
                                ID_Tranformador_LUM = dr["ID_Tranformador_LUM"] as string ?? "",
                                Latitud = dr["Latitud"] != DBNull.Value ? Convert.ToDecimal(dr["Latitud"]) : 0,
                                Longitud = dr["Longitud"] != DBNull.Value ? Convert.ToDecimal(dr["Longitud"]) : 0,
                                Interdistancia = dr["Interdistancia"] != DBNull.Value ? (int?)Convert.ToInt32(dr["Interdistancia"]) : null,
                                Conectado_Con = dr["Conectado_Con"] as string ?? "",
                                Ducteria = dr["Ducteria"] as string ?? "",
                                BARRIO = dr["BARRIO"] as string ?? "",
                                DIRECCION = dr["DIRECCION"] as string ?? "",
                                Archivo = dr["Archivo"] as string ?? "",
                                Observaciones = dr["Observaciones"] as string ?? ""
                            });
                        }
                    }
                }

                if (!lista.Any())
                {
                    return Content("No hay cajas para exportar.");
                }

                // GENERAR EXCEL CON CLOSEDXML
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Cajas");

                    // ENCABEZADOS
                    worksheet.Cell(1, 1).Value = "Código Caja ID";
                    worksheet.Cell(1, 2).Value = "ID Caja";
                    worksheet.Cell(1, 3).Value = "Código Poste";
                    worksheet.Cell(1, 4).Value = "Código Caja";
                    worksheet.Cell(1, 5).Value = "ID Apoyo Lum";
                    worksheet.Cell(1, 6).Value = "ID Transformador LUM";
                    worksheet.Cell(1, 7).Value = "Latitud";
                    worksheet.Cell(1, 8).Value = "Longitud";
                    worksheet.Cell(1, 9).Value = "Interdistancia";
                    worksheet.Cell(1, 10).Value = "Conectado Con";
                    worksheet.Cell(1, 11).Value = "Ducteria";
                    worksheet.Cell(1, 12).Value = "Barrio";
                    worksheet.Cell(1, 13).Value = "Dirección";
                    worksheet.Cell(1, 14).Value = "Archivo";
                    worksheet.Cell(1, 15).Value = "Observaciones";

                    // ESTILO ENCABEZADOS
                    var headerRange = worksheet.Range(1, 1, 1, 15);
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
                        worksheet.Cell(fila, 1).Value = item.Codigo_CajaID;
                        worksheet.Cell(fila, 2).Value = item.ID_CAJA;
                        worksheet.Cell(fila, 3).Value = item.Codigo_Poste;
                        worksheet.Cell(fila, 4).Value = item.Codigo_Caja;
                        worksheet.Cell(fila, 5).Value = item.ID_Apoyo_Lum;
                        worksheet.Cell(fila, 6).Value = item.ID_Tranformador_LUM;
                        worksheet.Cell(fila, 7).Value = item.Latitud;
                        worksheet.Cell(fila, 8).Value = item.Longitud;
                        worksheet.Cell(fila, 9).Value = item.Interdistancia?.ToString() ?? "";
                        worksheet.Cell(fila, 10).Value = item.Conectado_Con;
                        worksheet.Cell(fila, 11).Value = item.Ducteria;
                        worksheet.Cell(fila, 12).Value = item.BARRIO;
                        worksheet.Cell(fila, 13).Value = item.DIRECCION;
                        worksheet.Cell(fila, 14).Value = item.Archivo;
                        worksheet.Cell(fila, 15).Value = item.Observaciones;

                        // Alternar colores
                        if (fila % 2 == 0)
                        {
                            worksheet.Range(fila, 1, fila, 15).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
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
                        var nombreArchivo = $"Cajas_{fecha:yyyyMMdd_HHmmss}.xlsx";

                        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                    }
                }
            }
            catch (Exception ex)
            {
                return Content($"Error al exportar: {ex.Message}");
            }
        }
    }
}