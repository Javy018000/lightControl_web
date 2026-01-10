using System.Collections.Generic;

namespace controlLuces.Models
{
    public class OrdenCerradaModel
    {
        public int Id_Orden_Cerrada { get; set; }
        public int Id_Orden_Servicio { get; set; }
        public string Observacion { get; set; }
        public int? Respuesta { get; set; }
        public byte[] Recursos { get; set; }
        public string MaterialesUsados { get; set; }

        public List<TrabajoRealizadoModel> Trabajos { get; set; } = new List<TrabajoRealizadoModel>();
        public List<ImagenOrdenServicioModel> Imagenes { get; set; } = new List<ImagenOrdenServicioModel>();
    }
}
