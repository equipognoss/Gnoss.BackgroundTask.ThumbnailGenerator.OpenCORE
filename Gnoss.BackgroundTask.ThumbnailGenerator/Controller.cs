using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using Es.Riam.Gnoss.AD.Documentacion;
using Es.Riam.Gnoss.AD.Parametro;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.CL.Documentacion;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.Elementos.Documentacion;
using Es.Riam.Gnoss.Logica.BASE_BD;
using Es.Riam.Gnoss.Logica.Documentacion;
using Es.Riam.Gnoss.Logica.Parametro;
using Es.Riam.Gnoss.Logica.ParametroAplicacion;
using Es.Riam.Gnoss.Logica.ParametrosProyecto;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.ProcesadoTareas;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Web.MVC.Models;
using Es.Riam.Gnoss.AD.EntityModel;
using System.Linq;
using Es.Riam.Gnoss.Elementos.ParametroAplicacion;

using Es.Riam.Gnoss.AD.EntityModel.Models.ParametroGeneralDS;
using Es.Riam.Gnoss.Web.Controles.ParametroAplicacionGBD;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.RabbitMQ;
using Es.Riam.Util;
using Es.Riam.Gnoss.Util.General;
using Newtonsoft.Json;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.AD.EntityModel.Models.Documentacion;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Microsoft.Extensions.DependencyInjection;
using Es.Riam.Gnoss.Web.Controles.ServicioImagenesWrapper;
using Es.Riam.Gnoss.Util.Configuracion;
using Microsoft.EntityFrameworkCore;
using Es.Riam.AbstractsOpen;

namespace Es.Riam.Gnoss.ServicioMantenimiento
{
    internal class Controller : ControladorServicioGnoss
    {

        #region Miembros

        /// <summary>
        /// Tiempo de espera entre captura y captura de una URL.
        /// </summary>
        public static int TIEMPOCAPTURAURL_SEGUNDOS;

        /// <summary>
        /// DataSet de documentación con los documentos en cola.
        /// </summary>
        private DataWrapperDocumentacion mDocumentoColaDW;


        /// <summary>
        /// URL del servicio de imágenes.
        /// </summary>
        private string mURLServicioImagenesEcosistema;

        private string mFicheroConfiguracionBD_Controller;

        private long mEstadoCargaID = -1;
        private int mTareaID = -1;

        /// <summary>
        /// Lista con el tamanio de las caputuras configuradas para cada proyecto.
        /// </summary>
        private Dictionary<Guid, string> mTamanioCapturasProyecto = new Dictionary<Guid, string>();

        private Dictionary<Guid, string> mListaUrlServiciosIntragnossPorProyecto = null;

        #endregion

        #region Constructores

        public Controller(IServiceScopeFactory serviceScope, ConfigService configService)
            : base(serviceScope, configService)
        {

        }

        #endregion

        #region Metodos generales

        #region publicos

        private bool ProcesarItem(string pFila)
        {
            using (var scope = ScopedFactory.CreateScope())
            {
                EntityContext entityContext = scope.ServiceProvider.GetRequiredService<EntityContext>();
                LoggingService loggingService = scope.ServiceProvider.GetRequiredService<LoggingService>();
                VirtuosoAD virtuosoAD = scope.ServiceProvider.GetRequiredService<VirtuosoAD>();
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
                        ColaDocumento filaColaDocumento = JsonConvert.DeserializeObject<ColaDocumento>(pFila);

                        DocumentacionCN docCN = new DocumentacionCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);

                        DataWrapperDocumentacion dataWrapperDocumentacion = docCN.ObtenerDocumentosColaDocumentoRabbitMQ(filaColaDocumento.DocumentoID);
                        dataWrapperDocumentacion.ListaColaDocumento.Add(filaColaDocumento);

                        ProcesarFilaColaDocumento(dataWrapperDocumentacion, entityContext, loggingService, redisCacheWrapper, virtuosoAD, servicesUtilVirtuosoAndReplication);

                        ControladorConexiones.CerrarConexiones(false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    loggingService.GuardarLog(ex.Message);
                    return true;
                }
                finally
                {
                    GuardarTraza(loggingService);
                }
            }
        }

