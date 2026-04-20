terraform {
  required_version = ">= 1.7"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }

  # Terraform Cloud — free tier remote state
  cloud {
    organization = "clarizalooktech"
    workspaces {
      name = "student-admission-app-azure-dotnet"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

# ── Resource group ────────────────────────────────────────────────────────────
# Data source — reference existing RG if it already exists
data "azurerm_resource_group" "existing" {
  count = var.create_infrastructure ? 0 : 1
  name  = var.resource_group_name
}

# Create new RG only on first deploy
resource "azurerm_resource_group" "main" {
  count    = var.create_infrastructure ? 1 : 0
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# Local to reference whichever RG exists
locals {
  resource_group_name = var.create_infrastructure \
    ? azurerm_resource_group.main[0].name \
    : data.azurerm_resource_group.existing[0].name

  resource_group_location = var.create_infrastructure \
    ? azurerm_resource_group.main[0].location \
    : data.azurerm_resource_group.existing[0].location

  tags = {
    project     = var.app_name
    environment = var.environment
    managed_by  = "terraform"
  }
}
