using System.Web.Mvc;

namespace controlLuces.Controllers
{
    /// <summary>
    /// Controlador de redirección para Consumos RETILAP.
    /// Las funcionalidades reales están en ArchivoController.
    /// </summary>
    public class ConsumosController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("VerConsumos", "Archivo");
        }

        public ActionResult Crear()
        {
            return RedirectToAction("NuevoConsumo", "Archivo");
        }

        public ActionResult Resumen()
        {
            return RedirectToAction("VerConsumos", "Archivo");
        }
    }
}
