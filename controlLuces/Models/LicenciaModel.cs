using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace controlLuces.Models
{
    public class LicenciaModel
    {
        public int Id { get; set; }
        public string ClaveLicencia { get; set; }
        public string NombreCliente { get; set; }
        public string Municipio { get; set; }
        public string NIT { get; set; }
        public string Contacto { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public int MaxUsuarios { get; set; }
        public string Estado { get; set; }
        public string ModulosPermitidos { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public DateTime? FechaUltimaValidacion { get; set; }
        public string Observaciones { get; set; }

        // Propiedades calculadas
        public bool EstaActiva
        {
            get { return Estado == "Activa" && FechaExpiracion > DateTime.Now; }
        }

        public int DiasRestantes
        {
            get
            {
                if (FechaExpiracion > DateTime.Now)
                    return (FechaExpiracion - DateTime.Now).Days;
                return 0;
            }
        }

        public bool EstaProximaAExpirar
        {
            get { return DiasRestantes <= 30 && DiasRestantes > 0; }
        }

        public List<string> ListaModulos
        {
            get
            {
                if (string.IsNullOrEmpty(ModulosPermitidos))
                    return new List<string>();
                return ModulosPermitidos.Split(',').ToList();
            }
        }

        public bool TieneModulo(string modulo)
        {
            return ListaModulos.Contains(modulo, StringComparer.OrdinalIgnoreCase);
        }
    }

    public class ValidacionLicenciaResult
    {
        public bool Valida { get; set; }
        public string Resultado { get; set; }
        public string Mensaje { get; set; }
        public DateTime? FechaExpiracion { get; set; }
        public string NombreCliente { get; set; }
        public string ModulosPermitidos { get; set; }
        public int MaxUsuarios { get; set; }
    }

    public class LicenciaHistorialModel
    {
        public int Id { get; set; }
        public int IdLicencia { get; set; }
        public DateTime FechaValidacion { get; set; }
        public string IP { get; set; }
        public string Usuario { get; set; }
        public string Resultado { get; set; }
        public string Detalle { get; set; }
    }
}
