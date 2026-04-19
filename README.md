# Student Admission — AI Agent Demo

A talk demo for **"DevOps thinking for production AI agents"**.

React frontend + .NET 8 AI agent backend + GitHub Actions CI/CD + Terraform on Azure.

---

## Project structure

```
student-admission-app-azure-dotnet/
│
├── .github/
│   └── workflows/
│       ├── terraform.yml     # infra plan → apply (with create_infrastructure check)
│       ├── backend.yml       # build → push to ACR → deploy to Container Apps
│       └── frontend.yml      # inject API URL → deploy to Static Web Apps
│
├── frontend/
│   ├── index.html            # React entry (CDN, no build step needed)
│   └── src/
│       ├── App.jsx           # 4-step wizard + live AI agent result panel
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
│   ├── main.tf                    # provider, Terraform Cloud backend, resource group
│   ├── variables.tf               # all inputs incl. create_infrastructure flag
│   ├── acr.tf                     # Azure Container Registry (conditional create)
│   ├── monitoring.tf              # Log Analytics + App Insights
│   ├── container-apps.tf          # Container Apps environment + backend app
│   ├── frontend.tf                # Azure Static Web App
│   ├── outputs.tf                 # URLs + tokens needed by GitHub Actions
│   └── terraform.tfvars.example   # safe to commit — fill in and rename to .tfvars
│
├── .gitignore
└── README.md
```

---

## Architecture

```
Student (browser)
    │  HTTPS
    ▼
Azure Static Web Apps          ← React wizard (4 steps)
    │  REST API call
    ▼
Azure Container Apps           ← .NET 8 Web API (containerised)
    │
    ├── AI Agent loop
    │     Planner        → GPT-4o mini decides which tools to call
    │     Tool Executor  → validate docs, check eligibility, score application
    │     Synthesiser    → GPT-4o mini writes the human-readable decision
    │
    ├── Azure OpenAI (gpt-4o-mini)
    ├── App Insights     ← traces + exceptions
    └── Prometheus /metrics ← agent step counters, duration histograms
```

**CI/CD flow:**
```
git push → GitHub Actions
    ├── terraform.yml  → Terraform Cloud → provisions Azure infra
    ├── backend.yml    → ACR build/push → Container Apps deploy → health check
    └── frontend.yml   → inject backend URL → Azure Static Web Apps deploy
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Terraform CLI >= 1.7](https://developer.hashicorp.com/terraform/downloads)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) — `az login`
- A [Terraform Cloud](https://app.terraform.io) account (free)
- An Azure subscription
- An Azure OpenAI resource with `gpt-4o-mini` deployed

---

## Step 1 — Provision infrastructure with Terraform

```bash
cd terraform

# Copy and fill in your real values
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set subscription_id, openai_endpoint, openai_api_key etc.

# Connect to Terraform Cloud
terraform login

# Initialise (downloads providers, connects to TF Cloud workspace)
terraform init

# Preview what will be created
terraform plan

# Create everything in Azure
terraform apply
```

**What Terraform creates:**
- Resource group
- Azure Container Registry (ACR)
- Log Analytics workspace
- Application Insights
- Container Apps environment
- Container App (backend, scales to zero)
- Azure Static Web App (frontend, free tier)

After apply, grab the outputs you'll need for GitHub secrets:

```bash
terraform output -raw acr_login_server
terraform output -raw acr_admin_username
terraform output -raw acr_admin_password
terraform output -raw backend_url
terraform output -raw frontend_deployment_token
terraform output -raw app_insights_connection_string
```

> On subsequent runs, Terraform checks if the resource group already exists and sets
> `create_infrastructure=false` automatically — so it won't try to recreate existing resources.

---

## Step 2 — Run locally

### Backend

```bash
cd backend/src/AdmissionAgent

# Store secrets safely (never commit these)
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AZURE_OPENAI_KEY"      "YOUR-KEY"
dotnet user-secrets set "AZURE_OPENAI_MODEL"    "gpt-4o-mini"

dotnet run
# → API:      http://localhost:5000/api/admission/evaluate
# → Health:   http://localhost:5000/api/admission/health
# → Metrics:  http://localhost:5000/metrics  (Prometheus)
```

### Frontend

```bash
cd frontend
# No build step needed — just open in a browser
open index.html

# Or serve with any static server
npx serve .
```

> The frontend points to `http://localhost:5000` when running locally.
> Update `API_BASE` in `src/App.jsx` before deploying to point to your Container App URL.

---

