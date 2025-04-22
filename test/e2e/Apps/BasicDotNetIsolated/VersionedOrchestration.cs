// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class VersionedOrchestration
{
    [Function(nameof(VersionedOrchestration))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HelloCities));
        logger.LogInformation($"Versioned orchestration! Version: {context.Version}");

        return await context.CallActivityAsync<string>(nameof(SayVersion), context.Version);
    }

    [Function(nameof(SayVersion))]
    public static string SayVersion([ActivityTrigger] string version, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayVersion");
        logger.LogInformation("Activity running with version: {name}.", version);
        return $"Version: {version}";
    }

    [Function("OrchestrationVersion_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string version)
    {
        ILogger logger = executionContext.GetLogger("VersionedOrchestration_HttpStart");

        // Function input comes from the request content.
        string instanceId;
        if (!string.IsNullOrEmpty(version))
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(new TaskName(nameof(VersionedOrchestration), version));
        }
        else
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(VersionedOrchestration));
        }

        logger.LogInformation("Started orchestration with ID = '{instanceId}' and Version = '{version}'.", instanceId, version);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