        private void ProcesarFilaColaDocumento(DataWrapperDocumentacion pDataWrapperDocumentacion, EntityContext entityContext, LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            try
            {
                List<int> listaTareas = new List<int>();
                foreach (ColaDocumento filaColaDoc in pDataWrapperDocumentacion.ListaColaDocumento)
                {
                    listaTareas.Add(filaColaDoc.ID);
                }
                //Trato los documentos Agregados y Modificados
                foreach (int tareaID in listaTareas)
                {
                    ColaDocumento filaColaDoc = pDataWrapperDocumentacion.ListaColaDocumento.FirstOrDefault(doc => doc.ID.Equals(tareaID));
                    
                    AD.EntityModel.Models.Documentacion.Documento filaDoc = pDataWrapperDocumentacion.ListaDocumento.FirstOrDefault(doc => doc.DocumentoID.Equals(filaColaDoc.DocumentoID));

                    if (filaColaDoc.EstadoCargaID.HasValue)
                    {
                        mEstadoCargaID = filaColaDoc.EstadoCargaID.Value;
                        mTareaID = tareaID;
                    }

                    string mensaje = "documento '";
                    
                    if (filaDoc != null && (filaColaDoc.AccionRealizada == (short)AccionHistorialDocumento.Agregar || filaColaDoc.AccionRealizada == (short)AccionHistorialDocumento.GuardarDocumento))
                    {
                        if (filaDoc.ProyectoID.HasValue && !mTamanioCapturasProyecto.ContainsKey(filaDoc.ProyectoID.Value))
                        {
                            ProyectoCL proyectoCL = new ProyectoCL(entityContext, loggingService, redisCacheWrapper, mConfigService, virtuosoAD, servicesUtilVirtuosoAndReplication);
                            Dictionary<string, string> paramsProy = proyectoCL.ObtenerParametrosProyecto(filaDoc.ProyectoID.Value);
                            proyectoCL.Dispose();

                            if (paramsProy.ContainsKey(ParametroAD.CaputurasImgSize))
                            {
                                mTamanioCapturasProyecto.Add(filaDoc.ProyectoID.Value, paramsProy[ParametroAD.CaputurasImgSize]);
                            }
                            else
                            {
                                mTamanioCapturasProyecto.Add(filaDoc.ProyectoID.Value, null);

                                loggingService.GuardarLog("Aniade proyecto al diccionario");
                            }
                        }

                        int anchoCap = 180;
                        int altoCap = 160;

                        if (!string.IsNullOrEmpty(mTamanioCapturasProyecto[filaDoc.ProyectoID.Value]))
                        {
                            int.TryParse(mTamanioCapturasProyecto[filaDoc.ProyectoID.Value].Split(',')[0], out anchoCap);
                            int.TryParse(mTamanioCapturasProyecto[filaDoc.ProyectoID.Value].Split(',')[1], out altoCap);
                        }

                        string urlServicioImagenes = mURLServicioImagenesEcosistema;

                        if (ListaUrlServiciosIntragnossPorProyecto(entityContext, loggingService, servicesUtilVirtuosoAndReplication).ContainsKey(filaDoc.ProyectoID.Value))
                        {
                            urlServicioImagenes = ListaUrlServiciosIntragnossPorProyecto(entityContext, loggingService, servicesUtilVirtuosoAndReplication)[filaDoc.ProyectoID.Value] + "image-service";
                        }

                        if (filaDoc.Tipo == (short)TiposDocumentacion.Hipervinculo)
                        {
                            #region Hipervínculo
                            try
                            {
                                mensaje += filaDoc.DocumentoID + "' de tipo Hipervinculo se ha procesado";

                                //Obtengo la Imagen de la URL:
                                ControladorImagenes contrImg = new ControladorImagenes(urlServicioImagenes, TIEMPOCAPTURAURL_SEGUNDOS, mFicheroConfiguracionBD_Controller, anchoCap, altoCap, ScopedFactory, mConfigService);

                                bool capturaCorrecta = false;
                                bool esPaginaMini = false;

                                //Si no es MyGnoss
                                if (filaDoc.ProyectoID != ProyectoAD.MyGnoss)
                                {
                                    ParametroAplicacionCN paramCN = new ParametroAplicacionCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                                    GestorParametroAplicacion gestorParametroAplicacion = new GestorParametroAplicacion();
                                    ParametroAplicacionGBD parametroAplicacionGBD = new ParametroAplicacionGBD(loggingService, entityContext, mConfigService);
                                    parametroAplicacionGBD.ObtenerConfiguracionGnoss(gestorParametroAplicacion);
                                    //string urlIntraGnoss = (string)paramCN.ObtenerConfiguracionGnoss().ParametroAplicacion.Select("Parametro = 'UrlIntragnoss'")[0]["Valor"];
                                    string urlIntraGnoss = gestorParametroAplicacion.ParametroAplicacion.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;

                                    capturaCorrecta = contrImg.ObtenerImagenDesdeDescripcion(filaDoc.DocumentoID, filaDoc.Descripcion, urlIntraGnoss, loggingService, mConfigService, entityContext);
                                }

                                //Si no obtenemos imagen de la descripción cogemos la Web completa
                                //if (!capturaCorrecta)
                                //{
                                //    capturaCorrecta = contrImg.ObtenerImagenURLDocumento(filaDoc.DocumentoID, filaDoc.Enlace, loggingService);
                                //    esPaginaMini = true;
                                //}
                                contrImg.Dispose();

                                if (capturaCorrecta)
                                {
                                    //Si la captura es correcta aniadimos 1 a la version de la foto del documento (si se trata de una captura de la descripcion comienza a partir de 1000)
                                    if (!filaDoc.VersionFotoDocumento.HasValue)
                                    {
                                        if (!esPaginaMini)
                                        {
                                            filaDoc.VersionFotoDocumento = 1001;
                                        }
                                        else
                                        {
                                            filaDoc.VersionFotoDocumento = 1;
                                        }
                                    }
                                    else
                                    {
                                        int versionActual = filaDoc.VersionFotoDocumento.Value;
                                        if (versionActual > 1000)
                                        {
                                            versionActual = versionActual - 1000;
                                        }

                                        if (!esPaginaMini)
                                        {
                                            filaDoc.VersionFotoDocumento = 1000 + versionActual + 1;
                                        }
                                        else
                                        {
                                            filaDoc.VersionFotoDocumento = versionActual + 1;
                                        }
                                    }

                                    //Pongo la tarea como procesada:                                   
                                    EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                    mensaje += " con exito.";
                                }
                                else
                                {
                                    AgregarEstadoErrorAElementoCola(filaColaDoc);
                                    mensaje += " SIN exito.";
                                }
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }
                            #endregion
                        }
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.Nota || (filaDoc.Tipo == (short)TiposDocumentacion.FicheroServidor && !filaDoc.Enlace.Contains("slideshare.")))
                        {
                            #region Nota
                            try
                            {
                                #region Nota

                                mensaje += filaDoc.DocumentoID + "' de tipo Nota se ha procesado";

                                //Obtengo la Imagen de la URL:
                                ControladorImagenes contrImg = new ControladorImagenes(urlServicioImagenes, TIEMPOCAPTURAURL_SEGUNDOS, mFicheroConfiguracionBD_Controller, anchoCap, altoCap, ScopedFactory, mConfigService);

                                bool capturaCorrecta = false;

                                //Si no es MyGnoss
                                if (filaDoc.ProyectoID != ProyectoAD.MyGnoss)
                                {
                                    ParametroAplicacionCN paramCN = new ParametroAplicacionCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                                    GestorParametroAplicacion gestorParametroAplicacion = new GestorParametroAplicacion();
                                    ParametroAplicacionGBD parametroAplicacionGBD = new ParametroAplicacionGBD(loggingService, entityContext, mConfigService);
                                    parametroAplicacionGBD.ObtenerConfiguracionGnoss(gestorParametroAplicacion);
                                    string urlIntraGnoss = gestorParametroAplicacion.ParametroAplicacion.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;

                                    capturaCorrecta = contrImg.ObtenerImagenDesdeDescripcion(filaDoc.DocumentoID, filaDoc.Descripcion, urlIntraGnoss, loggingService, mConfigService, entityContext);
                                }

                                contrImg.Dispose();

                                if (capturaCorrecta || !contrImg.TieneImagenEnDescripcion)
                                {
                                    if (capturaCorrecta)
                                    {
                                        //Si la captura es correcta aniadimos 1 a la version de la foto del documento
                                        if (!filaDoc.VersionFotoDocumento.HasValue)
                                        {
                                            filaDoc.VersionFotoDocumento = 1;
                                        }
                                        else
                                        {
                                            //puede estar retomando una imagen y el valor estaría en negativo
                                            filaDoc.VersionFotoDocumento = Math.Abs(filaDoc.VersionFotoDocumento.Value) + 1;
                                        }
                                    }
                                    else if (VersionFotoDocumentoNegativo && !contrImg.TieneImagenEnDescripcion)
                                    {
                                        //si no tiene imágenes en la descripción y tenía versión la ponemos en negativo para poder recuperarla
                                        if (filaDoc.VersionFotoDocumento.HasValue && filaDoc.VersionFotoDocumento > 0)
                                        {
                                            filaDoc.VersionFotoDocumento = (-1) * filaDoc.VersionFotoDocumento;
                                        }
                                    }

                                    //Pongo la tarea como procesada:
                                    EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                    mensaje += " con exito.";
                                }
                                else
                                {
                                    AgregarEstadoErrorAElementoCola(filaColaDoc);
                                    mensaje += " SIN exito.";
                                }

                                #endregion
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }
                            #endregion
                        }
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.Imagen)
                        {
                            #region Imagen
                            try
                            {
                                mensaje += filaDoc.DocumentoID + "' de tipo Imagen se ha procesado";

                                Guid personaID = Guid.Empty;
                                Guid organizacionID = Guid.Empty;
                                Guid baseRecursosID = pDataWrapperDocumentacion.ListaDocumentoWebVinBaseRecursos.FirstOrDefault(doc => doc.DocumentoID.Equals(filaColaDoc.DocumentoID) && doc.TipoPublicacion.Equals((short)TipoPublicacion.Publicado)).BaseRecursosID;
                                if (pDataWrapperDocumentacion.ListaBaseRecursosUsuario.Where(baseRec => baseRec.BaseRecursosID.Equals(baseRecursosID)).Count() > 0)
                                {
                                    //Es BR de persona:
                                    Guid usuarioID = pDataWrapperDocumentacion.ListaBaseRecursosUsuario.FirstOrDefault(baseRec => baseRec.BaseRecursosID.Equals(baseRecursosID)).UsuarioID;

                                    PersonaCN personaCN = new PersonaCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                                    DataWrapperPersona personaDW = personaCN.ObtenerPersonaPorUsuario(usuarioID, false, false);
                                    personaCN.Dispose();

                                    personaID = personaDW.ListaPersona.FirstOrDefault(pers => pers.UsuarioID.Equals(usuarioID)).PersonaID;
                                }
                                else if (pDataWrapperDocumentacion.ListaBaseRecursosOrganizacion.Count(baseRec => baseRec.BaseRecursosID.Equals(baseRecursosID)) > 0)
                                {
                                    //Es BR de organización:
                                    organizacionID = pDataWrapperDocumentacion.ListaBaseRecursosOrganizacion.FirstOrDefault(baseRec => baseRec.BaseRecursosID.Equals(baseRecursosID)).OrganizacionID;
                                }

                                //Obtengo la mini-imagen para la ficha reducid:
                                ControladorImagenes contrImg = new ControladorImagenes(urlServicioImagenes, TIEMPOCAPTURAURL_SEGUNDOS, mFicheroConfiguracionBD_Controller, anchoCap, altoCap, ScopedFactory, mConfigService);

                                string extension = ".jpg";
                                if (filaDoc.NombreElementoVinculado == "Wiki2")
                                {
                                    extension = filaDoc.Enlace.Substring(filaDoc.Enlace.LastIndexOf('.'));
                                }

                                bool imagenReducida = contrImg.ObtenerImagenMiniaturaDocumento(filaColaDoc.DocumentoID, personaID, organizacionID, extension, loggingService);
                                contrImg.Dispose();

                                if (imagenReducida)
                                {
                                    //Si la captura es correcta aniadimos 1 a la version de la foto del documento
                                    if (!filaDoc.VersionFotoDocumento.HasValue)
                                    {
                                        filaDoc.VersionFotoDocumento = 1;
                                    }
                                    else
                                    {
                                        filaDoc.VersionFotoDocumento++;
                                    }

                                    //Pongo la tarea como procesada:
                                    EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                    mensaje += " con exito.";
                                }
                                else
                                {
                                    AgregarEstadoErrorAElementoCola(filaColaDoc);
                                    mensaje += " SIN exito.";
                                }
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }
                            #endregion
                        }
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.Video)
                        {
                            #region Video
                            try
                            {
                                string enlace = filaDoc.Enlace;
                                if (enlace.StartsWith("https://"))
                                {
                                    enlace = enlace.Replace("https://", "http://");
                                }

                                #region Youtube
                                if (((enlace.StartsWith("http://www.youtube.com") || enlace.StartsWith("http://youtube.com") || enlace.StartsWith("www.youtube.com")) && (enlace.Contains("/watch?") || enlace.Contains("/embed/"))) || enlace.StartsWith("http://youtu.be/"))
                                {
                                    //Youtube
                                    mensaje += filaDoc.DocumentoID + "' de tipo Video youtube se ha procesado";
                                    #region Captura mini youtube

                                    //Obtengo la Imagen de la URL:
                                    bool capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, ObtenerUrlImagenYoutube(enlace), filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);
                                    if (capturaCorrecta)
                                    {
                                        mensaje += " con exito.";
                                    }
                                    else
                                    {
                                        mensaje += " SIN exito.";
                                    }
                                    #endregion
                                }
                                #endregion

                                #region Vimeo
                                else if (enlace.StartsWith("http://www.vimeo.com") || enlace.StartsWith("http://vimeo.com") || enlace.StartsWith("www.vimeo.com"))
                                {
                                    string v = (new Uri(enlace)).AbsolutePath;
                                    long idVideo;
                                    int inicio = v.LastIndexOf("/");
                                    bool exito = long.TryParse(v.Substring(inicio + 1, v.Length - inicio - 1), out idVideo);

                                    if (exito)
                                    {
                                        //Vimeo
                                        mensaje += filaDoc.DocumentoID + "' de tipo Video vimeo se ha procesado";
                                        #region Captura mini Vimeo

                                        //Obtengo la Imagen de la URL:
                                        bool capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, ObtenerUrlImagenVimeo(enlace, (int)idVideo, entityContext, mConfigService, loggingService), filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);


                                        if (capturaCorrecta)
                                        {
                                            mensaje += " con exito.";
                                        }
                                        else
                                        {
                                            mensaje += " SIN exito.";
                                        }
                                        #endregion
                                    }
                                }
                                #endregion

                                #region TED
                                else if (enlace.StartsWith("http://www.ted.com/talks/") || enlace.StartsWith("www.ted.com/talks/") || enlace.StartsWith("ted.com/talks/") || enlace.StartsWith("http://tedxtalks.ted.com/video/") || enlace.StartsWith("tedxtalks.ted.com/video/"))
                                {
                                    string imagenTed = ObtenerUrlImagenTed(enlace, loggingService);

                                    // Dependiendo de la página que sea la url de la imagen la cogemos de un sitio diferente de la cabecera
                                    if (enlace.StartsWith("http://www.ted.com/talks/") || enlace.StartsWith("www.ted.com/talks/") || enlace.StartsWith("ted.com/talks/"))
                                    {
                                        mensaje += filaDoc.DocumentoID + "' de tipo Video TED www.ted.com/talks se ha procesado";
                                    }
                                    else if (enlace.StartsWith("http://tedxtalks.ted.com/video/") || enlace.StartsWith("tedxtalks.ted.com/video/"))
                                    {
                                        mensaje += filaDoc.DocumentoID + "' de tipo Video TED tedxtalks.ted.com/video/ se ha procesado";
                                    }

                                    #region Captura de la imagen
                                    // Obtener la imagen de la url
                                    bool capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, imagenTed, filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);

                                    if (capturaCorrecta)
                                    {
                                        mensaje += " con exito.";
                                    }
                                    else
                                    {
                                        mensaje += " SIN exito.";
                                    }
                                    #endregion
                                }
                                #endregion