## Step 3 — Set up GitHub Actions

Add these secrets to your repo under **Settings → Secrets and variables → Actions**:

| Secret | Where to get it |
|--------|----------------|
| `TF_API_TOKEN` | Terraform Cloud → User Settings → Tokens |
| `ARM_CLIENT_ID` | `az ad sp create-for-rbac` output |
| `ARM_CLIENT_SECRET` | same command |
| `ARM_TENANT_ID` | same command |
| `ARM_SUBSCRIPTION_ID` | `az account show --query id` |
| `AZURE_CREDENTIALS` | `az ad sp create-for-rbac --sdk-auth` (full JSON) |
| `TF_VAR_RESOURCE_GROUP_NAME` | e.g. `student-admission-app-azure-dotnet-rg` |
| `TF_VAR_ACR_NAME` | e.g. `acradmissiondemo` |
| `OPENAI_ENDPOINT` | Your Azure OpenAI endpoint URL |
| `OPENAI_API_KEY` | Your Azure OpenAI key |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Terraform output `frontend_deployment_token` — add after first apply |
| `BACKEND_URL` | Terraform output `backend_url` — add after first apply |

> `ACR_LOGIN_SERVER`, `ACR_USERNAME`, and `ACR_PASSWORD` are **not needed** as secrets.
> `deploy.yml` runs Terraform first then passes ACR credentials directly to the
> Docker build job via job outputs — same pattern as the glucose monitor app.

Create an Azure service principal for GitHub Actions:

```bash
az ad sp create-for-rbac \
  --name "sp-student-admission-app-azure-dotnet-github" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```

Once secrets are added, push to `main` — the pipelines run automatically:

```bash
git add .
git commit -m "initial commit"
git push origin main
```

**Pipeline order on first push:**
1. `terraform.yml` — provisions all Azure resources
2. `backend.yml` — builds Docker image, pushes to ACR, deploys to Container Apps
3. `frontend.yml` — deploys React to Static Web Apps

---

## Step 4 — Observability

| Signal | Tool | How to access |
|--------|------|--------------|
| Request traces + exceptions | App Insights | Azure Portal → App Insights → Transaction search |
| Agent step metrics | Prometheus | `GET /metrics` on the Container App |
| Dashboards | Grafana | Connect to Prometheus endpoint |
| Decision logs | App Insights Logs | KQL query below |

Useful KQL in App Insights → Logs:

```kql
traces
| where message contains "Evaluation complete"
| project timestamp, message, severityLevel
| order by timestamp desc
```

```kql
traces
| where message contains "Starting evaluation"
| project timestamp, message
| order by timestamp desc
```

---

## Cost estimate

All resources are optimised for demo/dev cost:

| Resource | Cost |
|----------|------|
| Container Apps (scales to zero) | ~$0 when idle |
| Static Web Apps (free tier) | $0 |
| ACR (Basic) | ~$5/month |
| Log Analytics | ~$2/month (30-day retention) |
| App Insights | Free up to 5GB/month |
| gpt-4o-mini | $0.15/1M input tokens — 100 demo calls ≈ $0.01 |

---

## Key files explained

| File | Purpose |
|------|---------|
| `frontend/src/App.jsx` | Full React wizard — 4 steps + live agent status panel |
| `backend/src/AdmissionAgent/Services/AgentService.cs` | The AI agent loop: Planner, Tool Executor, Synthesiser |
| `backend/src/AdmissionAgent/Controllers/AdmissionController.cs` | Single POST endpoint that triggers the agent |
| `backend/src/AdmissionAgent/Program.cs` | App Insights + Prometheus wiring |
| `terraform/main.tf` | Provider config + Terraform Cloud backend |
| `terraform/acr.tf` | ACR with conditional create (glucose monitor pattern) |
| `terraform/variables.tf` | All inputs including `create_infrastructure` flag |
| `terraform/terraform.tfvars.example` | Template — copy to `.tfvars`, never commit real values |
| `.github/workflows/terraform.yml` | Checks if RG exists → plan → manual approve → apply |
| `.github/workflows/backend.yml` | Build → push image (SHA tag) → deploy → health check |
| `.github/workflows/frontend.yml` | Inject backend URL → deploy to Static Web Apps |

---

## Secrets best practice (same as glucose monitor)

- `terraform.tfvars` is in `.gitignore` — never committed
- `terraform.tfvars.example` is committed — safe to show on screen
- OpenAI keys are passed as Container App secrets, not plain env vars
- GitHub Actions uses a service principal with contributor scope only
