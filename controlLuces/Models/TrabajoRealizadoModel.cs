using System;

namespace controlLuces.Models
{
    public class TrabajoRealizadoModel
    {
        public int IdTrabajoRealizado { get; set; }
        public int IdOrdenCerrada { get; set; }
        public string Descripcion { get; set; }
        public string Detalle { get; set; }
        public decimal? Cantidad { get; set; }   // decimal(18,4)
    }
}
