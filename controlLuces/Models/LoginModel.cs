using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class LoginModel


    {
        public int IdUsuario { get; set; }
        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string Correo { get; set; }

        public Rol IdRol { get; set; }
        public string Clave { get; set; }




    }
}