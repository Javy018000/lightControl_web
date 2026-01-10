using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace controlLuces.Models
{
    public class PqrsModel

    {
        public int Idpqrs { get; set; }
        public string Consecutivo { get; set; }
        public string FechaRegistro { get; set; } 
        public string Tipopqrs { get; set; }
        public string Canal { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string TipoDoc { get; set; }
        public string Documento { get; set; }
      
        public string Telefono { get; set; }
       
        public string Correo { get; set; }
        public string Referencia { get; set; }
        public string DireccionAfectacion { get; set; }
        public string BarrioAfectacion { get; set; }
        public string TipoAlumbrado { get; set; }
        public string DescripcionAfectacion { get; set; }
        public byte[] img { get; set; }
        public int Estado { get; set; }
        public string EstadoNombre { get; set; }
        public string ImagenDataUrl { get; set; }
        public string DatosRelacionados { get; set; }
        
        public int? ConsecutivoMunicipio { get; set; }   
        public string CodigoPqrs { get; set; }
        public string Cuadrilla { get; set; }
        public DateTime? FechaARealizar { get; set; }
    }
   }
    




    
