resource "azurerm_container_app_environment" "main" {
  name                       = "cae-admission-dev"
  resource_group_name        = local.resource_group_name
  location                   = local.resource_group_location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_container_app" "backend" {
  name                         = "ca-admission-dev"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = local.resource_group_name
  revision_mode                = "Single"
  tags                         = local.tags

  registry {
    server               = local.acr_login_server
    username             = local.acr_admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = local.acr_admin_password
  }
  secret {
    name  = "openai-key"
    value = var.openai_api_key
  }
  secret {
    name  = "appinsights-cs"
    value = azurerm_application_insights.main.connection_string
  }

  template {
    min_replicas = 0
    max_replicas = 3

    container {
      name   = "admission-backend"
      image  = "${local.acr_login_server}/admission-agent:latest"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "AZURE_OPENAI_ENDPOINT"
        value = var.openai_endpoint
      }
      env {
        name        = "AZURE_OPENAI_KEY"
        secret_name = "openai-key"
      }
      env {
        name  = "AZURE_OPENAI_MODEL"
        value = var.openai_model
      }
      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "appinsights-cs"
      }

      liveness_probe {
        transport = "HTTP"
        path      = "/api/admission/health"
        port      = 5000
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 5000
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}