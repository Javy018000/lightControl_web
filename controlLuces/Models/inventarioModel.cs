using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class inventarioModel
    { 
        // GET: ineventarioModel
        public int ID { get; set; }
        public string nombre_elemento { get; set; }
        public int cantidad { get; set; }
        public string estado { get; set; }
        public string descripcion { get; set; }

    }
}