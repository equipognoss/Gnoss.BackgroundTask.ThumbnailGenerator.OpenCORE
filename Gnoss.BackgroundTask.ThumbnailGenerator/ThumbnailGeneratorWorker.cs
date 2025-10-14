using Es.Riam.Gnoss.Elementos.Suscripcion;
using Es.Riam.Gnoss.ServicioMantenimiento;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Util.Configuracion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gnoss.BackgroundTask.ThumbnailGenerator
{
    public class ThumbnailGeneratorWorker : Worker
    {
        private readonly ConfigService _configService;
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        public ThumbnailGeneratorWorker(ConfigService configService, IServiceScopeFactory scopeFactory, ILogger<ThumbnailGeneratorWorker> logger, ILoggerFactory loggerFactory) : base(logger, scopeFactory)
        {
            _configService = configService;
            mlogger = logger;
            mLoggerFactory = loggerFactory;
        }

        protected override List<ControladorServicioGnoss> ObtenerControladores()
        {
            Controller.TIEMPOCAPTURAURL_SEGUNDOS = _configService.ObtenerTiempocapturaurl();
            List<ControladorServicioGnoss> controladores = new List<ControladorServicioGnoss>();
            controladores.Add(new Controller(ScopedFactory, _configService, mLoggerFactory.CreateLogger<Controller>(), mLoggerFactory));
            controladores.Add(new ControladorMiniaturaCategoria(ScopedFactory, _configService, mLoggerFactory.CreateLogger<ControladorMiniaturaCategoria>(), mLoggerFactory));
            return controladores;
        }
    }
}
