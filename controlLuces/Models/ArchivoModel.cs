using System;

namespace controlLuces.Models
{
    public class ArchivoModel
    {
        public int Id { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaArchivo { get; set; }
        public string TipoArchivo { get; set; }
        public DateTime? FechaCarga { get; set; }
    }
}
