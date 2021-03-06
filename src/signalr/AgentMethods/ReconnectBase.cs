﻿using Common;
using Plugin.Microsoft.Azure.SignalR.Benchmark.AgentMethods.Statistics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Plugin.Microsoft.Azure.SignalR.Benchmark.SignalREnums;

namespace Plugin.Microsoft.Azure.SignalR.Benchmark.AgentMethods
{
    public class ReconnectBase : BatchConnectionBase
    {
        protected async Task<IDictionary<string, object>> RunReconnect(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters)
        {
            try
            {
                // Get parameters
                stepParameters.TryGetTypedValue(SignalRConstants.HubUrls, out string urls, Convert.ToString);
                stepParameters.TryGetTypedValue(SignalRConstants.HubProtocol, out string protocol, Convert.ToString);
                stepParameters.TryGetTypedValue(SignalRConstants.TransportType, out string transportType, Convert.ToString);
                stepParameters.TryGetTypedValue(SignalRConstants.Type, out string type, Convert.ToString);
                stepParameters.TryGetTypedValue(SignalRConstants.ConcurrentConnection, out int concurrentConnection, Convert.ToInt32);

                var clientType = SignalREnums.ClientType.AspNetCore;
                if (pluginParameters.TryGetValue($"{SignalRConstants.ConnectionType}.{type}", out _))
                {
                    pluginParameters.TryGetTypedValue($"{SignalRConstants.ConnectionType}.{type}",
                        out var connectionType, Convert.ToString);
                    if (Enum.TryParse(connectionType, out SignalREnums.ClientType ct))
                    {
                        clientType = ct;
                    }
                }

                if (SignalRUtils.isUsingInternalApp(stepParameters) && clientType == ClientType.AspNetCore)
                {
                    // rewrite the URL to be localhost
                    stepParameters[SignalRConstants.HubUrls] = SignalRConstants.LocalhostUrl;
                    urls = SignalRConstants.LocalhostUrl;
                }
                // Get context
                pluginParameters.TryGetTypedValue($"{SignalRConstants.ConnectionIndex}.{type}",
                    out List<int> connectionIndex, (obj) => (List<int>)obj);
                pluginParameters.TryGetTypedValue($"{SignalRConstants.ConnectionStore}.{type}",
                    out IList<IHubConnectionAdapter> connections, (obj) => (IList<IHubConnectionAdapter>)obj);
                pluginParameters.TryGetTypedValue($"{SignalRConstants.RegisteredCallbacks}.{type}",
                    out var registeredCallbacks, obj => (IList<Action<IList<IHubConnectionAdapter>, StatisticsCollector>>)obj);
                pluginParameters.TryGetTypedValue($"{SignalRConstants.StatisticsStore}.{type}",
                    out StatisticsCollector statisticsCollector, obj => (StatisticsCollector)obj);

                SignalRUtils.DumpConnectionInternalStat(connections);
                // Re-create broken connections in their original index position
                var newConnections = await RecreateBrokenConnections(
                    connections, connectionIndex, urls,
                    transportType, protocol,
                    SignalRConstants.ConnectionCloseTimeout,
                    clientType);
                if (newConnections.Count == 0)
                {
                    // skip reconnect because of no drop
                    Log.Information("Skip reconnect");
                    return null;
                }
                Log.Information($"Start {newConnections.Count} reconnections");
                // Re-setCallbacks
                foreach (var registerCallback in registeredCallbacks)
                {
                    registerCallback(newConnections, statisticsCollector);
                }
                // It must use original connections instead of 'newConnections' here
                // because the 'connectionSuccessFlag' is for original connections
                await BatchConnect(
                    stepParameters,
                    pluginParameters,
                    connections,
                    concurrentConnection);
                Log.Information($"Finish {newConnections.Count} reconnections");
                var recoverred = (from i in Enumerable.Range(0, newConnections.Count)
                                  where newConnections[i].GetStat() == SignalREnums.ConnectionInternalStat.Active
                                  select newConnections[i]).ToList();
                statisticsCollector.UpdateReconnect(recoverred.Count);
                SignalRUtils.DumpConnectionInternalStat(connections);
                return null;
            }
            catch (Exception ex)
            {
                var message = $"Fail to reconnect: {ex}";
                Log.Error(message);
                throw;
            }
        }

        private async Task<IList<IHubConnectionAdapter>> RecreateBrokenConnections(
            IList<IHubConnectionAdapter> connections,
            IList<int> connectionIndex,
            string urls,
            string transportTypeString,
            string protocolString,
            int closeTimeout,
            ClientType clientType)
        {
            // Filter broken connections and local index
            var packages = (from i in Enumerable.Range(0, connections.Count)
                            where connections[i].GetStat() == SignalREnums.ConnectionInternalStat.Stopped
                            select new { Connection = connections[i], LocalIndex = i, GlobalIndex = connectionIndex[i] }).ToList();

            // Destroy broken connections
            foreach (var package in packages)
            {
                await package.Connection.StopAsync();
                await package.Connection.DisposeAsync();
            }

            var globalConnIndex = (from pkg in packages select pkg.GlobalIndex).ToList();
            // Re-create connections
            var newConnections = SignalRUtils.CreateClientConnection(
                transportTypeString,
                protocolString,
                urls,
                globalConnIndex,
                clientType);

            // Setup connection drop handler
            SignalRUtils.SetConnectionOnClose(newConnections);

            // Map new connections to orignal connection list
            for (var i = 0; i < newConnections.Count; i++)
            {
                connections[packages[i].LocalIndex] = newConnections[i];
            }

            return newConnections;
        }
    }
}
