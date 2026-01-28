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

    /// <summary>
    /// ViewModel para la Hoja de Vida de una Luminaria
    /// Incluye datos del elemento y su historial de órdenes de servicio
    /// </summary>
    public class HojaDeVidaLuminariaViewModel
    {
        public infraestructuraModel Luminaria { get; set; }
        public List<OrdenHistorialModel> HistorialOrdenes { get; set; } = new List<OrdenHistorialModel>();
        public List<ImagenElementoModel> Imagenes { get; set; } = new List<ImagenElementoModel>();

        // Estadísticas
        public int TotalOrdenes => HistorialOrdenes?.Count ?? 0;
        public int OrdenesActivas => HistorialOrdenes?.Count(o => o.IdEstado != 3) ?? 0;
        public int OrdenesCerradas => HistorialOrdenes?.Count(o => o.IdEstado == 3) ?? 0;
    }

    /// <summary>
    /// Modelo simplificado para el historial de órdenes
    /// </summary>
    public class OrdenHistorialModel
    {
        public int IdOrden { get; set; }
        public string CodigoOrden { get; set; }
        public string TipoDeOrden { get; set; }
        public string ClaseDeOrden { get; set; }
        public string ProblemaRelacionado { get; set; }
        public string Cuadrilla { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public DateTime FechaARealizar { get; set; }
        public int IdEstado { get; set; }
        public string EstadoNombre { get; set; }
        public string Trabajos { get; set; }
        public string Observaciones { get; set; }
    }
}