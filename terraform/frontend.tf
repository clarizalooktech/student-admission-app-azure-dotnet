resource "azurerm_static_web_app" "frontend" {
  name                = "swa-admission-dev"
  resource_group_name = local.resource_group_name
  location            = "eastus2"
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = local.tags
}