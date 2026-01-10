using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class CuadrillaModel 
    {
        // GET: CuadrillaModel


        public int Id_Cuadrilla { get; set; }
        public string Nombre { get; set; }
        public string Municipio { get; set; }
        public string Clave { get; set; }
        public int OrdenesAsociadas { get; set; }



    }
}