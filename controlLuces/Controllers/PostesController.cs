using controlLuces.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ClosedXML.Excel;
using System.IO;

namespace controlLuces.Controllers
{
    public class PostesController : Controller
    {
        private string GetConnectionString() =>
            "data source=tadeo.colombiahosting.com.co\\MSSQLSERVER2019;initial catalog=lightcon_luminaria;user id=lightcon_lumin;pwd=luminaria2024*";

        // GET: /Postes/vertodapostes
        public async Task<ActionResult> vertodapostes()
        {
            var lista = new List<PosteModel>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                com.CommandTimeout = 120;
                // Seleccionar solo columnas necesarias y ordenar en SQL
                com.CommandText = @"SELECT codigo, rev_1, poste, interdistancia_pos, latitud, longitud,
                                    barrio, direccion, tipo_red, archivo, observaciones
                                    FROM postes ORDER BY codigo";

                await con.OpenAsync().ConfigureAwait(false);
                using (var dr = await com.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await dr.ReadAsync().ConfigureAwait(false))
                    {
                        lista.Add(new PosteModel
                        {
                            codigo = dr["codigo"] as string ?? "",
                            rev_1 = dr["rev_1"] as string ?? "",
                            poste = dr["poste"] as string ?? "",
                            interdistancia_pos = dr["interdistancia_pos"] != DBNull.Value ? (int?)Convert.ToInt32(dr["interdistancia_pos"]) : null,
                            latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                            longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                            barrio = dr["barrio"] as string ?? "",
                            direccion = dr["direccion"] as string ?? "",
                            tipo_red = dr["tipo_red"] as string ?? "",
                            archivo = dr["archivo"] as string ?? "",
                            observaciones = dr["observaciones"] as string ?? ""
                        });
                    }
                }
            }

            ViewBag.Barrios = lista.Where(x => !string.IsNullOrWhiteSpace(x.barrio))
                                   .Select(x => x.barrio).Distinct().OrderBy(x => x).ToList();

