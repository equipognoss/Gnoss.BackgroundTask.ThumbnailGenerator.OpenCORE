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
        private readonly ILogger<ThumbnailGeneratorWorker> _logger;
        private readonly ConfigService _configService;

        public ThumbnailGeneratorWorker(ILogger<ThumbnailGeneratorWorker> logger, ConfigService configService, IServiceScopeFactory scopeFactory) : base(logger, scopeFactory)
        {
            _logger = logger;
            _configService = configService;
        }

        protected override List<ControladorServicioGnoss> ObtenerControladores()
        {
            Controller.TIEMPOCAPTURAURL_SEGUNDOS = _configService.ObtenerTiempocapturaurl();
            List<ControladorServicioGnoss> controladores = new List<ControladorServicioGnoss>();
            controladores.Add(new Controller(ScopedFactory, _configService));
            return controladores;
        }
    }
}
