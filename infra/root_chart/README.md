# root-chart

![Version: 0.1.0](https://img.shields.io/badge/Version-0.1.0-informational?style=flat-square) ![Type: application](https://img.shields.io/badge/Type-application-informational?style=flat-square) ![AppVersion: 1.16.0](https://img.shields.io/badge/AppVersion-1.16.0-informational?style=flat-square)

Root Chart to a single Service

## Requirements

| Repository | Name | Version |
|------------|------|---------|
| file://../api_chart | api(dotnet-chart) | 0.1.0 |
| file://../migration_chart | migration(dotnet-migration) | 0.1.0 |
| oci://ghcr.io/atomicloud/sulfoxide.bromine | bromine(sulfoxide-bromine) | 1.8.0 |
| oci://ghcr.io/dragonflydb/dragonfly/helm | maincache(dragonfly) | v1.20.1 |
| oci://registry-1.docker.io/bitnamicharts | mainstorage(minio) | 14.6.20 |
| oci://registry-1.docker.io/bitnamicharts | maindb(postgresql) | 15.5.16 |
| oci://registry-1.docker.io/bitnamicharts | streamcache(redis) | 19.6.1 |

## Values

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| api.affinity | object | `{}` |  |
| api.annotations."argocd.argoproj.io/hook" | string | `"Sync"` |  |
| api.annotations."argocd.argoproj.io/sync-wave" | string | `"4"` |  |
| api.annotations.drop_log | string | `"true"` |  |
| api.appSettings.App.Mode | string | `"Server"` |  |
| api.autoscaling | object | `{}` |  |
| api.configMountPath | string | `"/app/Config"` |  |
| api.enabled | bool | `true` |  |
| api.envFromSecret | string | `"zinc"` |  |
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
| api.livenessProbe.periodSeconds | int | `30` |  |
| api.nameOverride | string | `"zinc-api"` |  |
| api.nodeSelector | object | `{}` |  |
| api.podAnnotations | object | `{}` |  |
| api.podSecurityContext | object | `{}` |  |
| api.readinessProbe.httpGet.path | string | `"/"` |  |
| api.readinessProbe.httpGet.port | string | `"http"` |  |
| api.readinessProbe.periodSeconds | int | `30` |  |
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
| bromine.rootSecret | object | `{"name":"zinc","ref":{"clientId":"NITROSO_ZINC_CLIENT_ID","clientSecret":"NITROSO_ZINC_CLIENT_SECRET"}}` | Secret of Secrets reference |
| bromine.rootSecret.ref | object | `{"clientId":"NITROSO_ZINC_CLIENT_ID","clientSecret":"NITROSO_ZINC_CLIENT_SECRET"}` | Infisical Token Reference |
| bromine.rootSecret.ref.clientId | string | `"NITROSO_ZINC_CLIENT_ID"` | Client ID |
| bromine.rootSecret.ref.clientSecret | string | `"NITROSO_ZINC_CLIENT_SECRET"` | Client Secret |
| bromine.serviceTree.<<.landscape | string | `"lapras"` |  |
| bromine.serviceTree.<<.layer | string | `"2"` |  |
| bromine.serviceTree.<<.platform | string | `"nitroso"` |  |
| bromine.serviceTree.<<.service | string | `"zinc"` |  |
| bromine.storeName | string | `"zinc"` | Store name to create |
| bromine.target | string | `"zinc"` |  |
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
| migration.annotations."argocd.argoproj.io/hook" | string | `"Sync"` |  |
| migration.annotations."argocd.argoproj.io/sync-wave" | string | `"3"` |  |
| migration.annotations.drop_log | string | `"true"` |  |
| migration.appSettings.App.Mode | string | `"Migration"` |  |
| migration.aspNetEnv | string | `"Development"` |  |
| migration.backoffLimit | int | `4` |  |
| migration.configMountPath | string | `"/app/Config"` |  |
| migration.enabled | bool | `false` |  |
| migration.envFromSecret | string | `"zinc"` |  |
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
| streamcache.architecture | string | `"standalone"` |  |
| streamcache.auth.enabled | bool | `true` |  |
| streamcache.auth.existingSecret | string | `"zinc"` |  |
| streamcache.auth.existingSecretPasswordKey | string | `"ATOMI_CACHE__STREAM__PASSWORD"` |  |
| streamcache.commonAnnotations."argocd.argoproj.io/sync-wave" | string | `"2"` |  |
| streamcache.commonAnnotations."atomi.cloud/module" | string | `"streamcache"` |  |
| streamcache.commonAnnotations.<<."atomi.cloud/layer" | string | `"2"` |  |
| streamcache.commonAnnotations.<<."atomi.cloud/platform" | string | `"nitroso"` |  |
| streamcache.commonAnnotations.<<."atomi.cloud/service" | string | `"zinc"` |  |
| streamcache.commonLabels."atomi.cloud/module" | string | `"streamcache"` |  |
| streamcache.commonLabels.<<."atomi.cloud/layer" | string | `"2"` |  |
| streamcache.commonLabels.<<."atomi.cloud/platform" | string | `"nitroso"` |  |
| streamcache.commonLabels.<<."atomi.cloud/service" | string | `"zinc"` |  |
| streamcache.master.persistence.enabled | bool | `false` |  |
| streamcache.nameOverride | string | `"zinc-streamcache"` |  |
| streamcache.podAnnotations."argocd.argoproj.io/sync-wave" | string | `"2"` |  |
| streamcache.podAnnotations."atomi.cloud/module" | string | `"streamcache"` |  |
| streamcache.podAnnotations.<<."atomi.cloud/layer" | string | `"2"` |  |
| streamcache.podAnnotations.<<."atomi.cloud/platform" | string | `"nitroso"` |  |
| streamcache.podAnnotations.<<."atomi.cloud/service" | string | `"zinc"` |  |
| streamcache.replica.persistence.enabled | bool | `false` |  |
| streamcache.resources.limits.cpu | string | `"250m"` |  |
| streamcache.resources.limits.memory | string | `"512Mi"` |  |
| streamcache.resources.requests.cpu | string | `"100m"` |  |
| streamcache.resources.requests.memory | string | `"256Mi"` |  |
| tags."atomi.cloud/layer" | string | `"2"` |  |
| tags."atomi.cloud/platform" | string | `"nitroso"` |  |
| tags."atomi.cloud/service" | string | `"zinc"` |  |

----------------------------------------------
Autogenerated from chart metadata using [helm-docs v1.14.2](https://github.com/norwoodj/helm-docs/releases/v1.14.2)
