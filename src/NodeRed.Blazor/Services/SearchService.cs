// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Blazor.Models;

namespace NodeRed.Blazor.Services;

/// <summary>
/// Service for searching nodes and flows in the editor.
/// Matches the functionality of RED.search in the JS implementation.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches for nodes and flows matching the query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="nodes">All nodes to search</param>
    /// <param name="flows">All flows to search</param>
    /// <returns>List of search results</returns>
    List<SearchResult> Search(string query, IDictionary<string, NodeData> nodes, IEnumerable<FlowTab> flows);
}

/// <summary>
/// Implementation of the search service
/// </summary>
public class SearchService : ISearchService
{
    public List<SearchResult> Search(string query, IDictionary<string, NodeData> nodes, IEnumerable<FlowTab> flows)
    {
        var results = new List<SearchResult>();
        
        if (string.IsNullOrWhiteSpace(query))
            return results;
            
        var queryLower = query.ToLower();
        
        // Build flow lookup
        var flowLookup = flows.ToDictionary(f => f.Id, f => f.Label);
        
        // Search in nodes
        foreach (var node in nodes.Values)
        {
            bool matches = false;
            
            // Search by name
            if (!string.IsNullOrEmpty(node.Name) && node.Name.ToLower().Contains(queryLower))
                matches = true;
            
            // Search by type
            if (node.Type.ToLower().Contains(queryLower))
                matches = true;
            
            // Search by ID
            if (node.Id.ToLower().Contains(queryLower))
                matches = true;
            
            if (matches)
            {
                results.Add(new SearchResult
                {
                    Id = node.Id,
                    Name = string.IsNullOrEmpty(node.Name) ? node.Type : node.Name,
                    Type = node.Type,
                    FlowId = node.Z,
                    FlowName = flowLookup.TryGetValue(node.Z, out var flowName) ? flowName : "Unknown"
                });
            }
        }
        
        // Search in flows
        foreach (var flow in flows)
        {
            if (flow.Label.ToLower().Contains(queryLower))
            {
                results.Add(new SearchResult
                {
                    Id = flow.Id,
                    Name = flow.Label,
                    Type = "flow",
                    FlowId = flow.Id,
                    FlowName = flow.Label
                });
            }
        }
        
        return results;
    }
}
