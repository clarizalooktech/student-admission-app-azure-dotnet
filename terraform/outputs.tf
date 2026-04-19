output "acr_login_server" {
  description = "Container registry URL — used by GitHub Actions to push images"
  value       = local.acr_login_server
}

output "acr_name" {
  description = "ACR resource name"
  value       = var.acr_name
}

output "acr_admin_username" {
  description = "ACR admin username"
  value       = local.acr_admin_username
  sensitive   = true
}

output "acr_admin_password" {
  description = "ACR admin password"
  value       = local.acr_admin_password
  sensitive   = true
}

output "backend_url" {
  description = "Public URL of the Container App backend"
  value       = "https://${azurerm_container_app.backend.ingress[0].fqdn}"
}

output "frontend_url" {
  description = "Public URL of the Static Web App"
  value       = "https://${azurerm_static_web_app.frontend.default_host_name}"
}

output "frontend_deployment_token" {
  description = "SWA deployment token — add as AZURE_STATIC_WEB_APPS_API_TOKEN in GitHub secrets"
  value       = azurerm_static_web_app.frontend.api_key
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "App Insights connection string"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "resource_group_name" {
  description = "Resource group name"
  value       = local.resource_group_name
}
