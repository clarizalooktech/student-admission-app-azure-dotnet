resource "azurerm_container_registry" "main" {
  name                = var.acr_name
  resource_group_name = local.resource_group_name
  location            = local.resource_group_location
  sku                 = "Basic"
  admin_enabled       = true
  tags                = local.tags
}

locals {
  acr_login_server   = azurerm_container_registry.main.login_server
  acr_admin_username = azurerm_container_registry.main.admin_username
  acr_admin_password = azurerm_container_registry.main.admin_password
}