using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace controlLuces.Models
{
    public class InsumoRealizadoModel
    {
        public int IdInsumoRealizado { get; set; }
        public int IdOrdenCerrada { get; set; }   
        public string NombreInsumo { get; set; }
        public int Cantidad { get; set; }
    }
}
