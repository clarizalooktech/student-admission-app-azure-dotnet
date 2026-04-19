# ── Log Analytics workspace ───────────────────────────────────────────────────
resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.app_name}-law-${var.environment}"
  resource_group_name = local.resource_group_name
  location            = local.resource_group_location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# ── Application Insights ──────────────────────────────────────────────────────
resource "azurerm_application_insights" "main" {
  name                = "${var.app_name}-ai-${var.environment}"
  resource_group_name = local.resource_group_name
  location            = local.resource_group_location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}
