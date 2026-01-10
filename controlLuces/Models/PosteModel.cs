using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace controlLuces.Models
{
    public class PosteModel
    {
        public string codigo { get; set; }
        public string rev_1 { get; set; }
        public string poste { get; set; }
        public int? interdistancia_pos { get; set; }
        public float latitud { get; set; }
        public float longitud { get; set; }
        public string barrio { get; set; }
        public string direccion { get; set; }
        public string tipo_red { get; set; }
        public string archivo { get; set; }
        public string observaciones { get; set; }
    }
}
