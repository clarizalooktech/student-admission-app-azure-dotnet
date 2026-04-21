terraform {
  required_version = ">= 1.7"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }

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

data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

locals {
  resource_group_name     = data.azurerm_resource_group.main.name
  resource_group_location = data.azurerm_resource_group.main.location

  tags = {
    project     = var.app_name
    environment = var.environment
    managed_by  = "terraform"
  }
}