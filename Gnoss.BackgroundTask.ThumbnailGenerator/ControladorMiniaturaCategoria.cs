using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.RabbitMQ;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Microsoft.Extensions.DependencyInjection;
using System;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.Web.MVC.Models.Tesauro;
using Es.Riam.Gnoss.ProcesadoTareas;
using Es.Riam.Gnoss.Elementos.Tesauro;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Gnoss.BackgroundTask.ThumbnailGenerator
{
	internal class ControladorMiniaturaCategoria : ControladorServicioGnoss
	{
        private readonly ILogger mlogger;
        private readonly ILoggerFactory mLoggerFactory;
        private RabbitMQClient mRabbitMQClient;
        public ControladorMiniaturaCategoria(IServiceScopeFactory serviceScope, ConfigService configService, ILogger<ControladorMiniaturaCategoria> logger, ILoggerFactory loggerFactory)
			: base(serviceScope, configService,logger,loggerFactory)
		{
            mlogger = logger;
            mLoggerFactory = loggerFactory;
        }
        public override void RealizarMantenimiento(EntityContext entityContext, EntityContextBASE entityContextBASE, UtilidadesVirtuoso utilidadesVirtuoso, LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            #region Establezco el dominio de la cache

            mDominio = GestorParametroAplicacionDS.ParametroAplicacion.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;
            mDominio = mDominio.Replace("http://", "").Replace("www.", "");

            if (mDominio[mDominio.Length - 1] == '/')
            {
                mDominio = mDominio.Substring(0, mDominio.Length - 1);
            }
            #endregion

            RealizarMantenimientoRabbitMQ(loggingService);
        }

        public void RealizarMantenimientoRabbitMQ(LoggingService loggingService, bool reintentar = true)
		{
			if (mConfigService.ExistRabbitConnection(RabbitMQClient.BD_SERVICIOS_WIN))
			{
				RabbitMQClient.ReceivedDelegate funcionProcesarItem = new RabbitMQClient.ReceivedDelegate(ProcesarItem);
				RabbitMQClient.ShutDownDelegate funcionShutDown = new RabbitMQClient.ShutDownDelegate(OnShutDown);
				mRabbitMQClient?.Dispose();
				mRabbitMQClient = new RabbitMQClient(RabbitMQClient.BD_SERVICIOS_WIN, "ColaMiniaturaCategoria", loggingService, mConfigService, mLoggerFactory.CreateLogger<RabbitMQClient>(), mLoggerFactory, "", "ColaMiniaturaCategoria");

				try
				{
					mRabbitMQClient.ObtenerElementosDeCola(funcionProcesarItem, funcionShutDown);
					mReiniciarLecturaRabbit = false;
				}
				catch (Exception ex)
				{
					mReiniciarLecturaRabbit = true;
					loggingService.GuardarLogError(ex,mlogger);
				}
			}
		}

		private bool ProcesarItem(string pFila)
		{
			using (var scope = ScopedFactory.CreateScope())
			{
				EntityContext entityContext = scope.ServiceProvider.GetRequiredService<EntityContext>();
				LoggingService loggingService = scope.ServiceProvider.GetRequiredService<LoggingService>();
				RedisCacheWrapper redisCacheWrapper = scope.ServiceProvider.GetRequiredService<RedisCacheWrapper>();
				ConfigService configService = scope.ServiceProvider.GetRequiredService<ConfigService>();
				IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication = scope.ServiceProvider.GetRequiredService<IServicesUtilVirtuosoAndReplication>();
				ComprobarTraza("ThumbnailGenerator", entityContext, loggingService, redisCacheWrapper, configService, servicesUtilVirtuosoAndReplication);
				try
				{
					ComprobarCancelacionHilo();
					ComprobarCancelacionHilo();

					System.Diagnostics.Debug.WriteLine($"ProcesarItem, {pFila}!");

					if (!string.IsNullOrEmpty(pFila))
					{
						ColaImagenCategoria elementoColaImagenCategoria = JsonSerializer.Deserialize<ColaImagenCategoria>(pFila);
						ProcesarFilaColaImagenCategoria(elementoColaImagenCategoria, entityContext, loggingService, configService);

						ControladorConexiones.CerrarConexiones(false);
					} 
					return true;
				}
				catch (Exception ex)
				{
					loggingService.GuardarLog(ex.Message, mlogger);
					return true;
				}
				finally
				{
					GuardarTraza(loggingService);
				}
			}
		}

		private void ProcesarFilaColaImagenCategoria(ColaImagenCategoria pElementoColaImagenCategoria, EntityContext pEntityContext, LoggingService pLoggingService, ConfigService pConfigService)
		{
			try
			{
                ControladorImagenes controladorImagenes = new ControladorImagenes(pConfigService.ObtenerUrlServicioInterno(), pConfigService.ObtenerTiempocapturaurl(), null, 2400, 2400, ScopedFactory, mConfigService, mLoggerFactory.CreateLogger<ControladorImagenes>(), mLoggerFactory);
                controladorImagenes.GenerarImagenesCategoria(pElementoColaImagenCategoria, pLoggingService);

				DataWrapperTesauro dwTesauro = new DataWrapperTesauro();
                GestionTesauro gestionTesauro = new GestionTesauro(dwTesauro, pLoggingService, pEntityContext, mLoggerFactory.CreateLogger<GestionTesauro>(), mLoggerFactory);
				gestionTesauro.IncrementarVersionFoto(pElementoColaImagenCategoria.CategoriaID);
            }
            catch (Exception ex)
			{
				pLoggingService.GuardarLogError(ex, $"Error al procesar la imagen de la Categoria: {pElementoColaImagenCategoria.CategoriaID}",mlogger);
			}
            
        }

		protected override ControladorServicioGnoss ClonarControlador()
		{
			return new ControladorMiniaturaCategoria(ScopedFactory, mConfigService, mLoggerFactory.CreateLogger<ControladorMiniaturaCategoria>(), mLoggerFactory);
		}
	}
}
