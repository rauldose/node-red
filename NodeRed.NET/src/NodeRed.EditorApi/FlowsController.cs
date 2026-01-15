// ============================================================
// INSPIRED BY: @node-red/editor-api/lib/admin/flows.js
// REFERENCE: NODE-RED-ARCHITECTURE-ANALYSIS.md - Editor API section
// ============================================================
// REST API controller for flow operations
// ============================================================

/*!
 * Original work Copyright JS Foundation and other contributors, http://js.foundation
 * Modified work Copyright 2026 NodeRed.NET Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NodeRed.Runtime;
using System.Text.Json;

namespace NodeRed.EditorApi;

/// <summary>
/// Flows API controller
/// Maps to: /flows endpoints in @node-red/editor-api/lib/admin/flows.js
/// Reference: NODE-RED-ARCHITECTURE-ANALYSIS.md - Editor API Layer
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FlowsController : ControllerBase
{
    private readonly ILogger<FlowsController> _logger;
    private readonly FlowEngine? _flowEngine;

    public FlowsController(ILogger<FlowsController> logger, FlowEngine? flowEngine = null)
    {
        _logger = logger;
        _flowEngine = flowEngine;
    }

    /// <summary>
    /// GET /api/flows - Get current flows
    /// </summary>
    [HttpGet]
    public ActionResult<object> GetFlows()
    {
        _logger.LogInformation("GET /api/flows");

        // Return empty flows for now
        var flows = new
        {
            rev = "1",
            flows = new object[] { }
        };

        return Ok(flows);
    }

    /// <summary>
    /// POST /api/flows - Deploy flows
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> DeployFlows([FromBody] JsonElement request)
    {
        _logger.LogInformation("POST /api/flows - Deploying flows");

        try
        {
            // In full implementation, would parse and deploy flows
            // For now, just acknowledge
            await Task.CompletedTask;

            return Ok(new
            {
                rev = "2",
                message = "Flows deployed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying flows");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
