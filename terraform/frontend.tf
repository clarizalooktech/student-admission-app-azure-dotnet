# ── Azure Static Web App — React frontend ────────────────────────────────────
resource "azurerm_static_web_app" "frontend" {
  name                = "${var.app_name}-swa-${var.environment}"
  resource_group_name = local.resource_group_name
  location            = "centralus"
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = local.tags
}
