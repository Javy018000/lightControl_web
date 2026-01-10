using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace controlLuces.Models
{
    public class ImagenElementoModel
    {
        public int IdImagen { get; set; }
        public string CodigoElemento { get; set; }
        public string RutaArchivo { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
