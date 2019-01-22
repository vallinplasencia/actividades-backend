﻿using Examen.App.DTOs;
using Examen.App.Models.BindingModels;
using Examen.App.Util;
using Examen.App.Util.Seguridad;
using Examen.Dominio.Abstracto;
using Examen.Dominio.Entidades;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;

namespace Examen.App.Controllers
{
    [RoutePrefix("api/actividades")]
    [Authorize(Roles = TiposRole.Admin)]
    public class ActividadesController : ApiController
    {
        private IActividadRepo repo;
        private ITrabajadorRepo repoTrabajadores;

        public ActividadesController(IActividadRepo repo, ITrabajadorRepo repoTrabajadores) : base()
        {
            this.repo = repo;
            this.repoTrabajadores = repoTrabajadores;
        }

        // GET: api/Actividades
        [HttpGet]
        [Route("")]
        [ResponseType(typeof(List<Actividad>))]
        public async Task<IHttpActionResult> GetActividades([FromUri]int _pagina = 1, [FromUri]int _limite = 50,
            [FromUri]string _ordenar = "titulo", [FromUri]string _orden = "asc", [FromUri]string _filtro = null
         )
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            //_orden = _orden.ToLower();
            string[] camposOrdenar = { "titulo", "estado", "fechaRegistro", "creadaPorNombre", "asignadaANombre"};
            string[] ordenValores = { "asc", "desc" };

            if (
                !camposOrdenar.Any(campo => campo == _ordenar)
                || !ordenValores.Any(ordValor => ordValor == _orden)
                || _pagina < 1 || _limite < 1)
            {
                ModelState.AddModelError("error", "Valores incorrectos en la query string");
                return BadRequest(ModelState);
            }
            var actividades = await repo.ListarAsync(_pagina - 1, _limite, _ordenar, _orden, _filtro);

            var resp = Request.CreateResponse<List<Actividad>>(HttpStatusCode.OK, actividades);
            resp.Headers.Add(Urls.HEADER_ACCESS_CONTROL_EXPOSE, Urls.MY_HEADER_TOTAL_COUNT);
            resp.Headers.Add(Urls.MY_HEADER_TOTAL_COUNT, repo.TotalActividades(_filtro).ToString());
            //return resp;
            return ResponseMessage(resp);
        }

        // GET: api/Actividades/5
        [HttpGet]
        [Route("{id}", Name = "GetActividad")]
        [ResponseType(typeof(Actividad))]
        public async Task<IHttpActionResult> GetActividad(int id)
        {
            Actividad actividad = await repo.GetActividadAsync(id);
            if (actividad == null)
            {
                return NotFound();
            }

            return Ok(actividad);
        }

