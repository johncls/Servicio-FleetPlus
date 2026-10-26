locals {
  name = "fleetpulse-${var.environment}"
}

resource "aws_ecs_cluster" "fleetpulse" {
  count = var.create_cloud_resources ? 1 : 0
  name  = local.name
}

resource "aws_cloudwatch_log_group" "api" {
  count             = var.create_cloud_resources ? 1 : 0
  name              = "/fleetpulse/${var.environment}/api"
  retention_in_days = 14
}

resource "aws_ssm_parameter" "telemetry_api_key" {
  count = var.create_cloud_resources ? 1 : 0
  name  = "/fleetpulse/${var.environment}/telemetry-api-key"
  type  = "SecureString"
  value = var.telemetry_api_key
}
