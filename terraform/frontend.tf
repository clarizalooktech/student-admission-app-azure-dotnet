# ── Azure Blob Storage — static website hosting for React frontend ─────────────
resource "azurerm_storage_account" "frontend" {
  name                     = "stadmissionfrontend"
  resource_group_name      = local.resource_group_name
  location                 = local.resource_group_location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  tags                     = local.tags

  static_website {
    index_document     = "index.html"
    error_404_document = "index.html"
  }
}