        // PUT: api/Actividades/5
        [HttpPut]
        [Route("{id}")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> PutActividad(int id, ActividadEditarBM actividadBM)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (id != actividadBM.Id)
            {
                ModelState.AddModelError("error", "Los id no coinciden");
                return BadRequest(ModelState);
            }
            var actividadActual = await repo.GetActividadAsync(id);

            if (actividadActual == null)
            {
                return NotFound();
            }
            Actividad actividad = new Actividad
            {
                Id = actividadActual.Id,
                Titulo = actividadBM.Titulo,
                Descripcion = actividadBM.Descripcion,

                Estado = actividadBM.Estado,
                FechaRegistro = actividadBM.FechaRegistro,

                Tareas = actividadBM.Tareas.Select(t => new Tarea { Id = (int)t.Id, Nombre = t.Nombre, Porcentaje = t.Porcentaje, Realizada = t.Realizada }).ToList(),

                AsignadaAId = actividadBM.TrabajadorId,
                CreadaPorId = User.Identity.GetUserId(),

            };
            int rowAffectadas = await repo.SalvarAsync(actividad, actividadActual);

            switch (rowAffectadas)
            {
                case 0:
                    ModelState.AddModelError("error", "Los datos enviados son los mismos que los que se encuenetran guardados");
                    return BadRequest(ModelState);
                case -1:
                    return StatusCode(HttpStatusCode.InternalServerError);
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Actividades
        [HttpPost]
        [Route("")]
        [ResponseType(typeof(Actividad))]
        public async Task<IHttpActionResult> PostActividad(ActividadNuevaBM actividadBM)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            int porcentaje = 0;
            foreach (var t in actividadBM.Tareas) {
                porcentaje += t.Porcentaje;
            }
            if(porcentaje < 1 || porcentaje > 100)
            {
                ModelState.AddModelError("", "La sumatoria de los porcentajes que representa cada tarea debe de estar entre 1 y 100. Actualmente es " + porcentaje);
                return BadRequest(ModelState);
            }
            Actividad actividad = new Actividad {
                Titulo = actividadBM.Titulo,
                Descripcion = actividadBM.Descripcion,

                Estado = Dominio.Util.EstadosActividad.Asignada,
                FechaRegistro = DateTime.Now,

                Tareas = actividadBM.Tareas.Select(t => new Tarea { Nombre = t.Nombre, Porcentaje = t.Porcentaje }).ToList(),

                AsignadaAId = actividadBM.TrabajadorId,
                CreadaPorId = User.Identity.GetUserId(),
                
            };
            if (await repo.SalvarAsync(actividad, null) < 1)
            {
                return StatusCode(HttpStatusCode.InternalServerError);
            }
            return CreatedAtRoute("GetActividad", new { id = actividad.Id }, actividad);
        }

        // DELETE: api/Actividades/5
        [HttpDelete]
        [Route("{id}")]
        [ResponseType(typeof(Actividad))]
        public async Task<IHttpActionResult> DeleteActividad(int id)
        {
            var actividad = await repo.GetActividadAsync(id);

            if (actividad == null)
            {
                return NotFound();
            }
            actividad = await repo.RemoverAsync(actividad);

            if (actividad == null)
            {
                return StatusCode(HttpStatusCode.InternalServerError);
            }
            return Ok(actividad);
        }


        /********---------OTROS METODO DE ACCION.----------**********/

        /// <summary>
        /// Retorna los posibles valores de los campos que se llenan de la bd para dar de alta a una nueva actividad.
        /// 
        /// Los valores de los parametros del metodo se utilizan cuando se van a cargar 
        /// grandes cantidades de datos para los campos y hay q utilizar paginacion para ello.
        /// 
        /// </summary>
        /// <param name="_paginaResp">Numero de la pagina del paginado de la bd. Generalmente es la primera</param>
        /// <param name="_limiteResp">Cant de registros a retornas</param>
        /// <param name="_ordenarResp">Campo por el q se van a ordenar los resultados</param>
        /// <param name="_ordenResp">Orden asc o desc</param>
        /// <param name="_filtroResp">si se va a aplicar algun criterio de busqueda para los resultados.</param>
        /// <returns></returns>        
        [HttpGet()]
        [Route("actividad-campos")]
        [ResponseType(typeof(ActividadCamposDto))]
        public async Task<IHttpActionResult> getCamposDeActividad(
            [FromUri]int _paginaResp = 1,
            [FromUri]int _limiteResp = 50,
            [FromUri]string _ordenarResp = "nombre",
            [FromUri]string _ordenResp = "asc",
            [FromUri]string _filtroResp = null)
        {
            _ordenResp = _ordenResp.ToLower();
            string[] camposOrdenarResp = { "titulo", "estado", "fechaRegistro", "creadaPorNombre", "asignadaANombre" };
            string[] ordenValores = { "asc", "desc" };

            if (
                !camposOrdenarResp.Any(campo => campo == _ordenarResp)
                || !ordenValores.Any(ordValor => ordValor == _ordenResp)
                || _paginaResp < 1 || _limiteResp < 1)
            {
                ModelState.AddModelError("error", "Valores incorrectos en la query string");
                return BadRequest(ModelState);
            }
            //Response.Headers.Add(Urls.HEADER_TOTAL_COUNT_SEC_RESPONSABLES, repoResponsable.TotalResponsables(_filtroResp).ToString());
            
            var trabajadores = await repoTrabajadores.ListarAsync(_paginaResp - 1, _limiteResp, _ordenarResp, _ordenResp, _filtroResp);

            var resp = Request.CreateResponse<ActividadCamposDto>(HttpStatusCode.OK, new ActividadCamposDto
            {
                Trabajadores = trabajadores
            });

            resp.Headers.Add(Urls.HEADER_ACCESS_CONTROL_EXPOSE, Urls.MY_HEADER_TOTAL_COUNT_SEC_RESPONSABLES);
            resp.Headers.Add(Urls.MY_HEADER_TOTAL_COUNT_SEC_RESPONSABLES, repoTrabajadores.TotalTrabajadores(_filtroResp).ToString());

            return ResponseMessage(resp);
        }


        /// <summary>
        /// Retorna los posibles valores de los campos que se llenan de la bd para modificar la actividad previamente guardado.
        /// 
        /// Los valores de los parametros del metodo se utilizan cuando se van a cargar 
        /// grandes cantidades de datos para los campos de la actividad y hay q utilizar paginacion para ello.
        /// 
        /// </summary>
        /// <param name="id">Identificador del item a modificar</param>
        /// <param name="_paginaResp">Numero de la pagina del paginado de la bd. Generalmente es la primera</param>
        /// <param name="_limiteResp">Cant de registros a retornas</param>
        /// <param name="_ordenarResp">Campo por el q se van a ordenar los resultados</param>
        /// <param name="_ordenResp">Orden asc o desc</param>
        /// <param name="_filtroResp">i se va a aplicar algun criterio de busqueda para los resultados.</param>
        /// <returns></returns>
        [HttpGet()]
        [Route("actividad-y-campos/{id}")]
        [ResponseType(typeof(ActividadCamposDto))]
        public async Task<IHttpActionResult> GetActividadYCampos(
            [FromUri] int id, [FromUri]int _paginaResp = 1, [FromUri]int _limiteResp = 50,
            [FromUri]string _ordenarResp = "titulo", [FromUri]string _ordenResp = "asc", [FromUri]string _filtroResp = null
        )
        {
            var actividad = await repo.GetActividadAsync(id);

            if (actividad == null)
            {
                return NotFound();
            }
            _ordenResp = _ordenResp.ToLower();
            string[] camposOrdenarResp = { "titulo", "estado", "fechaRegistro", "creadaPorNombre", "asignadaANombre" };
            string[] ordenValores = { "asc", "desc" };

            if (
                !camposOrdenarResp.Any(campo => campo == _ordenarResp)
                || !ordenValores.Any(ordValor => ordValor == _ordenResp)
                || _paginaResp < 1 || _limiteResp < 1)
            {
                ModelState.AddModelError("error", "Valores incorrectos en la query string");
                return BadRequest(ModelState);
            }
            
            var trabajadores = await repoTrabajadores.ListarAsync(_paginaResp - 1, _limiteResp, _ordenarResp, _ordenResp, _filtroResp);

            var resp = Request.CreateResponse<ActividadCamposDto>(HttpStatusCode.OK, new ActividadCamposDto
            {
                Actividad = actividad,
                Trabajadores = trabajadores
            });

            resp.Headers.Add(Urls.HEADER_ACCESS_CONTROL_EXPOSE, Urls.MY_HEADER_TOTAL_COUNT_SEC_RESPONSABLES);
            resp.Headers.Add(Urls.MY_HEADER_TOTAL_COUNT_SEC_RESPONSABLES, repoTrabajadores.TotalTrabajadores(_filtroResp).ToString());

            return ResponseMessage(resp);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
