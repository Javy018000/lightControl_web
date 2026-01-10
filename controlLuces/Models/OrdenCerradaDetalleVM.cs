using System.Collections.Generic;

namespace controlLuces.Models
{
    public class OrdenCerradaDetalleVM
    {
        public OrdenModel Orden { get; set; } = new OrdenModel();
        public OrdenCerradaModel Cierre { get; set; } = new OrdenCerradaModel();
        public List<TrabajoRealizadoModel> Trabajos { get; set; } = new List<TrabajoRealizadoModel>();
        public List<ImagenOrdenServicioModel> Imagenes { get; set; } = new List<ImagenOrdenServicioModel>();
    }
}
