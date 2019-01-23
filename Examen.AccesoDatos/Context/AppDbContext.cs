using Examen.Dominio.Entidades;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examen.AccesoDatos.Context
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<Trabajador> Trabajadores { get; set; }
        public DbSet<Actividad> Actividades { get; set; }
        public DbSet<Tarea> Tareas { get; set; }

        public AppDbContext()
           : base("ExamenConnection", throwIfV1Schema: false)
        {
            this.Database.Log = s => System.Diagnostics.Debug.WriteLine(s);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public static AppDbContext Create()
        {
            return new AppDbContext();
        }

    }
}
