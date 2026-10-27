output "cluster_name" {
  value = try(aws_ecs_cluster.fleetpulse[0].name, null)
}

output "log_group_name" {
  value = try(aws_cloudwatch_log_group.api[0].name, null)
}
