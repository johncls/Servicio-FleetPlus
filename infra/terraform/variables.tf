variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "create_cloud_resources" {
  type        = bool
  default     = false
  description = "Sigue en false hasta aprobar un despliegue. terraform validate no crea nada."
}

variable "telemetry_api_key" {
  type        = string
  sensitive   = true
  description = "Clave del servicio. No tiene valor por defecto y no se guarda en el repositorio."
}
