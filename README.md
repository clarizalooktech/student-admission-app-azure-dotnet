# Student Admission — AI Agent Demo

React frontend + .NET 8 AI agent backend + GitHub Actions CI/CD + Terraform on Azure.

---

## Project structure

```
student-admission-app-azure-dotnet/
│
├── .github/
│   └── workflows/
│       ├── deploy.yml        # build → Terraform → push image → deploy to Container Apps
│       └── frontend.yml      # frontend runs locally (Azure Student subscription limitation)
│
├── frontend/
│   ├── index.html            # React app (all inlined, no build step needed)
│   ├── config.json           # backend URL config (overwritten at deploy time)
│   └── src/
│       └── styles.css
│
├── backend/
│   ├── Dockerfile
│   └── src/AdmissionAgent/
│       ├── Controllers/
│       │   └── AdmissionController.cs   # POST /api/admission/evaluate
│       ├── Services/
│       │   └── AgentService.cs          # Planner → Tools → Synthesiser loop
│       ├── Models/
│       │   └── Models.cs
│       ├── Program.cs                   # CORS, App Insights, Prometheus wiring
│       ├── appsettings.json
│       └── AdmissionAgent.csproj
│
├── terraform/
│   ├── main.tf               # provider, Terraform Cloud backend, resource group
│   ├── variables.tf          # all inputs
│   ├── acr.tf                # Azure Container Registry
│   ├── monitoring.tf         # Log Analytics + App Insights
│   ├── container-apps.tf     # Container Apps environment + backend app
│   ├── frontend.tf           # placeholder (SWA not supported on Student subscription)
│   ├── outputs.tf            # ACR creds, backend URL
│   └── terraform.tfvars.example
│
├── .gitattributes
├── .gitignore
└── README.md
```

---

## Architecture

```
Student (browser)
    │  HTTPS
    ▼
frontend/index.html            ← React
    │  REST API call
    ▼
Azure Container Apps           ← .NET 8 Web API (ca-admission-dev)
    │
    ├── AI Agent loop
    │     Planner        → GPT-4o decides which tools to call
    │     Tool Executor  → validate docs, check eligibility, score application
    │     Synthesiser    → GPT-4o writes the human-readable decision
    │
    ├── Azure OpenAI (gpt-4o, australiaeast)
    ├── App Insights     ← traces + exceptions
    └── Prometheus /metrics ← agent step counters, duration histograms
```

**CI/CD flow:**
```
git push → GitHub Actions (deploy.yml)
    ├── Job 1: Build & test     → dotnet build
    ├── Job 2: Terraform        → provisions Azure infra
    │          ↓ passes ACR creds directly to Job 3
    └── Job 3: Push & deploy    → docker build → ACR → Container Apps → health check
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Terraform CLI >= 1.7](https://developer.hashicorp.com/terraform/downloads)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) — `az login`
- A [Terraform Cloud](https://app.terraform.io) account (free)
- An Azure subscription
- An Azure OpenAI resource with `gpt-4o` deployed in `australiaeast`

---

## Step 1 — Create Azure OpenAI resource

```bash
# Create the resource
az cognitiveservices account create \
  --name "openai-student-admission" \
  --resource-group "student-admission-app-rg" \
  --location "australiaeast" \
  --kind "OpenAI" \
  --sku "S0"

# Deploy the model
az cognitiveservices account deployment create \
  --name "openai-student-admission" \
  --resource-group "student-admission-app-rg" \
  --deployment-name "gpt-4o" \
  --model-name "gpt-4o" \
  --model-version "2024-11-20" \
  --model-format "OpenAI" \
  --sku-capacity 10 \
  --sku-name "GlobalStandard"

# Get endpoint and key
az cognitiveservices account show \
  --name "openai-student-admission" \
  --resource-group "student-admission-app-rg" \
  --query "properties.endpoint" -o tsv

az cognitiveservices account keys list \
  --name "openai-student-admission" \
  --resource-group "student-admission-app-rg" \
  --query "key1" -o tsv
