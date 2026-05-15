// =============================================================================
// Sistema AAA — Infraestructura de Azure
// =============================================================================
// Recursos:
//   • Azure SQL Server + SQL Database (identidades y auditoría)
//   • App Service Plan (Linux)
//   • Web App (.NET 8)
//
// Despliegue rápido (az cli):
//   az group create -n rg-sistemaAAA-dev -l eastus
//   az deployment group create \
//     -g rg-sistemaAAA-dev \
//     -f infra/main.bicep \
//     -p projectName=sistemaAAA environment=dev \
//        sqlAdminPassword=<secret> jwtSecretKey=<secret>
// =============================================================================

metadata description = 'Infraestructura del Sistema AAA: App Service (.NET 8) + Azure SQL'

targetScope = 'resourceGroup'

// =============================================================================
// PARÁMETROS
// =============================================================================

@description('Nombre corto del proyecto. Prefijo de todos los recursos.')
@minLength(2)
@maxLength(12)
param projectName string

@description('Entorno de despliegue.')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Región de Azure. Por defecto, la del Resource Group.')
param location string = resourceGroup().location

// ── SQL ───────────────────────────────────────────────────────────────────

@description('Login administrador del SQL Server.')
@minLength(1)
@maxLength(128)
param sqlAdminLogin string = 'sqladmin'

@description('''
  Contraseña del administrador SQL.
  @secure() → ARM la ofusca en logs de despliegue e historial de actividad.
  Pasa el valor mediante --parameters o un archivo .bicepparam; nunca la
  escribas directamente en el comando de despliegue en texto plano.
''')
@secure()
@minLength(12)
param sqlAdminPassword string

// ── JWT ──────────────────────────────────────────────────────────────────

@description('''
  Clave secreta para firmar los JWT (HMAC-SHA256). Mínimo 32 caracteres.
  @secure() → ARM la ofusca en logs. Para producción, reemplaza este
  parámetro por una referencia a Azure Key Vault en el appSetting:
    '@Microsoft.KeyVault(VaultName=<kv>;SecretName=JwtSecretKey)'
''')
@secure()
@minLength(32)
param jwtSecretKey string

// ── Seed ─────────────────────────────────────────────────────────────────

@description('Activa el seed de datos al arrancar la aplicación.')
param runSeedOnStartup bool = false

@description('Email del usuario administrador inicial (solo cuando runSeedOnStartup=true).')
param seedAdminEmail string = ''

@description('Contraseña del usuario administrador inicial.')
@secure()
param seedAdminPassword string = ''

// =============================================================================
// VARIABLES
// =============================================================================

// Los nombres de SQL Server y Web App deben ser globalmente únicos en Azure.
// uniqueString() genera un hash determinista de 13 chars a partir del ID
// del Resource Group: mismo grupo → mismo hash (idempotente).
var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)

// toLower() garantiza que los nombres cumplen la restricción de
// solo-minúsculas de SQL Server (y evita inconsistencias en los demás).
var nameSuffix       = toLower('${projectName}-${environment}')
var uniqueNameSuffix = '${nameSuffix}-${uniqueSuffix}'

var appServicePlanName = 'plan-${nameSuffix}'
var webAppName         = 'app-${uniqueNameSuffix}'    // único global
var sqlServerName      = 'sql-${uniqueNameSuffix}'    // único global
var sqlDatabaseName    = 'sqldb-${nameSuffix}'

// SKU del App Service Plan según entorno.
// Free (F1) solo para demos — no soporta custom domains ni "Always On".
var appServicePlanSku = environment == 'prod'
  ? { name: 'B2', tier: 'Basic',    capacity: 1 }
  : { name: 'B1', tier: 'Basic',    capacity: 1 }

// Mapeo al valor de ASPNETCORE_ENVIRONMENT
var aspNetCoreEnv = environment == 'prod'
  ? 'Production'
  : environment == 'staging' ? 'Staging' : 'Development'

// Connection string construida a partir de parámetros @secure().
// ARM trata cualquier expresión que incluya un @secure() como dato sensible:
// no lo registra en los logs de despliegue ni en el historial de actividad.
// NUNCA lo expongas como output de este template.
var sqlConnectionString = join([
  'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433'
  'Initial Catalog=${sqlDatabaseName}'
  'Persist Security Info=False'
  'User ID=${sqlAdminLogin}'
  'Password=${sqlAdminPassword}'
  'MultipleActiveResultSets=False'
  'Encrypt=True'
  'TrustServerCertificate=False'
  'Connection Timeout=30'
], ';')

// =============================================================================
// RECURSOS
// =============================================================================

