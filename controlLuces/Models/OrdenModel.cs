using System;
using System.Collections.Generic;

namespace controlLuces.Models
{
    public class OrdenModel
    {
        public int IdOrden { get; set; }
        public string TipoDeElemento { get; set; }
        public string CodigoDeElemento { get; set; }
        public string ElementoRelacionado { get; set; }
        public string CodigoOrden { get; set; }
        public string ProblemaRelacionado { get; set; }
        public string ProblemaValidado { get; set; }
        public string OrdenPrioridad { get; set; }
        public int PrioridadDeRuta { get; set; }
        public DateTime FechaARealizar { get; set; }
        public DateTime? FechaCreacion { get; set; }  // Fecha exacta de creación de la orden
        public string Cuadrilla { get; set; }
        public string TipoDeOrden { get; set; }
        public string TipoDeSolucion { get; set; }
        public string ClaseDeOrden { get; set; }
        public string ObraRelacionada { get; set; }
        public int IdEstado { get; set; }
        public string EstadoNombre { get; set; }
        public string Descripcion { get; set; }
        public int Idpqrs { get; set; }
        public string observaciones { get; set; }
        public string Trabajos { get; set; }
        // Consecutivo interno por municipio (1, 2, 3, 4...)
        public int? ConsecutivoMunicipio { get; set; }

        // Municipio al que pertenece la orden (Madrid / Chía)
        public string Municipio
        {
            get; set;
        }
        public List<ImagenOrdenServicioModel> Imagenes { get; set; } = new List<ImagenOrdenServicioModel>();
    }

    public class ImagenOrdenServicioModel
    {
        public int IdImagen { get; set; }         // dbo.ImagenesOrdenesDeServicio.id_imagen
        public int IdOrden { get; set; }          // ...id_orden
        public int? Id_Orden_Cerrada { get; set; } // <- puede venir NULL
        public byte[] Imagen { get; set; }        // ...imagen
        public DateTime? FechaSubida { get; set; } // <- puede venir NULL
    }
}
