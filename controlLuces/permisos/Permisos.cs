using controlLuces.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web;

namespace controlLuces.permisos
{
    public class PermisosRolAttribute : ActionFilterAttribute
    {
        private Rol[] rolesPermitidos;

        // Constructor que acepta múltiples roles
        public PermisosRolAttribute(params Rol[] roles)
        {
            rolesPermitidos = roles;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (HttpContext.Current.Session["Usuario"] != null)
            {
                UsuarioModel usuario = HttpContext.Current.Session["Usuario"] as UsuarioModel;

                // Verificar si el rol del usuario está en los roles permitidos
                bool tieneAcceso = false;
                foreach (var rol in rolesPermitidos)
                {
                    if (usuario.IdRol == rol)
                    {
                        tieneAcceso = true;
                        break;
                    }
                }

                if (!tieneAcceso)
                {
                    filterContext.Result = new RedirectResult("~/Home/Contact");
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