// ── Azure SQL Server ─────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin:          sqlAdminLogin
    administratorLoginPassword:  sqlAdminPassword
    minimalTlsVersion:           '1.2'
    publicNetworkAccess:         'Enabled'
  }
}

// Permite que los servicios dentro de Azure (incluido el App Service)
// lleguen al SQL Server. La regla 0.0.0.0–0.0.0.0 es la convención
// reservada de Azure para "Allow Azure services and resources".
//
// ⚠ PRODUCCIÓN: restringe este acceso usando VNet Integration +
// Service Endpoints o Private Endpoint para evitar que cualquier
// servicio de otro tenant Azure también pueda intentar conectarse.
resource sqlFirewallAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress:   '0.0.0.0'
  }
}

// ── Azure SQL Database ───────────────────────────────────────────────────────
// SKU Basic: 5 DTU · 2 GB · ~$5/mes — adecuado para dev/staging y
// para auditoría e identidades con carga baja.
//
// Para producción con tráfico variable (Serverless, auto-pause):
//   sku: { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
//   properties: { autoPauseDelay: 60, minCapacity: '0.5', ... }
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name:     'Basic'
    tier:     'Basic'
    capacity: 5
  }
  properties: {
    collation:    'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
  }
}

// ── App Service Plan (Linux) ─────────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name:     appServicePlanName
  location: location
  kind:     'linux'
  sku:      appServicePlanSku
  properties: {
    reserved: true // obligatorio para planes Linux; habilita el stack Linux
  }
}

// ── Web App (.NET 8 en Linux) ────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name:     webAppName
  location: location
  kind:     'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly:    true           // redirige HTTP → HTTPS automáticamente

    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'  // runtime .NET 8 en Linux
      minTlsVersion:  '1.2'
      http20Enabled:  true
      ftpsState:      'Disabled'         // deshabilita FTPS; usa ZIP Deploy o Run From Package

      // La plataforma cifra y almacena los connection strings por separado
      // de los appSettings. La app los consume vía:
      //   IConfiguration.GetConnectionString("DefaultConnection")
      connectionStrings: [
        {
          name:             'DefaultConnection'
          connectionString: sqlConnectionString   // valor @secure() → no aparece en logs
          type:             'SQLAzure'
        }
      ]

      appSettings: [
        // ── Runtime ──────────────────────────────────────────────────
        {
          name:  'ASPNETCORE_ENVIRONMENT'
          value: aspNetCoreEnv
        }
        {
          // Monta el paquete directamente desde el ZIP subido.
          // Mejora el tiempo de arranque en frío y evita bloqueos de archivos.
          name:  'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }

        // ── JWT ───────────────────────────────────────────────────────
        // Los valores Jwt__* se mapean a Jwt:* en IConfiguration gracias
        // al separador doble guion bajo que usa el proveedor de env vars.
        //
        // jwtSecretKey viene de un parámetro @secure() → ARM lo ofusca.
        // Para producción, sustituye el value por:
        //   '@Microsoft.KeyVault(VaultName=<kv-name>;SecretName=JwtSecretKey)'
        // y asigna al Web App una Managed Identity con acceso al Key Vault.
        {
          name:  'Jwt__SecretKey'
          value: jwtSecretKey
        }
        {
          name:  'Jwt__Issuer'
          value: 'SistemaAAA'
        }
        {
          name:  'Jwt__Audience'
          value: 'SistemaAAA-clients'
        }
        {
          name:  'Jwt__ExpiresMinutes'
          value: '60'
        }

        // ── Seed de datos ────────────────────────────────────────────
        // Ejecuta DatabaseSeeder al arrancar solo si RunOnStartup=true.
        // Desactívalo (false) después del primer despliegue.
        {
          name:  'Seed__RunOnStartup'
          value: string(runSeedOnStartup)
        }
        {
          name:  'Seed__AdminEmail'
          value: seedAdminEmail
        }
        {
          // seedAdminPassword es @secure() → no aparece en logs de despliegue
          name:  'Seed__AdminPassword'
          value: seedAdminPassword
        }
      ]
    }
  }
}

// =============================================================================
// OUTPUTS — solo valores no sensibles
// El connection string, contraseñas y claves NUNCA deben ser outputs.
// =============================================================================

@description('URL pública de la Web App.')
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'

@description('Nombre del recurso Web App (requerido por el paso "Azure Web App Deploy" del pipeline CI/CD).')
output webAppName string = webApp.name

@description('Nombre del recurso App Service Plan.')
output appServicePlanName string = appServicePlan.name

@description('FQDN del servidor SQL (para configurar reglas de firewall adicionales o acceso desde herramientas locales).')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('Nombre de la base de datos SQL.')
output sqlDatabaseName string = sqlDatabase.name
