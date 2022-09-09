using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using Es.Riam.Util;
using System.Globalization;
using System.Net;
using Es.Riam.Gnoss.Logica.ParametroAplicacion;
using System.Xml;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Web.Controles.ServicioImagenesWrapper;
using Microsoft.Extensions.DependencyInjection;
using Es.Riam.Gnoss.Util.Configuracion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Es.Riam.Gnoss.ProcesadoTareas
{
    /// <summary>
    /// Clase para gestionar imágenes.
    /// </summary>
    public class ControladorImagenes : ControladorServicioGnoss, IDisposable
    {
        #region Miembros

        /// <summary>
        /// URL del servicio de imágenes.
        /// </summary>
        private string mURLServicioImagenes;

        /// <summary>
        /// URL para hacer una captura web.
        /// </summary>
        private string mUrl;

        /// <summary>
        /// Nombre del fichero de la captura.
        /// </summary>
        private string mFileName;

        /// <summary>
        /// Servicio de Imágenes.
        /// </summary>
        private ServicioImagenes mServicioImagenes;

        /// <summary>
        /// Indica si la captura de una imagen web se ha realizado correctamente o no.
        /// </summary>
        private bool mCapturaRealizada;

        /// <summary>
        /// Tiempo en milisegundos que se esperará a que se realice una captura de una web.
        /// </summary>
        private int mTiempoCapturaURL = 60000;

        /// <summary>
        /// Imagen capturada de la web
        /// </summary>
        private byte[] mImagen;

        /// <summary>
        /// Imagen capturada de la web
        /// </summary>
        private byte[] mImagenMax;

        /// <summary>
        /// Alto thumb para la imagen capturada de la web.
        /// </summary>
        public int ThumbHeightImg;

        /// <summary>
        /// Ancho thumb para la imagen capturada de la web.
        /// </summary>
        public int ThumbWidthImg;

        /// <summary>
        /// Máximo tamanio para las mini-imágenes generadas.
        /// </summary>
        public int MAX_SIZE_IMG;

        #region Miembros estáticos

        /// <summary>
        /// Ancho para la imagen capturada de la web.
        /// </summary>
        public static int WidthImg = 1024;

        /// <summary>
        /// Alto para la imagen capturada de la web.
        /// </summary>
        public static int HeightImg = 768;

        /// <summary>
        /// Timeout de la página en segundos
        /// </summary>
        public static int Timeout = 25;

        /// <summary>
        /// Path para guardar la captura web.
        /// </summary>
        public string AbsolutePath = "../imagenesEnlaces";

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor público.
        /// </summary>
        /// <param name="pURLServicioImagenes">URL del servicio de imágenes</param>
        /// <param name="pTiempoCapturaURL">Tiempo de espera para la captura de la web (en segundos)</param>
        /// <param name="pAnchoCap">Ancho para las capturas</param>
        /// <param name="pAltoCap">Alto para las capturas</param>
        public ControladorImagenes(string pURLServicioImagenes, int pTiempoCapturaURL, string pFicheroConfiguracionBD, int pAnchoCap, int pAltoCap, IServiceScopeFactory scopeFactory, ConfigService configService)
            : this(pFicheroConfiguracionBD, pAnchoCap, pAltoCap, scopeFactory, configService)
        {
            mURLServicioImagenes = pURLServicioImagenes;
            mTiempoCapturaURL = pTiempoCapturaURL * 1000;
        }

        /// <summary>
        /// Constructor público.
        /// </summary>
        /// <param name="pURLServicioImagenes">URL del servicio de imágenes</param>
        /// <param name="pAnchoCap">Ancho para las capturas</param>
        /// <param name="pAltoCap">Alto para las capturas</param>
        public ControladorImagenes(string pFicheroConfiguracionBD, int pAnchoCap, int pAltoCap, IServiceScopeFactory scopedFactory, ConfigService configService)
            : base(scopedFactory, configService)
        {
            ThumbWidthImg = pAnchoCap;
            MAX_SIZE_IMG = pAnchoCap;
            ThumbHeightImg = pAltoCap;
        }
        #endregion

        #region Métodos

        #region Calculo imagenes Web

        ///// <summary>
        ///// Obtiene la imagen de la URL de un documento.
        ///// </summary>
        ///// <param name="pDocumentoID">Identificador del documento</param>
        ///// <param name="pUrl">URL a capturar</param>
        ///// <returns>TRUE si la captura se ha realizado correctamente, FALSE en caso contrario</returns>
        //public bool ObtenerImagenURLDocumento(Guid pDocumentoID, string pUrl, LoggingService loggingService)
        //{
        //    mServicioImagenes = new ServicioImagenes(loggingService);
        //    mServicioImagenes.Url = mURLServicioImagenes;

        //    this.mUrl = pUrl;

        //    this.AbsolutePath = "../" + UtilArchivos.ContentImagenesEnlaces + "/" + UtilArchivos.DirectorioDocumento(pDocumentoID);
        //    this.mFileName = pDocumentoID.ToString();
        //    this.mCapturaRealizada = false;

        //    if (AbsolutePath != null)
        //    {
        //        Thread t = new Thread(new ThreadStart(_GetScreenShot));
        //        t.SetApartmentState(ApartmentState.STA);
        //        t.Start();
        //        //Thread.Sleep(mTiempoCapturaURL + 5000);

        //        int tiempoEsperado = 0;

        //        while (tiempoEsperado < mTiempoCapturaURL && !mCapturaRealizada)
        //        {
        //            Thread.Sleep(500);
        //            System.Windows.Forms.Application.DoEvents();
        //            tiempoEsperado += 500;
        //        }

        //        if (t.IsAlive)
        //        {
        //            t.Abort();
        //        }
        //    }

        //    if (mServicioImagenes != null)
        //    {
        //        mServicioImagenes = null;
        //    }
        //    return mCapturaRealizada;
        //}

        ///// <summary>
        ///// Obtiene la imagen de la URL de un documento.
        ///// </summary>
        ///// <param name="pDocumentoID">Identificador del documento</param>
        ///// <param name="pUrl">URL a capturar</param>
        ///// <returns>TRUE si la captura se ha realizado correctamente, FALSE en caso contrario</returns>
        //public byte[] ObtenerImagenURLDocumentoDevuelveImagen(string pUrl)
        //{
        //    mServicioImagenes = new ServicioImagenes();
        //    mServicioImagenes.Url = mURLServicioImagenes;

        //    this.mUrl = pUrl;

        //    Thread t = new Thread(new ThreadStart(_GetScreenShotDevuelveImagen));
        //    t.SetApartmentState(ApartmentState.STA);
        //    t.Start();
        //    //Thread.Sleep(mTiempoCapturaURL + 5000);

        //    int tiempoEsperado = 0;

        //    while (tiempoEsperado < mTiempoCapturaURL && !mCapturaRealizada)
        //    {
        //        Thread.Sleep(500);
        //        System.Windows.Forms.Application.DoEvents();
        //        tiempoEsperado += 500;
        //    }

        //    if (t.IsAlive)
        //    {
        //        t.Abort();
        //    }


        //    if (mServicioImagenes != null)
        //    {
        //        mServicioImagenes.Dispose();
        //        mServicioImagenes = null;
        //    }
        //    return mImagen;
        //}

        ///// <summary>
        ///// Captura la imagen de una web.
        ///// </summary>
        //private void _GetScreenShot()
        //{
        //    try
        //    {
        //        mWebBrowser = new System.Windows.Forms.WebBrowser();
        //        mWebBrowser.ScrollBarsEnabled = false;
        //        mWebBrowser.ScriptErrorsSuppressed = true;
        //        mWebBrowser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
        //        mWebBrowser.Navigate(mUrl);

        //        int tiempoEsperado = 0;

        //        while (tiempoEsperado < mTiempoCapturaURL && !mCapturaRealizada)
        //        {
        //            Thread.Sleep(500);
        //            System.Windows.Forms.Application.DoEvents();
        //            tiempoEsperado += 500;
        //        }

        //        if (mWebBrowser != null)
        //        {
        //            mWebBrowser.Dispose();
        //            mWebBrowser = null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        this.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
        //    }
        //}

        ///// <summary>
        ///// Devuelve la imagen capturada por una web
        ///// </summary>
        //private void _GetScreenShotDevuelveImagen()
        //{
        //    try
        //    {
        //        mImagen = null;
        //        mImagenMax = null;
        //        mWebBrowser = new System.Windows.Forms.WebBrowser();
        //        mWebBrowser.ScrollBarsEnabled = false;
        //        mWebBrowser.ScriptErrorsSuppressed = true;
        //        mWebBrowser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompletedDevuelveImagen);

        //        mWebBrowser.Navigate(mUrl);

        //        int tiempoEsperado = 0;

        //        while (tiempoEsperado < mTiempoCapturaURL && !mCapturaRealizada)
        //        {
        //            Thread.Sleep(500);
        //            System.Windows.Forms.Application.DoEvents();
        //            tiempoEsperado += 500;
        //        }

        //        if (mWebBrowser != null)
        //        {
        //            mWebBrowser.Dispose();
        //            mWebBrowser = null;
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        this.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);

        //    }
        //}

        ///// <summary>
        ///// Se ejecuta al completarse la captura de una web.
        ///// </summary>
        ///// <param name="sender">El que produce el evento</param>
        ///// <param name="e">Argumentos del evento</param>
        //private void WebBrowser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        //{
        //    try
        //    {
        //        mWebBrowser = (System.Windows.Forms.WebBrowser)sender;
        //        mWebBrowser.ClientSize = new System.Drawing.Size(WidthImg, HeightImg);
        //        mWebBrowser.ScrollBarsEnabled = false;
        //        System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(mWebBrowser.Bounds.Width, mWebBrowser.Bounds.Height);
        //        mWebBrowser.BringToFront();
        //        mWebBrowser.DrawToBitmap(bmp, mWebBrowser.Bounds);


        //        //Para las capturas de pantalla, la mejor manera es con la clase Graphics, no pierde calidad.
        //        //System.Drawing.Image img = bmp.GetThumbnailImage(ThumbWidthImg, ThumbHeightImg, null, IntPtr.Zero);

        //        MemoryStream ms = new MemoryStream();
        //        Bitmap newImage = new Bitmap(ThumbWidthImg, ThumbHeightImg);
        //        using (Graphics gr = Graphics.FromImage(newImage))
        //        {
        //            gr.SmoothingMode = SmoothingMode.HighQuality;
        //            gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
        //            gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
        //            gr.DrawImage(bmp, new Rectangle(0, 0, ThumbWidthImg, ThumbHeightImg));
        //        }
        //        newImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

        //        string path = AbsolutePath + mFileName;

        //        if (AbsolutePath != null)
        //        {
        //            byte[] buffer = ms.GetBuffer();
        //            mServicioImagenes.AgregarImagenADirectorio(buffer, AbsolutePath, mFileName, ".jpg");
        //            ms.Dispose();
        //            mServicioImagenes.Dispose();
        //            mServicioImagenes = null;

        //            //Especifico que la captura se ha realizado con exito:
        //            mCapturaRealizada = true;
        //        }

        //        mWebBrowser.Dispose();
        //        mWebBrowser = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        this.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
        //    }
        //}

        ///// <summary>
        ///// Se ejecuta al completarse la captura de una web.
        ///// </summary>
        ///// <param name="sender">El que produce el evento</param>
        ///// <param name="e">Argumentos del evento</param>
        //private void WebBrowser_DocumentCompletedDevuelveImagen(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        //{
        //    try
        //    {
        //        mWebBrowser = (System.Windows.Forms.WebBrowser)sender;
        //        mWebBrowser.ClientSize = new System.Drawing.Size(WidthImg, HeightImg);
        //        mWebBrowser.ScrollBarsEnabled = false;
        //        System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(mWebBrowser.Bounds.Width, mWebBrowser.Bounds.Height);
        //        mWebBrowser.BringToFront();
        //        mWebBrowser.DrawToBitmap(bmp, mWebBrowser.Bounds);


        //        MemoryStream ms = new MemoryStream();
        //        MemoryStream ms_Max = new MemoryStream();
        //        Bitmap newImage = new Bitmap(ThumbWidthImg, ThumbHeightImg);
        //        using (Graphics gr = Graphics.FromImage(newImage))
        //        {
        //            gr.SmoothingMode = SmoothingMode.HighQuality;
        //            gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
        //            gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
        //            gr.DrawImage(bmp, new Rectangle(0, 0, ThumbWidthImg, ThumbHeightImg));
        //        }
        //        newImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        //        bmp.Save(ms_Max, System.Drawing.Imaging.ImageFormat.Jpeg);
                
        //        //Devolvemos la imagen grande para que haga capturas de esta imagen.


        //        byte[] buffer_Max = ms_Max.GetBuffer();
        //        ms_Max.Dispose();
        //        mImagenMax = buffer_Max;
                
        //        byte[] buffer = ms.GetBuffer();
        //        ms.Dispose();
        //        mImagen = buffer;
        //        mWebBrowser.Dispose();
        //        mWebBrowser = null;

        //    }
        //    catch (Exception ex)
        //    {
        //        this.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
        //    }
        //}

        #endregion

        #region Calculo imagenes miniatura

        /// <summary>
        /// Obtiene la imágen en miniatura de un documento de tipo imágen.
        /// </summary>
        /// <param name="pDocumentoID">Identificador del documento</param>
        /// <param name="pPersonaID">Si el documento es de persona: Identificador de la persona a la que pertenece el documento; 
        /// sino Guid.Empty</param>
        /// <param name="pOrganizacionID">Si el documento es de organización: Identificador de la organización a la que pertenece
        /// el documento; sino Guid.Empty</param>
        public bool ObtenerImagenMiniaturaDocumento(Guid pDocumentoID, Guid pPersonaID, Guid pOrganizacionID, string pExtension, LoggingService pLoggingService)
        {
            //Inicializo el servicio de imágenes:
            mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
            mServicioImagenes.Url = mURLServicioImagenes;

            string nombreImagen = pDocumentoID.ToString();

            if (pPersonaID == Guid.Empty && pOrganizacionID == Guid.Empty)
            {
                nombreImagen = Path.Combine(UtilArchivos.DirectorioDocumento(pDocumentoID), nombreImagen);
            }

            //Traigo la imágen del documento:
            byte[] buffer = mServicioImagenes.ObtenerImagenDocumento(nombreImagen, pExtension, pPersonaID, pOrganizacionID);

            bool subida = false;

            if (buffer != null)
            {
                Byte[] bufferFinal;


                MemoryStream streamImagen = new MemoryStream(buffer);
                SixLabors.ImageSharp.Image imagenGrande = SixLabors.ImageSharp.Image.Load(streamImagen);
                //System.Drawing.Image imagenGrande = System.Drawing.Image.FromStream(streamImagen);

                //Extraigo el tamanio actual de la imagen para cacular el que deberá tener la imágen reducida:
                int ancho = imagenGrande.Width;
                int alto = imagenGrande.Height;

                int nuevoAncho = 0;
                int nuevoAlto = 0;

                //Si la anchura de la imagen es superior se redimensiona la imagen
                if (ancho >= MAX_SIZE_IMG)
                {
                    nuevoAncho = MAX_SIZE_IMG;
                    nuevoAlto = (alto * nuevoAncho) / ancho;

                    Image imagenReducida = UtilImages.AjustarImagen(imagenGrande, nuevoAncho, nuevoAlto);
                    using (var ms = new MemoryStream())
                    {
                        imagenReducida.Save(ms, PngFormat.Instance);
                        bufferFinal = ms.ToArray();
                    }
                }
                else
                {
                    ////Si la anchura de la imagen es inferior se superpone sobre fondo transparente
                    //double posicionY = 0;
                    //double posicionX = MAX_SIZE_IMG / 2 - imagenGrande.Width / 2;

                    //Bitmap bmPhoto = new Bitmap(MAX_SIZE_IMG, imagenGrande.Height);
                    //Graphics grPhoto = Graphics.FromImage(bmPhoto);

                    //Rectangle rectOrigen = new Rectangle((int)posicionX, (int)posicionY, (int)imagenGrande.Width, (int)imagenGrande.Height);
                    //Rectangle rectDestino = new Rectangle(0, 0, imagenGrande.Width, imagenGrande.Height);
                    
                    //grPhoto.DrawImage(imagenGrande, rectOrigen, rectDestino, GraphicsUnit.Pixel);

                    //bufferFinal = (byte[])new ImageConverter().ConvertTo(bmPhoto, typeof(byte[]));

                    //bmPhoto.Dispose();
                    //grPhoto.Dispose();

                    imagenGrande.Mutate(x => x.Resize(MAX_SIZE_IMG, imagenGrande.Height));
                    using (var ms = new MemoryStream())
                    {
                        imagenGrande.Save(ms, PngFormat.Instance);
                        bufferFinal = ms.ToArray();
                    }
                }

                if (imagenGrande.Height > 2 * imagenGrande.Width)
                {
                    Image imagenRecortar = Image.Load(new MemoryStream(bufferFinal));
                    bufferFinal = UtilImages.RecortarImagen(imagenRecortar, MAX_SIZE_IMG, MAX_SIZE_IMG * 2, 0, imagenGrande.Height / 2 - MAX_SIZE_IMG);
                }

                subida = mServicioImagenes.AgregarImagenEnMiniaturaADocumento(bufferFinal, pDocumentoID.ToString() + "_peque", pExtension);

                //Disposo todo:
                imagenGrande.Dispose();
                streamImagen.Dispose();
            }
            return subida;
        }

        #endregion

        #region Calculo imágenes directamente

        public bool ObtenerImagenDesdeURLAImagen(Guid pDocumentoID, string pURL, LoggingService pLoggingService)
        {
            //Inicializo el servicio de imágenes:
            mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
            mServicioImagenes.Url = mURLServicioImagenes;

            AbsolutePath = "../" + UtilArchivos.ContentImagenesEnlaces + "/" + UtilArchivos.DirectorioDocumento(pDocumentoID);
            mFileName = pDocumentoID.ToString();
            mCapturaRealizada = false;

            try
            {
                if (!string.IsNullOrEmpty(pURL))
                {
                    WebRequest requestPic = WebRequest.Create(pURL);
                    WebResponse responsePic = requestPic.GetResponse();

                    Image img = Image.Load(responsePic.GetResponseStream());

                    //Extraigo el tamanio actual de la imagen para cacular el que deberá tener la imágen reducida:
                    int ancho = img.Width;
                    int alto = img.Height;

                    int nuevoAncho = 0;
                    int nuevoAlto = 0;

                    //Si tiene una anchura superior a 240 la redimensionamos
                    if (ancho > MAX_SIZE_IMG)
                    {
                        nuevoAncho = MAX_SIZE_IMG;
                        nuevoAlto = (alto * nuevoAncho) / ancho;
                        img.Mutate(x => x.Resize(nuevoAncho, nuevoAlto));
                    }


                    MemoryStream ms = new MemoryStream();
                    img.SaveAsJpeg(ms);

                    byte[] buffer = ms.GetBuffer();
                    
                    mCapturaRealizada = mServicioImagenes.AgregarImagenADirectorio(buffer, AbsolutePath, mFileName, ".jpg");



                    ms.Dispose();
                    mServicioImagenes = null;
                }
                else
                {
                    pLoggingService.GuardarLog("ERROR: imagenUrl null or empty '" + pURL + "'");
                }
            }
            catch (Exception ex)
            {
                pLoggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
            }

            return mCapturaRealizada;
        }

        public bool ObtenerImagenSemanticaDesdeURLAImagen(Guid pDocumentoID, string pURL, List<int> pTamaniosImagenesInt, string pNuevaRutaDestino, string pFileNameCExtension, string pFileName, LoggingService pLoggingService)
        {
            mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
            mServicioImagenes.Url = mURLServicioImagenes;
            mCapturaRealizada = false;

            try
            {
                Image img = null;
                Image img_Max = null;
                if (pURL.EndsWith(".jpg"))
                {
                    #region Imagen
                    WebRequest requestPic = WebRequest.Create(pURL);
                    WebResponse responsePic = requestPic.GetResponse();

                    img = Image.Load(responsePic.GetResponseStream());
                    img_Max = img.Clone(x=>x.Resize(img.Width, img.Height));

                    //mensaje += filaDoc.DocumentoID + "' de tipo Semántico Imagen se ha procesado";
                    #endregion
                }
                else // Hipervínculo
                {
                    #region Hipervínculo

                    //byte[] imagen = this.ObtenerImagenURLDocumentoDevuelveImagen(pURL);

                    //MemoryStream ms = new MemoryStream(imagen);
                    //img = Image.Load(ms);

                    //MemoryStream ms_Max = new MemoryStream(mImagenMax);
                    //img_Max = Image.Load(ms_Max);

                    #endregion
                }

                if (img != null && img_Max != null)
                {
                    // Guardar la imagen sin redimensionar con nombre filaDoc.DocumentoID.ToString() en rutaDestino
                    byte[] imagenArray = null;
                    mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
                    mServicioImagenes.Url = mURLServicioImagenes;
                    MemoryStream ms = new MemoryStream();
                    img.SaveAsJpeg(ms);
                    imagenArray = ms.GetBuffer();
                    ms.Dispose();
                    mServicioImagenes.AgregarImagenADirectorio(imagenArray, pNuevaRutaDestino, pFileName, ".jpg");
                    imagenArray = null;

                    //Extraigo el tamanio actual de la imagen para cacular el que deberá tener la imágen reducida:
                    int ancho = img_Max.Width;
                    int alto = img_Max.Height;
                    int nuevoAncho = 0;
                    int nuevoAlto = 0;

                    //Trabajamos con mImagenMax

                    foreach (int anchoImagenesInt in pTamaniosImagenesInt)
                    {
                        // Guardar imagen redimensionada en rutaDestino
                        mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
                        mServicioImagenes.Url = mURLServicioImagenes;
                        ms = new MemoryStream();

                        mFileName = pFileNameCExtension.Substring(0, pFileNameCExtension.Length - ".jpg".Length) + "_" + anchoImagenesInt.ToString();

                        //Si tiene una anchura superior a 240 la redimensionamos
                        if (ancho > anchoImagenesInt)
                        {
                            nuevoAncho = anchoImagenesInt;
                            nuevoAlto = (alto * nuevoAncho) / ancho;
                            img.Mutate(x => x.Resize(nuevoAncho, nuevoAlto));
                            img.SaveAsJpeg(ms);
                        }
                        else
                        {
                            //Mantengo la imagen original
                            img.SaveAsJpeg(ms);
                        }

                        imagenArray = ms.GetBuffer();
                        ms.Dispose();

                        // 3º Subir las imagenes a la ruta
                        mServicioImagenes.AgregarImagenADirectorio(imagenArray, pNuevaRutaDestino, mFileName, ".jpg");
                    }

                    mCapturaRealizada = true;
                }


                mImagenMax = null;
                mImagen = null;
            }
            catch (Exception ex)
            {
                pLoggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
            }

            return mCapturaRealizada;
        }

        #endregion

        #region Calculo imágenes desde una descripción
        public bool ObtenerImagenDesdeDescripcion(Guid pDocumentoID, string pDescripcion, string pUrlIntraGnoss, LoggingService pLoggingService)
        {
            //Inicializo el servicio de imágenes:
            mServicioImagenes = new ServicioImagenes(pLoggingService, mConfigService);
            mServicioImagenes.Url = mURLServicioImagenes;

            AbsolutePath = "../" + UtilArchivos.ContentImagenesEnlaces + "/" + UtilArchivos.DirectorioDocumento(pDocumentoID);
            mFileName = pDocumentoID.ToString();
            mCapturaRealizada = false;

            SortedDictionary<int, string> listaFotos = new SortedDictionary<int, string>();

            #region Imagenes
            int caracteractualImagenes = 0;
            try
            {
                while (caracteractualImagenes >= 0)
                {
                    if (pDescripcion.Length > caracteractualImagenes && pDescripcion.Substring(caracteractualImagenes).Contains("<img"))
                    {
                        caracteractualImagenes = pDescripcion.IndexOf("<img", caracteractualImagenes);
                        int finImg = pDescripcion.IndexOf(">", caracteractualImagenes) - caracteractualImagenes + 1;
                        string foto = pDescripcion.Substring(caracteractualImagenes, finImg);

                        if (foto.Contains("src=\""))
                        {
                            int inicioSRC = foto.IndexOf("src=\"") + 5;
                            string ruta = foto.Substring(inicioSRC, foto.Substring(inicioSRC).IndexOf("\""));
                            listaFotos.Add(caracteractualImagenes, ruta);

                        }
                        else if (foto.Contains("src='"))
                        {
                            int inicioSRC = foto.IndexOf("src='") + 5;
                            string ruta = foto.Substring(inicioSRC, foto.Substring(inicioSRC).IndexOf("'"));
                            listaFotos.Add(caracteractualImagenes, ruta);
                        }
                        caracteractualImagenes = caracteractualImagenes + 1;
                    }
                    else
                    {
                        caracteractualImagenes = -1;
                    }
                }
            }
            catch (Exception) { }
            #endregion

            #region Videos Iframe
            int caracteractualIframe = 0;

            try
            {
                while (caracteractualIframe >= 0)
                {
                    if (pDescripcion.Length > caracteractualIframe && pDescripcion.Substring(caracteractualIframe).Contains("<iframe "))
                    {
                        caracteractualIframe = pDescripcion.IndexOf("<iframe ", caracteractualIframe);
                        int finImg = pDescripcion.IndexOf(">", caracteractualIframe) - caracteractualIframe + 1;
                        string foto = pDescripcion.Substring(caracteractualIframe, finImg);

                        if (foto.Contains("src=\""))
                        {
                            int inicioSRC = foto.IndexOf("src=\"") + 5;
                            string ruta = foto.Substring(inicioSRC, foto.Substring(inicioSRC).IndexOf("\""));
                            ruta = ruta.Replace("https://", "http://");

                            //Imagen de youtube
                            if (((ruta.StartsWith("http://www.youtube.com") || ruta.StartsWith("http://youtube.com") || ruta.StartsWith("www.youtube.com")) && (ruta.Contains("/watch?") || ruta.Contains("/embed/"))) || ruta.StartsWith("http://youtu.be/"))
                            {
                                listaFotos.Add(caracteractualIframe, ObtenerUrlImagenYoutube(ruta));
                            }
                            else if (ruta.StartsWith("http://player.vimeo.com/video/")) 
                            {
                                string idVideos = ruta.Substring(ruta.IndexOf("/video/") + 7);
                                if (idVideos.Contains("?"))
                                {
                                    idVideos=idVideos.Substring(0, idVideos.IndexOf("?"));
                                }

                                int idVideo;
                                bool exito = int.TryParse(idVideos, out idVideo);

                                if (exito)
                                {
                                    string urlImageVimeo = ObtenerUrlImagenVimeo(ruta, idVideo);
                                    if (!string.IsNullOrEmpty(urlImageVimeo))
                                    {
                                        listaFotos.Add(caracteractualIframe, urlImageVimeo);
                                    }
                                }
                            }
                        }
                        else if (foto.Contains("src='"))
                        {
                            int inicioSRC = foto.IndexOf("src='") + 5;
                            string ruta = foto.Substring(inicioSRC, foto.Substring(inicioSRC).IndexOf("'"));
                            listaFotos.Add(caracteractualIframe, ruta);
                        }
                        caracteractualIframe = caracteractualIframe + 1;
                    }
                    else
                    {
                        caracteractualIframe = -1;
                    }
                }
            }
            catch (Exception) { }
            #endregion

            if(listaFotos.Values.Count > 0)
            {
                TieneImagenEnDescripcion = true;
            }

            //Si existen fotos se procede a la captura
            foreach (string foto in listaFotos.Values)
            {
                string fotoactual = foto;
                try
                {
                    if (!fotoactual.StartsWith("http"))
                    {
                        fotoactual = pUrlIntraGnoss + fotoactual;
                    }

                    WebRequest requestPic = WebRequest.Create(fotoactual);
                    WebResponse responsePic = requestPic.GetResponse();
                    Image img = Image.Load(responsePic.GetResponseStream());

                    //Tiene un area minima suficiente
                    if (img.Height * img.Width > 10000)
                    {
                        //Si la anchura no es superior al doble de la altura
                        if (img.Width < img.Height * 4)
                        {
                            MemoryStream ms = new MemoryStream();
                            //Si tiene una anchura superior a 240 la redimensionamos
                            if (img.Width > MAX_SIZE_IMG)
                            {
                                int nuevoAncho = MAX_SIZE_IMG;
                                int nuevoAlto = (img.Height * nuevoAncho) / img.Width;

                                //Reduzco la imagen:
                                //img = img.GetThumbnailImage(nuevoAncho, nuevoAlto, null, IntPtr.Zero);

                                //Reducción de la imagen sin perder calidad. 
                                //GetThumbnailImage pierde calidad a partir de tamanios como 300x300 (http://stackoverflow.com/questions/7209891/image-getthumbnailimage-method-and-quality)
                                img.Mutate(x => x.Resize(nuevoAncho, nuevoAlto));
                                img.SaveAsJpeg(ms);
                            }
                            else
                            {
                                img.SaveAsJpeg(ms);
                            }



                            byte[] buffer = ms.GetBuffer();

                            mCapturaRealizada = mServicioImagenes.AgregarImagenADirectorio(buffer, AbsolutePath, mFileName, ".jpg");

                            ms.Dispose();
                            mServicioImagenes = null;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    pLoggingService.GuardarLog(ex.Message);
                }
            }

            return mCapturaRealizada;

        }
        #endregion

        // <summary>
        /// Método privado para la obtención de la url de la imagen de los vídeos de VIMEO
        /// </summary>
        /// <param name="pUrlVideo">Url del vídeo</param>
        /// <returns>Url de la imagen</returns>
        private string ObtenerUrlImagenVimeo(string pUrlVideo, int pIdVideo)
        {
            string urlImagen = "";
            //TODO Javi
            //https://developer.vimeo.com/api/reference/videos?version=3.4#get_video_thumbnails
            using (XmlTextReader reader = new XmlTextReader("http://vimeo.com/api/v2/video/" + pIdVideo + ".xml"))
            {
                reader.MoveToContent();
                reader.ReadStartElement();
                while (reader.Read())
                {
                    //ruta de la imagen
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "thumbnail_large")
                    {
                        urlImagen = reader.ReadString();
                    }
                }
            }

            return urlImagen;
        }

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
            }else if (!string.IsNullOrEmpty(System.Web.HttpUtility.ParseQueryString(new Uri(pUrlVideo).Query).Get("v")))
            {
                return "http://i4.ytimg.com/vi/" + System.Web.HttpUtility.ParseQueryString(new Uri(pUrlVideo).Query).Get("v") + "/0.jpg";
            }
            else if (pUrlVideo.Contains("/embed/"))
            {
                string idVideo=pUrlVideo.Substring(pUrlVideo.IndexOf("/embed/")+7);
                if (idVideo.Contains("?"))
                {
                    idVideo=idVideo.Substring(0, idVideo.IndexOf("?"));
                }
                return "http://i4.ytimg.com/vi/" + idVideo + "/0.jpg";
            }else
            {
                return "";
            }
        }

        protected override ControladorServicioGnoss ClonarControlador()
        {
            return new ControladorImagenes(mFicheroConfiguracionBDOriginal, ThumbWidthImg, ThumbHeightImg, ScopedFactory, mConfigService);
        }

        #endregion

        #region Propiedades

        /// <summary>
        /// Indica si el recurso tiene imagen en la descripción
        /// </summary>
        public bool TieneImagenEnDescripcion { get; set; }

        #endregion

        #region Dispose

        /// <summary>
        /// Determina si está disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Destructor
        /// </summary>
        ~ControladorImagenes()
        {
            //Libero los recursos
            Dispose(false);
        }

        /// <summary>
        /// Libera los recursos
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            //impido que se finalice dos veces este objeto
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Libera los recursos
        /// </summary>
        /// <param name="disposing">Determina si se está llamando desde el Dispose()</param>
        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;

                //Libero todos los recursos administrados que he aniadido a esta clase
                mServicioImagenes = null;
            }
        }

        #endregion
    }
}