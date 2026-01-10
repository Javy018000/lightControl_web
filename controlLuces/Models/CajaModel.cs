using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace controlLuces.Models
{
    public class CajaModel
    {
        public string Codigo_CajaID { get; set; }
        public string ID_CAJA { get; set; }
        public string Codigo_Poste { get; set; }
        public string Codigo_Caja { get; set; }
        public string ID_Apoyo_Lum { get; set; }
        public string ID_Tranformador_LUM { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public int? Interdistancia { get; set; }
        public string Conectado_Con { get; set; }
        public string Ducteria { get; set; }
        public string BARRIO { get; set; }
        public string DIRECCION { get; set; }
        public string Archivo { get; set; }
        public string Observaciones { get; set; }
    }
}
