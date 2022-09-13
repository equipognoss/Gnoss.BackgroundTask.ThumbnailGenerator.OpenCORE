![](https://content.gnoss.ws/imagenes/proyectos/personalizacion/7e72bf14-28b9-4beb-82f8-e32a3b49d9d3/cms/logognossazulprincipal.png)

# Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE

![](https://github.com/equipognoss/Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE/workflows/BuildThumbnail/badge.svg)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=bugs)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.ThumbnailGenerator.OpenCORE)

Aplicación de segundo plano que se encarga de generar las miniaturas de las imágenes que suben los usuarios a los recursos.

Este servicio está escuchando la cola de nombre "ColaMiniatura". Se envía un mensaje a esta cola cada vez que se crea o edita un recurso desde la Web o el API, para que este servicio se encargue de comprobar si contiene alguna imagen y en caso afirmativo, crear una miniatura de la misma. 

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
thumbnail:
    image: gnoss/gnoss.backgroundtask.thumbnailgenerator.opencore
    env_file: .env
    environment:
     virtuosoConnectionString: ${virtuosoConnectionString}
     acid: ${acid}
     base: ${base}
     RabbitMQ__colaServiciosWin: ${RabbitMQ}
     RabbitMQ__colaReplicacion: ${RabbitMQ}
     redis__redis__ip__master: ${redis__redis__ip__master}
     redis__redis__bd: ${redis__redis__bd}
     redis__redis__timeout: ${redis__redis__timeout}
     redis__recursos__ip__master: ${redis__recursos__ip__master}
     redis__recursos__bd: ${redis__recursos_bd}
     redis__recursos__timeout: ${redis__recursos_timeout}
     redis__liveUsuarios__ip__master: ${redis__liveUsuarios__ip__master}
     redis__liveUsuarios__bd: ${redis__liveUsuarios_bd}
     redis__liveUsuarios__timeout: ${redis__liveUsuarios_timeout}
     idiomas: "es|Español,en|English"
     Servicios__urlBase: "https://servicios.test.com"
     connectionType: "0"
     intervalo: "100"
     Servicios__urlInterno: "https://localhost:4959"
     scopeIdentity: ${scopeIdentity}
     clientIDIdentity: ${clientIDIdentity}
     clientSecretIdentity: ${clientIDIdentity}
    volumes:
     - ./logs/thumbnail:/app/logs
```


Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE

## Código de conducta
Este proyecto a adoptado el código de conducta definido por "Contributor Covenant" para definir el comportamiento esperado en las contribuciones a este proyecto. Para más información ver https://www.contributor-covenant.org/

## Licencia
Este producto es parte de la plataforma [Gnoss Semantic AI Platform Open Core](https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE), es un producto open source y está licenciado bajo GPLv3.
