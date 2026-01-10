using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Controllers
{
    public class ArchivoController : Controller
    {
        // Carpeta física donde se guardan TODOS los archivos
        // (fuera de la app, para que no se cierre el programa)
        private string ObtenerCarpetaBase()
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LightControlArchivos");

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

                string extension = Path.GetExtension(archivo.FileName);
                string nombreBase = Path.GetFileNameWithoutExtension(archivo.FileName);

                // nombre único
                string nombreFinal = nombreBase + extension;
                string rutaFisica = Path.Combine(carpeta, nombreFinal);

                int i = 1;
                while (System.IO.File.Exists(rutaFisica))
                {
                    nombreFinal = $"{nombreBase}_{i}{extension}";
                    rutaFisica = Path.Combine(carpeta, nombreFinal);
                    i++;
                }

                archivo.SaveAs(rutaFisica);

                TempData["MensajeExito"] = "El archivo se cargó correctamente.";
                return RedirectToAction("VerArchivo");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Ocurrió un error al guardar el archivo: " + ex.Message;
                return View();
            }
        }

        // ======================= LISTAR ARCHIVOS =======================
        [HttpGet]
        public ActionResult VerArchivo()
        {
            string carpeta = ObtenerCarpetaBase();

            var dir = new DirectoryInfo(carpeta);
            var archivos = dir.Exists
                ? dir.GetFiles().OrderByDescending(f => f.LastWriteTime).ToList()
                : new List<FileInfo>();

            ViewBag.Archivos = archivos;
            ViewBag.MensajeExito = TempData["MensajeExito"] as string;

            return View();
        }

        // ======================= MOSTRAR / DESCARGAR ARCHIVO =======================
        [HttpGet]
        public ActionResult MostrarArchivo(string nombre)
        {
            if (string.IsNullOrEmpty(nombre))
                return HttpNotFound();

            string carpeta = ObtenerCarpetaBase();
            string rutaFisica = Path.Combine(carpeta, nombre);

            if (!System.IO.File.Exists(rutaFisica))
                return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(rutaFisica);
            return File(rutaFisica, contentType);
        }
    }
}