                                #region Fichero servidor que no debe ser tratado
                                else
                                {
                                    //Lo borro de la cola ya que no debe ser tratado:
                                    mensaje += filaColaDoc.DocumentoID + "' ha sido descartado.";
                                    EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);

                                }
                                #endregion
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }
                            #endregion
                        }
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.FicheroServidor && filaDoc.Enlace.Contains("slideshare."))
                        {
                            #region Slideshare
                            try
                            {
                                #region Slideshare

                                //Slideshare
                                mensaje += filaDoc.DocumentoID + "' de tipo Slideshare se ha procesado";
                                #region Captura mini Slideshare

                                //Obtengo la Imagen de la URL:
                                bool capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, ObtenerUrlImagenSlideshare(filaDoc.Enlace, loggingService), filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);

                                if (capturaCorrecta)
                                {
                                    mensaje += " con exito.";
                                }
                                else
                                {
                                    mensaje += " SIN exito.";
                                }
                                #endregion

                                #endregion
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }
                            #endregion
                        }//Ya ha sido procesado el video o hace mas de dos dias q existe
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.VideoBrightcove)
                        {
                            if (filaDoc.Enlace != "")
                            {
                                #region Video Brightcove
                                try
                                {
                                    mensaje += filaDoc.DocumentoID + "' de tipo Video brightcove se ha procesado";
                                    #region Captura mini Brightcove

                                    string urlImagen = "";

                                    #region Obtengo la Url de la imagen

                                    ParametroGeneralCN paramGeneralCN = new ParametroGeneralCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                                    ParametroGeneral filaParametrosGenerales = paramGeneralCN.ObtenerFilaParametrosGeneralesDeProyecto(filaDoc.ProyectoID.Value);

                                    string tokenLectura = filaParametrosGenerales.BrightcoveTokenRead;
                                    string tokenEscritura = filaParametrosGenerales.BrightcoveTokenWrite;

                                    #endregion

                                    if (urlImagen != "" || filaDoc.FechaCreacion < DateTime.Now.AddDays(-3))
                                    {

                                        //Obtengo la Imagen de la URL:
                                        bool capturaCorrecta = false;

                                        if (urlImagen != "")
                                        {
                                            capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, urlImagen, filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);
                                        }
                                        
                                        if (capturaCorrecta)
                                        {
                                            mensaje += " con exito.";
                                        }
                                        else
                                        {
                                            mensaje += " SIN exito.";
                                        }
                                    }
                                    else
                                    {
                                        mensaje += " pero aún no existe el video en brightcove.";
                                    }
                                    #endregion
                                }
                                catch (Exception ex)
                                {
                                    AgregarEstadoErrorAElementoCola(filaColaDoc);
                                    mensaje += " SIN exito con fallo.";

                                    loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                                }
                                #endregion
                            }
                            else if (filaDoc.FechaCreacion < DateTime.Now.AddDays(-2))
                            {
                                //Lo borro de la cola ya que no debe ser tratado:
                                mensaje += "Video brightcove " + filaColaDoc.DocumentoID + "' descartado porque hace más de 2 días que está y no tiene foto.";
                                EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                            }
                            else if (filaDoc.FechaCreacion < DateTime.Now.AddHours(-1))
                            {
                                //Si en 1 hora no se ha hecho la captura pierde prioridad para que no estorbe
                                filaColaDoc.Prioridad = 1;
                            }
                        }//Ya ha sido procesado el video o hace mas de dos dias q existe
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.VideoTOP)
                        {
                            mensaje += filaDoc.DocumentoID + "'";
                            if (filaDoc.Enlace != "")
                            {
                                #region Video TOP
                                try
                                {
                                    mensaje += " de tipo Video TOP se ha procesado";
                                    #region Captura mini TOP

                                    string urlImagen = "";

                                    #region Obtengo la Url de la imagen

                                    ParametroGeneralCN paramGeneralCN = new ParametroGeneralCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                                    ParametroGeneral filaParametrosGenerales = paramGeneralCN.ObtenerFilaParametrosGeneralesDeProyecto(filaDoc.ProyectoID.Value);

                                    WebRequest requestPic = WebRequest.Create("http://fapi-top.prisasd.com/api/" + filaParametrosGenerales.TOPIDCuenta + "/" + filaDoc.Enlace + "/");
                                    requestPic.Headers.Add("UserAgent", UtilWeb.GenerarUserAgent());
                                    WebResponse responsePic = requestPic.GetResponse();
                                    TextReader reader = new StreamReader(responsePic.GetResponseStream());
                                    string jsonTOPString = reader.ReadToEnd();
                                    JsonTOPApi JsonTOPApi = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonTOPApi>(jsonTOPString);

                                    if (!string.IsNullOrEmpty(JsonTOPApi.url_video_still))
                                    {
                                        urlImagen = JsonTOPApi.url_video_still;
                                    }

                                    #endregion

                                    if (urlImagen != "" || filaDoc.FechaCreacion < DateTime.Now.AddDays(-3))
                                    {
                                        bool capturaCorrecta = false;
                                        if (urlImagen != "")
                                        {
                                            capturaCorrecta = ObtenerImagenUrl(urlServicioImagenes, anchoCap, altoCap, filaDoc, urlImagen, filaColaDoc, pDataWrapperDocumentacion, entityContext, loggingService);
                                        }

                                        if (capturaCorrecta)
                                        {
                                            mensaje += " con exito.";
                                        }
                                        else
                                        {
                                            mensaje += " SIN exito.";
                                        }
                                    }
                                    else
                                    {
                                        mensaje += " pero aún no existe el video en TOP.";
                                    }
                                    #endregion
                                }
                                catch (Exception ex)
                                {
                                    AgregarEstadoErrorAElementoCola(filaColaDoc);
                                    mensaje += " SIN exito con fallo.";

                                    loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                                }
                                #endregion
                            }
                            else if (filaDoc.FechaCreacion < DateTime.Now.AddDays(-2))
                            {
                                //Lo borro de la cola ya que no debe ser tratado:
                                mensaje += "Video top " + filaColaDoc.DocumentoID + "' descartado porque hace más de 2 días que está y no tiene foto.";
                                EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                            }
                            else if (filaDoc.FechaCreacion < DateTime.Now.AddHours(-1))
                            {
                                //Si en 1 hora no se ha hecho la captura pierde prioridad para que no estorbe
                                filaColaDoc.Prioridad = 1;
                                mensaje += " de tipo Video TOP aun no tiene captura";
                            }
                            else
                            {
                                mensaje += " de tipo Video TOP aun no tiene captura";
                            }
                        }
                        else if (filaDoc.Tipo == (short)TiposDocumentacion.Semantico)
                        {
                            #region Semántico
                            try
                            {
                                string[] infoExtra = filaColaDoc.InfoExtra.Split(new char[] { '|' });

                                if (infoExtra != null && infoExtra[0] != "")
                                {
                                    // Si el recurso es OpenSeaDragon tratamos la imagen con la librería y generamos las imágenes.
                                    if (infoExtra[0].Equals("OpenSeaDragon"))
                                    {
                                        PartirImagenOpenSeaDragon(infoExtra[1], infoExtra[1], urlServicioImagenes, loggingService);
                                        //Pongo la tarea como procesada:
                                        EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                        mensaje += " con exito.";
                                    }
                                    else
                                    {
                                        string urlImagen = infoExtra[0].ToString().Trim();
                                        string rutaDestino = infoExtra[1].ToString().Trim();
                                        string tamaniosImagenesString = infoExtra[2].ToString().Trim();

                                        string extraImagenDoc = null;

                                        if (infoExtra.Length > 3)
                                        {
                                            extraImagenDoc = "|" + infoExtra[3].ToString().Trim();
                                        }


                                        string[] tamaniosImagenes = tamaniosImagenesString.Split(new char[] { ',' });

                                        List<int> tamaniosImagenesInt = new List<int>();
                                        foreach (string tamanio in tamaniosImagenes)
                                        {
                                            tamaniosImagenesInt.Add(Convert.ToInt32(tamanio.Trim()));
                                        }

                                        string nuevaRutaDestino = "";
                                        string[] rutaDestinoArray = rutaDestino.Split(new char[] { '/' });
                                        for (int i = 0; i < rutaDestinoArray.Length - 1; i++)
                                        {
                                            nuevaRutaDestino += "/" + rutaDestinoArray[i];
                                        }
                                        if (nuevaRutaDestino.StartsWith("/"))
                                        {
                                            nuevaRutaDestino = nuevaRutaDestino.Substring(1);
                                        }
                                        nuevaRutaDestino = "../" + nuevaRutaDestino + "/";
                                        string mFileNameCExtension = rutaDestinoArray[rutaDestinoArray.Length - 1];
                                        string mFileName = mFileNameCExtension.Substring(0, mFileNameCExtension.Length - ".jpg".Length);

                                        if (urlImagen.StartsWith("https://"))
                                        {
                                            urlImagen = urlImagen.Replace("https://", "http://");
                                        }

                                        //1º Obtenemos el byte[] de la imagen
                                        if (((urlImagen.StartsWith("http://www.youtube.com") || urlImagen.StartsWith("http://youtube.com") || urlImagen.StartsWith("www.youtube.com")) && (urlImagen.Contains("/watch?") || urlImagen.Contains("/embed/"))) || urlImagen.StartsWith("http://youtu.be/"))
                                        {
                                            #region Youtube
                                            urlImagen = ObtenerUrlImagenYoutube(urlImagen);
                                            mensaje += filaDoc.DocumentoID + "' de tipo Semántico Youtube se ha procesado";
                                            #endregion
                                        }
                                        else if (urlImagen.StartsWith("http://www.vimeo.com") || urlImagen.StartsWith("http://vimeo.com") || urlImagen.StartsWith("www.vimeo.com"))
                                        {
                                            #region Vimeo
                                            string v = (new Uri(urlImagen)).AbsolutePath;
                                            int idVideo;
                                            int inicio = v.LastIndexOf("/");
                                            bool exito = int.TryParse(v.Substring(inicio + 1, v.Length - inicio - 1), out idVideo);

                                            if (exito)
                                            {
                                                //Vimeo
                                                mensaje += filaDoc.DocumentoID + "' de tipo Semántico Vimeo se ha procesado";
                                                urlImagen = ObtenerUrlImagenVimeo(urlImagen, idVideo, entityContext, mConfigService, loggingService);

                                            }

                                            #endregion
                                        }
                                        else if (urlImagen.Contains("slideshare."))
                                        {
                                            #region Slideshare

                                            mensaje += filaDoc.DocumentoID + "' de tipo Semántico Slideshare se ha procesado";

                                            urlImagen = ObtenerUrlImagenSlideshare(urlImagen, loggingService);

                                            #endregion
                                        }
                                        else if (urlImagen.StartsWith("http://www.ted.com/talks/") || urlImagen.StartsWith("www.ted.com/talks/") || urlImagen.StartsWith("ted.com/talks/") || urlImagen.StartsWith("http://tedxtalks.ted.com/video/") || urlImagen.StartsWith("tedxtalks.ted.com/video/"))
                                        {
                                            #region TED

                                            mensaje += filaDoc.DocumentoID + "' de tipo Semántico TED se ha procesado";
                                            urlImagen = ObtenerUrlImagenTed(urlImagen, loggingService);

                                            #endregion
                                        }
                                        else
                                        {
                                            #region WEB
                                            mensaje += filaDoc.DocumentoID + "' de tipo Semántico Imagen se ha procesado";
                                            #endregion
                                        }

                                        if (!tamaniosImagenesInt.Contains(anchoCap))
                                        {
                                            tamaniosImagenesInt.Add(anchoCap);
                                        }

                                        ControladorImagenes contrImg = new ControladorImagenes(urlServicioImagenes, TIEMPOCAPTURAURL_SEGUNDOS, mFicheroConfiguracionBD_Controller, anchoCap, altoCap, ScopedFactory, mConfigService);
                                        bool capturaConExito = contrImg.ObtenerImagenSemanticaDesdeURLAImagen(filaDoc.DocumentoID, urlImagen, tamaniosImagenesInt, nuevaRutaDestino, mFileNameCExtension, mFileName, loggingService);
                                        contrImg.Dispose();


                                        if (capturaConExito)
                                        {
                                            filaDoc.NombreCategoriaDoc = "";

                                            foreach (int tamanio in tamaniosImagenesInt)
                                            {
                                                filaDoc.NombreCategoriaDoc += tamanio + ",";
                                            }

                                            filaDoc.NombreCategoriaDoc += rutaDestino + extraImagenDoc;

                                            //Pongo la tarea como procesada:
                                            EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                            mensaje += " con exito.";
                                        }
                                        else
                                        {
                                            mensaje += filaColaDoc.DocumentoID + "' SIN exito con fallo. No se pudo capturar la imagen";
                                            AgregarEstadoErrorAElementoCola(filaColaDoc);
                                        }
                                    }
                                }
                                else
                                {
                                    //Lo borro de la cola ya que no debe ser tratado por no tener información extra:
                                    mensaje += filaColaDoc.DocumentoID + "' ha sido descartado porque no tiene información extra.";
                                    EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                                }
                            }
                            catch (Exception ex)
                            {
                                AgregarEstadoErrorAElementoCola(filaColaDoc);
                                mensaje += " SIN exito con fallo.";

                                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                            }

                            #endregion
                        }
                        else
                        {
                            //Lo borro de la cola ya que no debe ser tratado:
                            mensaje += filaColaDoc.DocumentoID + "' ha sido descartado.";
                            EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                        }
                    }
                    else
                    {
                        //Lo borro de la cola ya que no debe ser tratado:

                        mensaje += filaColaDoc.DocumentoID + "' ha sido descartado.";
                        EliminarFilaColaDocumento(pDataWrapperDocumentacion, filaColaDoc, entityContext);
                    }

                    DocumentacionCN docCN = new DocumentacionCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                    docCN.ActualizarDocumentacion();
                    docCN.Dispose();

                    if (filaDoc != null && filaDoc.ProyectoID.HasValue)
                    {
                        DocumentacionCL docCL = new DocumentacionCL(mFicheroConfiguracionBD, "recursos", entityContext, loggingService, redisCacheWrapper, mConfigService, servicesUtilVirtuosoAndReplication);
                        docCL.InvalidarFichaRecursoMVC(filaDoc.DocumentoID, filaDoc.ProyectoID.Value);
                    }

                    loggingService.GuardarLog(mensaje);
                }
            }
            catch (Exception ex)
            {
                loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
            }
        }

        private bool ObtenerImagenUrl(string pUrlServicioImagenes, int pAnchoCap, int pAltoCap, AD.EntityModel.Models.Documentacion.Documento pDocumento, string pEnlace, ColaDocumento pColaDocumento, DataWrapperDocumentacion pDataWrapperDocumentacion, EntityContext entityContext, LoggingService loggingService)
        {
            ControladorImagenes contrImg = new ControladorImagenes(pUrlServicioImagenes, TIEMPOCAPTURAURL_SEGUNDOS, mFicheroConfiguracionBD_Controller, pAnchoCap, pAltoCap, ScopedFactory, mConfigService);
            bool capturaCorrecta = contrImg.ObtenerImagenDesdeURLAImagen(pDocumento.DocumentoID, pEnlace, loggingService);

            contrImg.Dispose();

            if (capturaCorrecta)
            {
                //Si la captura es correcta aniadimos 1 a la version de la foto del documento
                if (!pDocumento.VersionFotoDocumento.HasValue)
                {
                    pDocumento.VersionFotoDocumento = 1;
                }
                else
                {
                    pDocumento.VersionFotoDocumento++;
                }

                //Pongo la tarea como procesada:
                EliminarFilaColaDocumento(pDataWrapperDocumentacion, pColaDocumento, entityContext);
            }
            else
            {
                AgregarEstadoErrorAElementoCola(pColaDocumento);
            }

            return capturaCorrecta;
        }


        private void EliminarFilaColaDocumento(DataWrapperDocumentacion pDataWrapperDocumentacion, ColaDocumento pFilaColaDoc, EntityContext entityContext)
        {
            pDataWrapperDocumentacion.ListaColaDocumento.Remove(pFilaColaDoc);
            if (!entityContext.Entry(pFilaColaDoc).State.Equals(EntityState.Detached))
            {
                entityContext.Entry(pFilaColaDoc).State = Microsoft.EntityFrameworkCore.EntityState.Deleted;
            }
        }


        public void RealizarMantenimientoRabbitMQ(LoggingService loggingService, bool reintentar = true)
        {  
            if (mConfigService.ExistRabbitConnection(RabbitMQClient.BD_SERVICIOS_WIN))
            {
                CargarDatos();
                RabbitMQClient.ReceivedDelegate funcionProcesarItem = new RabbitMQClient.ReceivedDelegate(ProcesarItem);
                RabbitMQClient.ShutDownDelegate funcionShutDown = new RabbitMQClient.ShutDownDelegate(OnShutDown);

                RabbitMQClient rabbitMQClient = new RabbitMQClient(RabbitMQClient.BD_SERVICIOS_WIN, "ColaMiniatura", loggingService, mConfigService, "", "ColaMiniatura");

                try
                {
                    rabbitMQClient.ObtenerElementosDeCola(funcionProcesarItem, funcionShutDown);
                    mReiniciarLecturaRabbit = false;
                }
                catch (Exception ex)
                {
                    mReiniciarLecturaRabbit = true;
                    loggingService.GuardarLogError(ex);
                }
            }
        }
        
        /// <summary>
        /// Procesa cada suscripcion para crear su notificacion correspondiente
        /// </summary>
        public override void RealizarMantenimiento(EntityContext entityContext, EntityContextBASE entityContextBASE, UtilidadesVirtuoso utilidadesVirtuoso, LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            #region Establezco el dominio de la cache

            //mDominio = ((ParametroAplicacionDS.ParametroAplicacionRow)ParametroAplicacionDS.ParametroAplicacion.Select("Parametro='UrlIntragnoss'")[0]).Valor;
            mDominio = GestorParametroAplicacionDS.ParametroAplicacion.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;
            mDominio = mDominio.Replace("http://", "").Replace("www.", "");

            if (mDominio[mDominio.Length - 1] == '/')
            {
                mDominio = mDominio.Substring(0, mDominio.Length - 1);
            }
            #endregion


            RealizarMantenimientoRabbitMQ(loggingService);
        }

        #endregion

        #region OpenSeaDragon
        /// <summary>
        /// Divide una imagen de gran tamanio utilizando la librería openseadragon
        ///  
        /// </summary>
        /// <param name="rutaImagen">
        /// Ruta de la imagen a dividir
        /// </param>
        /// <param name="rutaGuardar">
        /// Ruta donde guardará las carpetas generadas
        /// </param>
        private void PartirImagenOpenSeaDragon(string pRutaImagen, string pRutaGuardar, string pUrlServicioImagenes, LoggingService loggingService)
        {
            string extension = pRutaImagen.Substring(pRutaImagen.LastIndexOf("."));
            ServicioImagenes servicioImagenes = new ServicioImagenes(loggingService, mConfigService);
            servicioImagenes.Url = pUrlServicioImagenes;
            string urlObtServ = pRutaImagen.ToLower().Replace("imagenes/", "");
            urlObtServ = urlObtServ.Substring(0, urlObtServ.LastIndexOf("."));
            string rutaTemporales = Path.GetTempPath();
            string rutaTemporalesImagenesOpenSeaDragon = rutaTemporales + urlObtServ.Substring(urlObtServ.LastIndexOf("/") + 1);
            string rutaImagenTemporal = rutaTemporales + urlObtServ.Substring(urlObtServ.LastIndexOf("/") + 1) + extension;
            string rutaXMLTemporal = rutaImagenTemporal.Substring(0, rutaImagenTemporal.LastIndexOf(".")) + ".xml";

            // Obtenemos la imagen y la guardamos en un archivo temporal
            byte[] buffer = servicioImagenes.ObtenerImagen(urlObtServ, extension);
            Stream stream = new MemoryStream(buffer);
            System.Drawing.Image img = System.Drawing.Image.FromStream(stream);
            img.Save(rutaImagenTemporal);
            img.Dispose();
            stream.Close();
            stream.Dispose();

            //// Generamos las imágenes de OpenSeaDragon
            //SparseImageCreator s = new SparseImageCreator();
            //Microsoft.DeepZoomTools.Image imagen = new Microsoft.DeepZoomTools.Image(rutaImagenTemporal);
            //List<Microsoft.DeepZoomTools.Image> imagenes = new List<Microsoft.DeepZoomTools.Image>();
            //imagenes.Add(imagen);
            //s.Create(imagenes, rutaTemporalesImagenesOpenSeaDragon);

            // Recorremos las imágenes guardadas en una carpeta temporal y las subimos con el servicio imágenes
            DirectoryInfo dirImagenesTemporales = new DirectoryInfo(rutaTemporalesImagenesOpenSeaDragon + "_files");
            DirectoryInfo[] subDirImagenesTemporales = dirImagenesTemporales.GetDirectories();

            foreach (DirectoryInfo subDirectorio in subDirImagenesTemporales)
            {
                foreach (FileInfo imgOpenSeaDragon in subDirectorio.GetFiles("*.*"))
                {
                    System.Drawing.Image imgAux = System.Drawing.Image.FromFile(imgOpenSeaDragon.FullName);
                    MemoryStream ms = new MemoryStream();
                    imgAux.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
                    byte[] bufferImg = ms.ToArray();
                    Boolean correctoImagen = servicioImagenes.AgregarImagenADirectorio(bufferImg, urlObtServ + "/" + subDirectorio.Name, imgOpenSeaDragon.Name.Substring(0, imgOpenSeaDragon.Name.LastIndexOf(".")), imgOpenSeaDragon.Extension);
                    ms.Close();
                    ms.Dispose();
                    imgAux.Dispose();
                    if (!correctoImagen)
                    {
                        throw new Exception("Error al agregar imagen");
                    }
                }
            }

            // Borramos los temporales creados
            if (File.Exists(rutaImagenTemporal))
            {
                File.Delete(rutaImagenTemporal);
            }
            if (File.Exists(rutaXMLTemporal))
            {
                File.Delete(rutaXMLTemporal);
            }
            Directory.Delete(dirImagenesTemporales.FullName, true);

        }
        #endregion

        #region privados

        /// <summary>
        /// Carga los datos necesario para el funcionamiento del sistema.
        /// </summary>
        private void CargarDatos()
        {
            try
            {
                mURLServicioImagenesEcosistema = mConfigService.ObtenerUrlServicioInterno();                      
            }
            catch (Exception ex) { throw ex; }
        }

        /// <summary>
        /// Agrega o incrementa el estado de error de un documento.
        /// </summary>
        /// <param name="pFilaColaDoc">Fila de la cola de documento</param>
        private void AgregarEstadoErrorAElementoCola(AD.EntityModel.Models.Documentacion.ColaDocumento pFilaColaDoc)
        {
            if (pFilaColaDoc.Estado < 4)
            {
                //Agrego o aumento el número de error.
                pFilaColaDoc.Estado = (short)(pFilaColaDoc.Estado + 1);
            }
            else
            {
                //Pongo el elemento como fallido:
                pFilaColaDoc.Estado = (short)EstadoElementoCola.Fallido;
            }

            //Agrego la fecha de procesado:
            pFilaColaDoc.FechaProcesado = DateTime.Now;
        }

        /// <summary>
        /// Crea un entrada en el registro del sistema.
        /// </summary>
        /// <param name="pEstado">Estado del servicio</param>
        /// <param name="pDetalles">Detalles de funcionamiento</param>
        /// <returns>Entrarda del registro</returns>
        private String CrearEntradaRegistro(LogStatus pEstado, String pDetalles)
        {
            String entradaLog = String.Empty;
            switch (pEstado)
            {
                case LogStatus.Correcto:
                    entradaLog = "\r\n\t >> OK: ";
                    break;
                case LogStatus.Error:
                    entradaLog = "\r\n\t >> ALERT: ";
                    break;
                case LogStatus.NoCreadas:
                    entradaLog = "\r\n\t >> OK: ";
                    break;
            }
            return entradaLog + pDetalles;
        }

        private string ObtenerTagHeadHTML(string pUrl, LoggingService loggingService)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pUrl);
            string pageHtml = "";
            req.UserAgent = UtilWeb.GenerarUserAgent();
            HttpWebResponse resp = null;
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
                Stream istrm = resp.GetResponseStream();
                StreamReader rdr = new StreamReader(istrm, Encoding.UTF8, true);

                pageHtml = rdr.ReadToEnd();
                pageHtml = pageHtml.Substring(pageHtml.IndexOf("<head") + "<head".Length);
                pageHtml = pageHtml.Substring(0, pageHtml.IndexOf("</head>"));

                istrm.Dispose();
                rdr.Dispose();
                rdr.Close();
            }
            catch (Exception ex)
            {
                loggingService.GuardarLog("ERROR en la obtención de la cabecera:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
            }
            finally
            {
                if (resp != null) resp.Close();
            }

            return pageHtml;
        }

        #region URLImagenes

        /// <summary>
        /// Método privado para la obtención de la url de la imagen de los vídeos de Youtube
        /// </summary>
        /// <param name="pUrlVideo">Url del vídeo</param>
        /// <returns>Url de la imagen</returns>
        private string ObtenerUrlImagenYoutube(string pUrlVideo)
        {
            if (pUrlVideo.StartsWith("http://youtu.be/"))
            {
                return "http://i4.ytimg.com/vi/" + pUrlVideo.Replace("http://youtu.be/", "") + "/0.jpg";
            }
            else if (!string.IsNullOrEmpty(System.Web.HttpUtility.ParseQueryString(new Uri(pUrlVideo).Query).Get("v")))
            {
                return "http://i4.ytimg.com/vi/" + System.Web.HttpUtility.ParseQueryString(new Uri(pUrlVideo).Query).Get("v") + "/0.jpg";
            }
            else if (pUrlVideo.Contains("/embed/"))
            {
                string idVideo = pUrlVideo.Substring(pUrlVideo.IndexOf("/embed/") + 7);
                if (idVideo.Contains("?"))
                {
                    idVideo = idVideo.Substring(0, idVideo.IndexOf("?"));
                }
                return "http://i4.ytimg.com/vi/" + idVideo + "/0.jpg";
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Método privado para la obtención de la url de la imagen de los vídeos de VIMEO
        /// </summary>
        /// <param name="pUrlVideo">Url del vídeo</param>
        /// <returns>Url de la imagen</returns>
        private string ObtenerUrlImagenVimeo(string pUrlVideo, int pIdVideo, EntityContext pEntityContext, ConfigService pConfigService, LoggingService pLoggingService)
        {
            ParametroAplicacionCN paramCN = new ParametroAplicacionCN(pEntityContext, pLoggingService, pConfigService, null);
            string urlImagen = "";
            string token = paramCN.ObtenerParametroAplicacion("VimeoAccessToken");

            if (!string.IsNullOrEmpty(token))
            {
                string peticion = $"https://api.vimeo.com/videos/{pIdVideo}/pictures";
                string response = UtilGeneral.WebRequest("GET", peticion, token, null);
                string[] campos = response.Split(',');

                for (int i = 0; i < campos.Length; i++)
                {
                    if (campos[i].Contains("base_link"))
                    {
                        campos[i] = campos[i].Replace("\"", "");
                        urlImagen = campos[i].Replace("base_link:", "");
                        break;
                    }
                }
            }

            return urlImagen;
        }

        /// <summary>
        /// Método privado para la obtención de la url de la imagen de las presentaciones de slideshare
        /// </summary>
        /// <param name="pUrlVideo">Url de la presentación</param>
        /// <returns>Url de la imagen</returns>
        private string ObtenerUrlImagenSlideshare(string pUrlVideo, LoggingService loggingService)
        {
            string rutaImagen = "";

            string enlaceSlideshare = pUrlVideo;
            if (enlaceSlideshare.Contains("?"))
            {
                enlaceSlideshare = enlaceSlideshare.Substring(0, enlaceSlideshare.IndexOf("?"));
            }

            string ruta = "https://www.slideshare.net/api/oembed/2?url=" + enlaceSlideshare;

            try
            {
                XmlDocument docXml = new XmlDocument();
                docXml.Load(ruta);

                rutaImagen = docXml.SelectSingleNode("oembed/thumbnail-url").InnerText;
            }
            catch (Exception ex)
            {
                loggingService.GuardarLogError(ex);
            }

            //Corregimos el final de la URL
            if (!rutaImagen.EndsWith(".jpg") && rutaImagen.Contains("?"))
            {
                rutaImagen = LimpiarParametrosUrl(rutaImagen);
            }

            //// Guardar el XML descargado en algún Log
            //this.GuardarLog(xmlDownloaded);

            // Guardar la rutaImagen en algún Log
            loggingService.GuardarLog(rutaImagen);

            return rutaImagen;
        }

        private string LimpiarParametrosUrl(string pUrlVideo)
        {
            if (pUrlVideo.Contains("?"))
            {
                pUrlVideo = pUrlVideo.Substring(0, pUrlVideo.IndexOf("?"));
            }

            return pUrlVideo;
        }

        /// <summary>
        /// Método privado para la obtención de la url de la imagen de los vídeos de TED
        /// </summary>
        /// <param name="pUrlVideo">Url de la presentación</param>
        /// <returns>Url de la imagen</returns>
        private string ObtenerUrlImagenTed(string pUrlVideo, LoggingService loggingService)
        {
            string imagenTed = "";

            if (pUrlVideo.StartsWith("https://www.ted.com/talks/") || pUrlVideo.StartsWith("http://www.ted.com/talks/") || pUrlVideo.StartsWith("www.ted.com/talks/") || pUrlVideo.StartsWith("ted.com/talks/"))
            {
                #region www.ted.com
                //mensaje += filaDoc.DocumentoID + "' de tipo Video TED www.ted.com/talks se ha procesado";

                string headTagHTMLtedCom = ObtenerTagHeadHTML(pUrlVideo, loggingService);

                // La url de la imagen asociada al video viene en la propiedad <meta property="og:image" content= de la cabecera
                if (headTagHTMLtedCom.Contains("<meta property=\"og:image\" content=\""))
                {
                    headTagHTMLtedCom = headTagHTMLtedCom.Substring(headTagHTMLtedCom.IndexOf("<meta property=\"og:image\" content=\"") + "<meta property=\"og:image\" content=\"".Length);
                    headTagHTMLtedCom = headTagHTMLtedCom.Trim();
                    imagenTed = headTagHTMLtedCom.Substring(0, headTagHTMLtedCom.IndexOf("\""));
                }
                #endregion
            }
            else if (pUrlVideo.StartsWith("https://tedxtalks.ted.com/video/") || pUrlVideo.StartsWith("http://tedxtalks.ted.com/video/") || pUrlVideo.StartsWith("tedxtalks.ted.com/video/"))
            {
                #region tedxtalks.ted.com
                //mensaje += filaDoc.DocumentoID + "' de tipo Video TED tedxtalks.ted.com/video/ se ha procesado";
                string headTagHTMLtedxtalks = ObtenerTagHeadHTML(pUrlVideo, loggingService);

                // La url de la imagen asociada al video viene en la propiedad <link rel="image_src"> de la cabecera
                if (headTagHTMLtedxtalks.Contains("<link rel=\"image_src\""))
                {
                    imagenTed = headTagHTMLtedxtalks.Substring(headTagHTMLtedxtalks.IndexOf("<link rel=\"image_src\" href=\"") + "<link rel=\"image_src\" href=\"".Length);
                    imagenTed = imagenTed.Trim();
                    imagenTed = imagenTed.Substring(0, imagenTed.IndexOf("\""));
                }
                #endregion
            }

            return imagenTed;
        }

        #endregion

        #endregion

        protected override ControladorServicioGnoss ClonarControlador()
        {
            return new Controller(ScopedFactory, mConfigService);
        }

        #endregion

        #region Propiedades
        public Dictionary<Guid, string> ListaUrlServiciosIntragnossPorProyecto(EntityContext entityContext, LoggingService loggingService, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            if (mListaUrlServiciosIntragnossPorProyecto == null)
            {
                ParametroCN paramCN = new ParametroCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
                mListaUrlServiciosIntragnossPorProyecto = paramCN.ObtenerParametroDeProyectos(TiposParametrosAplicacion.UrlIntragnossServicios);
            }
            return mListaUrlServiciosIntragnossPorProyecto;
            
        }

        private bool VersionFotoDocumentoNegativo
        {
            get
            {
                bool usarVersionFotoDocumentoNegativo = false;
                //List<ParametroAplicacion> filasParam = GestorParametroAplicacionDS.ParametroAplicacion.Select("Parametro='" + TiposParametrosAplicacion.VersionFotoDocumentoNegativo + "'");
                List<ParametroAplicacion> filasParam = GestorParametroAplicacionDS.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.VersionFotoDocumentoNegativo)).ToList();

                if (filasParam != null && filasParam.Count > 0)
                {
                    //usarVersionFotoDocumentoNegativo = GestorParametroAplicacionDS.ParametroAplicacion.Select("Parametro='" + TiposParametrosAplicacion.VersionFotoDocumentoNegativo + "'")[0].Valor.ToLower().Equals("true");
                    usarVersionFotoDocumentoNegativo = GestorParametroAplicacionDS.ParametroAplicacion.Find(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.VersionFotoDocumentoNegativo)).Valor.ToLower().Equals("true");
                }

                return usarVersionFotoDocumentoNegativo;
            }
        }

        #endregion

    }
}