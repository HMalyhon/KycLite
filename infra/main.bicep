// KYC-Lite hosting: one Linux App Service (free tier) serving the API and the built SPA.
// Deploy with:  az deployment group create -g <rg> -f infra/main.bicep -p webAppName=<name>
// One-time setup walkthrough (including the GitHub OIDC federation): infra/README.md.

@description('Globally unique App Service name; becomes https://<webAppName>.azurewebsites.net.')
param webAppName string = 'kyclite'

@description('Region for the plan and app.')
param location string = resourceGroup().location

@description('App Service plan SKU. F1 is free (cold starts, 60 CPU-min/day) — fine for a demo.')
param sku string = 'F1'

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${webAppName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true // required for Linux plans
  }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  // Linux code app (not a container). The plan's reserved:true + linuxFxVersion drive the actual
  // behaviour; this kind keeps the portal and tooling from classifying it as a Windows app.
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      http20Enabled: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          // App Service fronts the app with a proxy, so the client address arrives in
          // X-Forwarded-For. This built-in switch enables ASP.NET Core's forwarded-headers
          // middleware, keeping the per-client-IP rate limiter partitioned on real client IPs
          // instead of lumping everyone into the proxy's address.
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        // To run real OCR instead of the mock, add DocumentIntelligence__Endpoint and
        // DocumentIntelligence__ApiKey as app settings (see infra/README.md) — never in source.
      ]
    }
  }
}

output webAppUrl string = 'https://${app.properties.defaultHostName}'
output webAppName string = app.name
