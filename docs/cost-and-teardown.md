# Cost and teardown

## What costs money

| Resource | Notes |
|---|---|
| Model calls | Pay per token. The biggest variable cost when you demo a lot |
| Container Apps | The MCP server keeps one small replica running. The API scales to zero |
| Container Registry (Basic) | A small fixed daily charge |
| API Management (Consumption) | Pay per call. The first million calls each month are free |
| Cosmos DB (serverless) | Pay per request and storage. Almost nothing when idle |
| Application Insights and Log Analytics | Small at this volume. The workspace has a daily ingestion cap |

Check the real numbers in Azure Cost Management, filtered to the resource group, after a week.

## Keep it cheap but alive

- Set the MCP server's minimum replicas to 0 (it then has a cold start on the first request).
- Keep an Azure budget alert active.

## Teardown

Dry run first, then delete:

```
.\scripts\teardown.ps1
.\scripts\teardown.ps1 -Delete
```

The script keeps the Foundry resource and its model deployments, because they may share the
resource group. If API Management was deleted, purge the soft-deleted service with
`az apim deletedservice list` and `az apim deletedservice purge`.
