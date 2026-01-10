using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class UsuarioModel
    {
        // GET: UsuarioModel
        public int IdUsuario { get; set; }
        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string Correo { get; set; }

        public Rol IdRol { get; set; }
        public string Clave { get; set; }

        public string Municipio { get; set; }
        public string Cuadrilla { get; set; }  


    }
}