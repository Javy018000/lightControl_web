using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace controlLuces.Models
{
    public class TransformadorModel
    {
        public int id { get; set; }
        public string codigo_apoyo { get; set; }
        public string pintado_apoyo { get; set; }
        public float latitud { get; set; }
        public float longitud { get; set; }
        public string nombre_estructura { get; set; }
        public string tipo_fase { get; set; }
        public string potencia_kva { get; set; }
        public string fabricante { get; set; }
        public string observaciones { get; set; }
    
    }
}