```

---

## Step 2 — GitHub secrets

Add these to your repo under **Settings → Secrets and variables → Actions**:

| Secret | Where to get it |
|--------|----------------|
| `TF_API_TOKEN` | Terraform Cloud → User Settings → Tokens |
| `ARM_CLIENT_ID` | `az ad sp create-for-rbac` output |
| `ARM_CLIENT_SECRET` | same command |
| `ARM_TENANT_ID` | same command |
| `ARM_SUBSCRIPTION_ID` | `az account show --query id` |
| `AZURE_CREDENTIALS` | `az ad sp create-for-rbac --json-auth` (full JSON) |
| `TF_VAR_RESOURCE_GROUP_NAME` | `student-admission-app-rg` |
| `TF_VAR_ACR_NAME` | `acrstudentadmission` |
| `OPENAI_ENDPOINT` | Azure OpenAI endpoint URL |
| `OPENAI_API_KEY` | Azure OpenAI Key 1 |

> `ACR_LOGIN_SERVER`, `ACR_USERNAME`, and `ACR_PASSWORD` are NOT needed as secrets.
> The deploy.yml pipeline fetches ACR credentials directly from Azure CLI after Terraform runs.

Create service principal:

```bash
export MSYS_NO_PATHCONV=1
az ad sp create-for-rbac \
  --name "sp-student-admission-github" \
  --role contributor \
  --scopes "/subscriptions/YOUR_SUBSCRIPTION_ID" \
  --json-auth
```

---

## Step 3 — Terraform Cloud setup

1. Create account at [app.terraform.io](https://app.terraform.io)
2. Create organisation e.g. `clarizalooktech`
3. Create workspace: `student-admission-app-azure-dotnet` → API-driven → Auto apply
4. Create Variable Set `azure-credentials` with these environment variables:
   - `ARM_CLIENT_ID`
   - `ARM_CLIENT_SECRET`
   - `ARM_TENANT_ID`
   - `ARM_SUBSCRIPTION_ID`
5. Apply variable set to all workspaces

Update `terraform/main.tf` with your org name:
```hcl
cloud {
  organization = "clarizalooktech"
  workspaces {
    name = "student-admission-app-azure-dotnet"
  }
}
```

---

## Step 4 — Deploy

```bash
git push origin main
```

Watch **GitHub Actions → deploy.yml** — three jobs run in sequence:
```
✅ Build & test
✅ Terraform — provision infra
✅ Push image & deploy
```

---

## Step 5 — Run frontend locally

```bash
# Update config.json with your Container App URL
# frontend/config.json:
# { "apiBase": "https://ca-admission-dev.YOUR-ENV.australiaeast.azurecontainerapps.io" }

cd frontend
npx serve .
# Open http://localhost:3000
```

---

## Step 6 — Update Container App with OpenAI key

After creating the OpenAI resource, update the Container App:

**portal.azure.com → ca-admission-dev → Containers → Environment variables**

Update:
- `AZURE_OPENAI_KEY` → your Key 1
- `AZURE_OPENAI_ENDPOINT` → your endpoint URL

Or via CLI:

```bash
az containerapp update \
  --name ca-admission-dev \
  --resource-group student-admission-app-rg \
  --set-env-vars \
    AZURE_OPENAI_ENDPOINT=https://openai-student-admission.openai.azure.com/ \
    AZURE_OPENAI_MODEL=gpt-4o
```

---

## Observability

| Signal | Tool | How to access |
|--------|------|--------------|
| Request traces + exceptions | App Insights | Azure Portal → App Insights → Transaction search |
| Agent step metrics | Prometheus | `GET /metrics` on the Container App |
| Decision logs | App Insights Logs | KQL query below |

```kql
traces
| where message contains "Evaluation complete"
| project timestamp, message, severityLevel
| order by timestamp desc
```

## Tear down

```bash
terraform -chdir=terraform destroy
```

Removes all Terraform-managed resources. The OpenAI resource (created separately via CLI) needs to be deleted manually:

```bash
az cognitiveservices account delete \
  --name "openai-student-admission" \
  --resource-group "student-admission-app-rg"
```

---

## Notes

- Azure Static Web Apps is not supported on Azure for Students subscription — frontend runs locally
- Container App name `ca-admission-dev` is hardcoded (Azure 32-char limit)
- ACR admin credentials are fetched via Azure CLI in the pipeline — no manual secret setup needed
- Terraform state lives in Terraform Cloud (free tier)