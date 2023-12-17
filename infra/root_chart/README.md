# root-chart

![Version: 0.1.0](https://img.shields.io/badge/Version-0.1.0-informational?style=flat-square) ![Type: application](https://img.shields.io/badge/Type-application-informational?style=flat-square) ![AppVersion: 1.16.0](https://img.shields.io/badge/AppVersion-1.16.0-informational?style=flat-square)

Root Chart to a single Service

## Requirements

| Repository | Name | Version |
|------------|------|---------|
| file://../api_chart | api(dotnet-chart) | 0.1.0 |
| file://../migration_chart | migration(dotnet-migration) | 0.1.0 |
| oci://ghcr.io/atomicloud/sulfoxide.bromine | bromine(sulfoxide-bromine) | 1.3.0 |
| oci://ghcr.io/dragonflydb/dragonfly/helm | maincache(dragonfly) | v1.13.0 |
| oci://registry-1.docker.io/bitnamicharts | mainstorage(minio) | 12.10.4 |
| oci://registry-1.docker.io/bitnamicharts | maindb(postgresql) | 13.2.24 |

## Values

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| api.affinity | object | `{}` |  |
| api.annotations."argocd.argoproj.io/sync-wave" | string | `"4"` |  |
| api.appSettings.App.Mode | string | `"Server"` |  |
| api.autoscaling | object | `{}` |  |
| api.configMountPath | string | `"/app/Config"` |  |
| api.enabled | bool | `true` |  |
| api.image.pullPolicy | string | `"IfNotPresent"` |  |
| api.image.repository | string | `"nitroso-zinc-api"` |  |
| api.image.tag | string | `""` |  |
| api.imagePullSecrets | list | `[]` |  |
| api.ingress.className | string | `"nginx"` |  |
| api.ingress.enabled | bool | `true` |  |
| api.ingress.hosts[0].host | string | `"api.zinc.nitroso.lapras.lvh.me"` |  |
| api.ingress.hosts[0].paths[0].path | string | `"/"` |  |
| api.ingress.hosts[0].paths[0].pathType | string | `"ImplementationSpecific"` |  |
| api.ingress.tls[0].hosts[0] | string | `"api.zinc.nitroso.lapras.lvh.me"` |  |
| api.ingress.tls[0].issuerRef | string | `"sample"` |  |
| api.ingress.tls[0].secretName | string | `"sample"` |  |
| api.livenessProbe.httpGet.path | string | `"/"` |  |
| api.livenessProbe.httpGet.port | string | `"http"` |  |
| api.nameOverride | string | `"zinc-api"` |  |
| api.nodeSelector | object | `{}` |  |
| api.podAnnotations | object | `{}` |  |
| api.podSecurityContext | object | `{}` |  |
| api.readinessProbe.httpGet.path | string | `"/"` |  |
| api.readinessProbe.httpGet.port | string | `"http"` |  |
| api.replicaCount | int | `1` |  |
| api.resources.limits.cpu | string | `"1"` |  |
| api.resources.limits.memory | string | `"1Gi"` |  |
| api.resources.requests.cpu | string | `"100m"` |  |
| api.resources.requests.memory | string | `"128Mi"` |  |
| api.securityContext | object | `{}` |  |
| api.service.containerPort | int | `9000` |  |
| api.service.port | int | `80` |  |
| api.service.type | string | `"ClusterIP"` |  |
| api.serviceAccount.annotations | object | `{}` |  |
| api.serviceAccount.create | bool | `false` |  |
| api.serviceAccount.name | string | `""` |  |
| api.serviceTree.<<.landscape | string | `"lapras"` |  |
| api.serviceTree.<<.layer | string | `"2"` |  |
| api.serviceTree.<<.platform | string | `"nitroso"` |  |
| api.serviceTree.<<.service | string | `"zinc"` |  |
| api.serviceTree.module | string | `"api"` |  |
| api.tolerations | list | `[]` |  |
| api.topologySpreadConstraints | object | `{}` |  |
| bromine.annotations."argocd.argoproj.io/sync-wave" | string | `"1"` |  |
| bromine.enable | bool | `false` |  |
| bromine.rootSecret | object | `{"ref":"NITROSO_ZINC"}` | Secret of Secrets reference |
| bromine.rootSecret.ref | string | `"NITROSO_ZINC"` | DOPPLER Token Reference |
| bromine.storeName | string | `"nitroso-zinc"` | Store name to create |
| maindb.auth.database | string | `"nitroso-zinc"` |  |
| maindb.auth.password | string | `"supersecret"` |  |
| maindb.auth.username | string | `"admin"` |  |
| maindb.nameOverride | string | `"zinc-maindb"` |  |
| maindb.primary.persistence.enabled | bool | `false` |  |
| mainstorage.apiIngress.enabled | bool | `true` |  |
| mainstorage.apiIngress.hostname | string | `"mainstorage.zinc.nitroso.lapras.lvh.me"` |  |
| mainstorage.apiIngress.ingressClassName | string | `"traefik"` |  |
| mainstorage.auth.rootPassword | string | `"supersecret"` |  |
| mainstorage.auth.rootUser | string | `"admin"` |  |
| mainstorage.ingress.enabled | bool | `true` |  |
| mainstorage.ingress.hostname | string | `"console-mainstorage.zinc.nitroso.lapras.lvh.me"` |  |
| mainstorage.ingress.ingressClassName | string | `"traefik"` |  |
| mainstorage.nameOverride | string | `"zinc-mainstorage"` |  |
| mainstorage.persistence.enabled | bool | `false` |  |
| mainstorage.persistence.size | string | `"10Gi"` |  |
| migration.affinity | object | `{}` |  |
| migration.annotations."argocd.argoproj.io/sync-wave" | string | `"3"` |  |
| migration.appSettings.App.Mode | string | `"Migration"` |  |
| migration.aspNetEnv | string | `"Development"` |  |
| migration.backoffLimit | int | `4` |  |
| migration.configMountPath | string | `"/app/Config"` |  |
| migration.enabled | bool | `false` |  |
| migration.image.pullPolicy | string | `"IfNotPresent"` |  |
| migration.image.repository | string | `"nitroso-zinc-migration"` |  |
| migration.image.tag | string | `""` |  |
| migration.imagePullSecrets | list | `[]` |  |
| migration.nameOverride | string | `"zinc-migration"` |  |
| migration.nodeSelector | object | `{}` |  |
| migration.podAnnotations | object | `{}` |  |
| migration.podSecurityContext | object | `{}` |  |
| migration.resources.limits.cpu | string | `"500m"` |  |
| migration.resources.limits.memory | string | `"1Gi"` |  |
| migration.resources.requests.cpu | string | `"100m"` |  |
| migration.resources.requests.memory | string | `"128Mi"` |  |
| migration.securityContext | object | `{}` |  |
| migration.serviceAccount.annotations | object | `{}` |  |
| migration.serviceAccount.create | bool | `false` |  |
| migration.serviceAccount.name | string | `""` |  |
| migration.serviceTree.<<.landscape | string | `"lapras"` |  |
| migration.serviceTree.<<.layer | string | `"2"` |  |
| migration.serviceTree.<<.platform | string | `"nitroso"` |  |
| migration.serviceTree.<<.service | string | `"zinc"` |  |
| migration.serviceTree.module | string | `"migration"` |  |
| migration.tolerations | list | `[]` |  |
| migration.topologySpreadConstraints | object | `{}` |  |
| serviceTree.landscape | string | `"lapras"` |  |
| serviceTree.layer | string | `"2"` |  |
| serviceTree.platform | string | `"nitroso"` |  |
| serviceTree.service | string | `"zinc"` |  |

----------------------------------------------
Autogenerated from chart metadata using [helm-docs v1.11.2](https://github.com/norwoodj/helm-docs/releases/v1.11.2)
