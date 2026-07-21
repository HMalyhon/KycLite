# One-time Azure setup

Everything here is done **once**; afterwards every push to `main` that passes CI deploys
automatically (the `deploy` job in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)).

The pipeline authenticates with **OIDC federated identity** — GitHub exchanges a short-lived
token with Entra ID, so no publish profile or client secret is ever stored in the repo.

Prerequisites: [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), an Azure
subscription, and permission to create app registrations in the tenant.

## 1. Provision the App Service (Bicep)

App Service names are globally unique — if `kyclite` is taken, pick another name **and update
`AZURE_WEBAPP_NAME` in `.github/workflows/ci.yml` to match**.

```bash
# MFA is mandatory on most tenants; pass --tenant explicitly if a bare `az login`
az login --tenant <TENANT_ID>

az group create --name kyclite-rg --location swedencentral
az deployment group create \
  --resource-group kyclite-rg \
  --template-file infra/main.bicep \
  --parameters webAppName=kyclite location=swedencentral
```

The deployment outputs the site URL (`https://<webAppName>.azurewebsites.net`).


## 2. Create the deployment identity (OIDC federation)

```bash
# An app registration + service principal for the pipeline.
appId=$(az ad app create --display-name kyclite-github-deploy --query appId -o tsv)
az ad sp create --id "$appId"

# Trust GitHub's OIDC tokens from this repo — this is what replaces a stored secret.
#
# The "subject" must match the token GitHub actually mints. Because the deploy job declares
# `environment: production`, GitHub sets the subject to the *environment* form below — not the
# branch form (repo:.../ref:refs/heads/main). Using the branch form here is the most common
# cause of a first-deploy AADSTS70021 "no matching federated identity record" failure.
az ad app federated-credential create --id "$appId" --parameters '{
  "name": "kyclite-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:HMalyhon/KycLite:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Least privilege: the identity can deploy this one web app, nothing else.
subId=$(az account show --query id -o tsv)
az role assignment create \
  --assignee "$appId" \
  --role "Website Contributor" \
  --scope "/subscriptions/$subId/resourceGroups/kyclite-rg/providers/Microsoft.Web/sites/kyclite"
```

> **Create the GitHub environment too.** Repository → Settings → Environments → *New
> environment* → name it `production`. The subject above only matches if that environment
> exists and the job runs in it. (Optionally restrict it to the `main` branch under
> *Deployment branches* for a second layer of protection.)
>
> If you ever remove `environment:` from the deploy job, the subject reverts to the branch form
> `repo:HMalyhon/KycLite:ref:refs/heads/main` and the federated credential must be updated to
> match.

## 3. Add the GitHub secrets

Repository → Settings → Secrets and variables → Actions → *New repository secret* (none of these
are sensitive credentials — they only identify *which* tenant/app to ask for a token):

| Secret                  | Value                                    |
| ----------------------- | ---------------------------------------- |
| `AZURE_CLIENT_ID`       | `$appId` from step 2                     |
| `AZURE_TENANT_ID`       | `az account show --query tenantId -o tsv`|
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv`      |

Push to `main` — the workflow builds both halves, deploys, and smoke-tests `/health` and
`/api/fields`. The app runs in **mock** extractor mode by default.

## 4. (Optional) Real OCR on the deployed app

Point the deployed app at an Azure Document Intelligence resource — configuration only, never
committed to source:

```bash
az webapp config appsettings set \
  --resource-group kyclite-rg --name kyclite --settings \
  DocumentIntelligence__Endpoint='https://<resource>.cognitiveservices.azure.com/' \
  DocumentIntelligence__ApiKey='<key>'
```

The app restarts and logs `Document extractor active: azure`; responses report
`"extractorMode": "azure"`. Remove both settings to fall back to the mock.

## Costs

`F1` is free: no always-on (cold starts after idle), 60 CPU-minutes/day, 1 GB storage — plenty
for a portfolio demo. Bump `-p sku=B1` for a warm, always-on instance (paid).

Expect the **first request after idle to take ~30–40 s** on F1 while the app cold-starts; this is
why the workflow's smoke test retries rather than failing on the first attempt.

## Tearing it down

Everything lives in one resource group plus one app registration:

```bash
az group delete --name kyclite-rg --yes
az ad app delete --id <AZURE_CLIENT_ID>
```
