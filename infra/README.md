# One-time Azure setup

Everything here is done **once**; afterwards every push to `main` that passes CI deploys
automatically (the `deploy` job in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)).

The pipeline authenticates with **OIDC federated identity** — GitHub exchanges a short-lived
token with Entra ID, so no publish profile or client secret is ever stored in the repo.

The deployed app reaches Azure AI Document Intelligence the same way: as its **managed identity**,
granted `Cognitive Services User` on that one resource. There is no OCR key in the repo, in GitHub
secrets, in the build artifact, or in the app's configuration — the account is provisioned with
keys disabled outright.

Prerequisites: [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), an Azure
subscription, permission to create app registrations in the tenant, and **Owner or User Access
Administrator on the resource group** (step 1 creates a role assignment; a plain Contributor
can't).

## 1. Provision the App Service + OCR resource (Bicep)

One template creates everything and wires it together: the plan, the web app (with a
system-assigned identity), the Document Intelligence account (`F0`, keys disabled, custom
subdomain so Entra ID auth works), the role assignment, and the `DocumentIntelligence__Endpoint`
app setting.

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

The deployment outputs the site URL (`https://<webAppName>.azurewebsites.net`) and the Document
Intelligence endpoint. `F0` is the free OCR tier — 500 pages/month and ~20 requests/minute, which
is what the API's rate limit is aligned to. Azure allows **one free account per subscription per
region**; if you already have one, pass `-p documentIntelligenceSku=S0` (paid) or point the
template at a different region.


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

Push to `main` — the workflow builds both halves, deploys, and smoke-tests `/health`,
`/api/fields` and `/api/status`.

## 4. Confirm the app is running real OCR

No OCR credential to configure: step 1 already wired the identity, the role and the endpoint. The
app logs `Document extractor active: azure (Entra ID / managed identity)`, `/api/status` reports
`{"extractorMode":"azure"}`, and the page shows a green **Live OCR** badge on load.

```bash
curl -s https://kyclite.azurewebsites.net/api/status
```

Optionally set the repository **variable** `EXPECTED_EXTRACTOR_MODE` to `azure` (Settings →
Secrets and variables → Actions → *Variables*). The smoke test then fails any deploy whose
`/api/status` disagrees. Note the mode only reflects *configuration*; the smoke test's real
verification probe is what proves the identity can actually authenticate.

If the badge says *Mock extractor*, the endpoint app setting is missing — re-run step 1. If
uploads instead fail with **503** (`"The document provider is not accepting this application's
credentials."`), the role assignment is the culprit: confirm it exists and give it a minute to
propagate. The underlying Azure status is in the log stream (`az webapp log tail -g kyclite-rg -n
kyclite`), not in the response.

```bash
az webapp identity show -g kyclite-rg -n kyclite --query principalId -o tsv
az role assignment list --assignee <principalId> --all -o table   # expect: Cognitive Services User
```

To run the deployed demo on the offline mock instead, remove the endpoint setting — the app falls
back automatically:

```bash
az webapp config appsettings delete \
  --resource-group kyclite-rg --name kyclite \
  --setting-names DocumentIntelligence__Endpoint
```

This lasts until the next template deployment: `siteConfig.appSettings` is declared inline, so
re-running step 1 replaces the whole collection and puts the endpoint back. For a durable switch,
remove the setting from `main.bicep` as well.

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
