using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class infraestructuraModel
    {
        public string codigo { get; set; }
        public float latitud { get; set; }
        public float longitud { get; set; }
        public string direccion { get; set; }
        public string configuracion { get; set; }
        public string fabricante { get; set; }
        public string linea { get; set; }
        public string barrio { get; set; }
        public string potencia { get; set; }
        public string tipo { get; set; }
        public string municipio { get; set; }
        public int? IdMunicipio { get; set; }   
    }
}