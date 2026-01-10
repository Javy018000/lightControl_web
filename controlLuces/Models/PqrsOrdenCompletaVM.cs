using System.Collections.Generic;

namespace controlLuces.Models
{
    public class PqrsOrdenCompletaVM
    {
        // Datos de la PQRS
        public PqrsModel Pqrs { get; set; } = new PqrsModel();

        // Datos de la Orden de Servicio
        public OrdenModel Orden { get; set; } = new OrdenModel();

        // Datos del Cierre
        public OrdenCerradaModel Cierre { get; set; } = new OrdenCerradaModel();

        // Trabajos realizados
        public List<TrabajoRealizadoModel> Trabajos { get; set; } = new List<TrabajoRealizadoModel>();

        // Imágenes
        public List<ImagenOrdenServicioModel> Imagenes { get; set; } = new List<ImagenOrdenServicioModel>();
        public List<InsumoRealizadoModel> Insumos { get; set; }
    }
}