// KYC-Lite hosting: one Linux App Service (free tier) serving the API and the built SPA, plus the
// Document Intelligence resource it reads documents with — wired together **keylessly** via the
// app's managed identity, so no credential exists to store, rotate or leak.
// Deploy with:  az deployment group create -g <rg> -f infra/main.bicep -p webAppName=<name>
// One-time setup walkthrough (including the GitHub OIDC federation): infra/README.md.

@description('Globally unique App Service name; becomes https://<webAppName>.azurewebsites.net.')
param webAppName string = 'kyclite'

@description('Region for the plan and app.')
param location string = resourceGroup().location

@description('App Service plan SKU. F1 is free (cold starts, 60 CPU-min/day) — fine for a demo.')
param sku string = 'F1'

@description('Name of the Document Intelligence account created alongside the app.')
param documentIntelligenceName string = '${webAppName}-di'

@description('Document Intelligence SKU. F0 is free: 500 pages/month, ~20 requests/min (one free account per subscription per region).')
param documentIntelligenceSku string = 'F0'

@description('Turn off account-key auth on the OCR resource. Set false only if you want the key-based .env quick-start to work against this same resource.')
param disableOcrLocalAuth bool = true

// Built-in role "Cognitive Services User" — the least-privilege built-in role that grants the
// data-plane action the app needs (calling analyze). Note it also carries
// accounts/listkeys/action, which is why disableLocalAuth above matters: with local auth off, a
// leaked key is not a usable credential, so that extra permission grants nothing in practice.
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

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

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: documentIntelligenceName
  location: location
  kind: 'FormRecognizer' // the resource kind behind Azure AI Document Intelligence
  sku: {
    name: documentIntelligenceSku
  }
  properties: {
    // A custom subdomain is what makes Entra ID token auth possible at all: the shared regional
    // endpoint accepts account keys only. This is the pre-requisite for the keyless setup below.
    // Note it is globally unique and immutable once set — a name collision here fails the deploy.
    customSubDomainName: documentIntelligenceName
    // Nothing in this system uses an account key, so leaving them live would only be a credential
    // waiting to leak.
    disableLocalAuth: disableOcrLocalAuth
  }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  // Linux code app (not a container). The plan's reserved:true + linuxFxVersion drive the actual
  // behaviour; this kind keeps the portal and tooling from classifying it as a Windows app.
  kind: 'app,linux'
  // The identity the app authenticates to Document Intelligence with — Azure issues and rotates
  // its credentials; none of them are ever visible to us, the repo, or the pipeline.
  identity: {
    type: 'SystemAssigned'
  }
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
        {
          // The endpoint is the *only* extraction config the deployed app needs. There is no
          // DocumentIntelligence__ApiKey counterpart: with the key absent, the extractor
          // authenticates as the managed identity above (see AzureDocumentExtractor).
          name: 'DocumentIntelligence__Endpoint'
          value: documentIntelligence.properties.endpoint
        }
      ]
    }
  }
}

// The one grant that makes the keyless path work. Assigning a role needs Owner or User Access
// Administrator on the resource group — an `az deployment` run by a plain Contributor fails here.
resource documentIntelligenceAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: documentIntelligence
  // Role assignment names must be deterministic GUIDs, so re-deploying this template is
  // idempotent. The principalId would be the more precise seed, but ARM requires a name it can
  // compute before deployment starts and that value only exists afterwards. Consequence to know:
  // web app ids are name-based, so *deleting and recreating* the app reuses this name with a new
  // principal, which Azure refuses (RoleAssignmentUpdateNotPermitted). Delete the assignment by
  // hand in that case:
  //   az role assignment delete --ids <assignment id>
  name: guid(documentIntelligence.id, app.id, cognitiveServicesUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppUrl string = 'https://${app.properties.defaultHostName}'
output webAppName string = app.name
output documentIntelligenceEndpoint string = documentIntelligence.properties.endpoint