            return View(lista);
        }

        // GET: /Postes/VerInfoDetallada/{id}
        public async Task<ActionResult> VerInfoDetallada(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return HttpNotFound();

            PosteModel poste = null;
            using (var con = new SqlConnection(GetConnectionString()))
            using (var com = new SqlCommand("SELECT * FROM postes WHERE codigo = @codigo", con))
            {
                com.Parameters.AddWithValue("@codigo", id);
                await con.OpenAsync();
                using (var dr = await com.ExecuteReaderAsync())
                {
                    if (await dr.ReadAsync())
                    {
                        poste = new PosteModel
                        {
                            codigo = dr["codigo"] as string ?? "",
                            rev_1 = dr["rev_1"] as string ?? "",
                            poste = dr["poste"] as string ?? "",
                            interdistancia_pos = dr["interdistancia_pos"] != DBNull.Value ? (int?)Convert.ToInt32(dr["interdistancia_pos"]) : null,
                            latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                            longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                            barrio = dr["barrio"] as string ?? "",
                            direccion = dr["direccion"] as string ?? "",
                            tipo_red = dr["tipo_red"] as string ?? "",
                            archivo = dr["archivo"] as string ?? "",
                            observaciones = dr["observaciones"] as string ?? ""
                        };
                    }
                }
            }

            if (poste == null) return HttpNotFound();
            return View("VerInfoDetallada", poste); // Views/Postes/VerInfoDetallada.cshtml
        }

        [HttpGet]
        public async Task<ActionResult> ExportarPostesExcel()
        {
            try
            {
                var lista = new List<PosteModel>();

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand())
                {
                    await con.OpenAsync();
                    cmd.Connection = con;
                    cmd.CommandTimeout = 300;

                    // TRAER TODOS LOS POSTES
                    cmd.CommandText = @"
                SELECT 
                    codigo, rev_1, poste, interdistancia_pos, latitud, longitud,
                    barrio, direccion, tipo_red, archivo, observaciones
                FROM postes
                ORDER BY codigo";

                    using (var dr = await cmd.ExecuteReaderAsync())
                    {
                        while (await dr.ReadAsync())
                        {
                            lista.Add(new PosteModel
                            {
                                codigo = dr["codigo"] as string ?? "",
                                rev_1 = dr["rev_1"] as string ?? "",
                                poste = dr["poste"] as string ?? "",
                                interdistancia_pos = dr["interdistancia_pos"] != DBNull.Value ? (int?)Convert.ToInt32(dr["interdistancia_pos"]) : null,
                                latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                                longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                                barrio = dr["barrio"] as string ?? "",
                                direccion = dr["direccion"] as string ?? "",
                                tipo_red = dr["tipo_red"] as string ?? "",
                                archivo = dr["archivo"] as string ?? "",
                                observaciones = dr["observaciones"] as string ?? ""
                            });
                        }
                    }
                }

                if (!lista.Any())
                {
                    return Content("No hay postes para exportar.");
                }

                // GENERAR EXCEL CON CLOSEDXML
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Postes");

                    // ENCABEZADOS
                    worksheet.Cell(1, 1).Value = "Código";
                    worksheet.Cell(1, 2).Value = "Rev_1";
                    worksheet.Cell(1, 3).Value = "Poste";
                    worksheet.Cell(1, 4).Value = "Interdistancia";
                    worksheet.Cell(1, 5).Value = "Latitud";
                    worksheet.Cell(1, 6).Value = "Longitud";
                    worksheet.Cell(1, 7).Value = "Barrio";
                    worksheet.Cell(1, 8).Value = "Dirección";
                    worksheet.Cell(1, 9).Value = "Tipo Red";
                    worksheet.Cell(1, 10).Value = "Archivo";
                    worksheet.Cell(1, 11).Value = "Observaciones";

                    // ESTILO ENCABEZADOS
                    var headerRange = worksheet.Range(1, 1, 1, 11);
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
                        worksheet.Cell(fila, 2).Value = item.rev_1;
                        worksheet.Cell(fila, 3).Value = item.poste;
                        worksheet.Cell(fila, 4).Value = item.interdistancia_pos?.ToString() ?? "";
                        worksheet.Cell(fila, 5).Value = item.latitud;
                        worksheet.Cell(fila, 6).Value = item.longitud;
                        worksheet.Cell(fila, 7).Value = item.barrio;
                        worksheet.Cell(fila, 8).Value = item.direccion;
                        worksheet.Cell(fila, 9).Value = item.tipo_red;
                        worksheet.Cell(fila, 10).Value = item.archivo;
                        worksheet.Cell(fila, 11).Value = item.observaciones;

                        // Alternar colores
                        if (fila % 2 == 0)
                        {
                            worksheet.Range(fila, 1, fila, 11).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);
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
                        var nombreArchivo = $"Postes_{fecha:yyyyMMdd_HHmmss}.xlsx";

                        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                    }
                }
            }
            catch (Exception ex)
            {
                return Content($"Error al exportar: {ex.Message}");
            }
        }
        // GET: /Postes/BuscarPostes?tipoBusqueda=codigo&codigo=P001  (opcional)
        public async Task<ActionResult> BuscarPostes(string tipoBusqueda, string codigo, string barrio)
        {
            var lista = new List<PosteModel>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var com = new SqlCommand() { Connection = con })
            {
                com.CommandTimeout = 120;
                // Seleccionar solo columnas necesarias
                string baseSelect = "SELECT codigo, latitud, longitud, poste, barrio, direccion, tipo_red FROM postes";

                switch ((tipoBusqueda ?? "").ToLower())
                {
                    case "codigo":
                        com.CommandText = baseSelect + " WHERE codigo = @codigo ORDER BY codigo";
                        com.Parameters.AddWithValue("@codigo", codigo ?? "");
                        break;
                    case "barrio":
                        com.CommandText = baseSelect + " WHERE barrio = @barrio ORDER BY codigo";
                        com.Parameters.AddWithValue("@barrio", barrio ?? "");
                        break;
                    default:
                        com.CommandText = baseSelect + " ORDER BY codigo";
                        break;
                }

                await con.OpenAsync().ConfigureAwait(false);
                using (var dr = await com.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await dr.ReadAsync().ConfigureAwait(false))
                    {
                        lista.Add(new PosteModel
                        {
                            codigo = dr["codigo"] as string ?? "",
                            latitud = dr["latitud"] != DBNull.Value ? Convert.ToSingle(dr["latitud"]) : 0,
                            longitud = dr["longitud"] != DBNull.Value ? Convert.ToSingle(dr["longitud"]) : 0,
                            poste = dr["poste"] as string ?? "",
                            barrio = dr["barrio"] as string ?? "",
                            direccion = dr["direccion"] as string ?? "",
                            tipo_red = dr["tipo_red"] as string ?? ""
                        });
                    }
                }
            }

            ViewBag.Barrios = lista.Where(x => !string.IsNullOrWhiteSpace(x.barrio))
                                   .Select(x => x.barrio).Distinct().OrderBy(x => x).ToList();

            return View("vertodapostes", lista);
        }
    }
}
