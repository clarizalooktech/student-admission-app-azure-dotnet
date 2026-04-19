# ── Azure Container Registry ──────────────────────────────────────────────────

# Reference existing ACR on subsequent deploys
data "azurerm_container_registry" "existing" {
  count               = var.create_infrastructure ? 0 : 1
  name                = var.acr_name
  resource_group_name = local.resource_group_name
}

# Create ACR on first deploy
resource "azurerm_container_registry" "main" {
  count               = var.create_infrastructure ? 1 : 0
  name                = var.acr_name
  resource_group_name = local.resource_group_name
  location            = local.resource_group_location
  sku                 = "Basic"
  admin_enabled       = true
  tags                = local.tags
}

# Local — works whether ACR is new or existing
locals {
  acr_login_server   = var.create_infrastructure ? azurerm_container_registry.main[0].login_server   : data.azurerm_container_registry.existing[0].login_server
  acr_admin_username = var.create_infrastructure ? azurerm_container_registry.main[0].admin_username : data.azurerm_container_registry.existing[0].admin_username
  acr_admin_password = var.create_infrastructure ? azurerm_container_registry.main[0].admin_password : data.azurerm_container_registry.existing[0].admin_password
}
