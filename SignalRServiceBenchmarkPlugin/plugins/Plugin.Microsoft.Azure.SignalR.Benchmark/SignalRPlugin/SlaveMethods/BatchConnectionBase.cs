﻿using Common;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Plugin.Microsoft.Azure.SignalR.Benchmark.SlaveMethods
{
    public class BatchConnectionBase
    {
        public async Task BatchReconnect(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters,
            IList<IHubConnectionAdapter> connections,
            int concurrentConnection,
            List<SignalREnums.ConnectionState> connectionsSuccessFlag)
        {
            var packages = (from i in Enumerable.Range(0, connections.Count())
                            select (Connection: connections[i], LocalIndex: i,
                            ConnectionsSuccessFlag: connectionsSuccessFlag,
                            NormalState: SignalREnums.ConnectionState.Success,
                            AbnormalState: SignalREnums.ConnectionState.Fail)).ToList();
            await Task.WhenAll(Util.BatchProcess(packages, SignalRUtils.StartConnect, concurrentConnection));
        }

        public async Task BatchConnect(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters,
            IList<IHubConnectionAdapter> connections,
            int concurrentConnection,
            List<SignalREnums.ConnectionState> connectionsSuccessFlag)
        {
            SignalRUtils.TryGetBatchMode(
                stepParameters,
                out string batchConfigMode,
                out int batchWaitMilliSeconds,
                out SignalREnums.BatchMode mode);
            var packages = (from i in Enumerable.Range(0, connections.Count())
                            select (Connection: connections[i], LocalIndex: i,
                            ConnectionsSuccessFlag: connectionsSuccessFlag,
                            NormalState: SignalREnums.ConnectionState.Success,
                            AbnormalState: SignalREnums.ConnectionState.Fail)).ToList();
            // 100 milliseconds is the default fine-granularity
            var period = SignalRConstants.RateLimitDefaultGranularity;
            var factor = 1000 / period;
            var fillTokenPerDuration = concurrentConnection > factor ? concurrentConnection / factor : 1;
            switch (mode)
            {
                case SignalREnums.BatchMode.ExtLimitRatePress:
                    await Task.WhenAll(Util.ExternalRateLimitBatchProcess(packages,
                        SignalRUtils.StartConnect, concurrentConnection, fillTokenPerDuration, period));
                    break;
                case SignalREnums.BatchMode.LimitRatePress:
                    await Util.RateLimitBatchProces(packages,
                        SignalRUtils.StartConnect, concurrentConnection, fillTokenPerDuration, period);
                    break;
                case SignalREnums.BatchMode.HighPress:
                    await Task.WhenAll(Util.BatchProcess(packages,
                        SignalRUtils.StartConnect, concurrentConnection));
                    break;
                case SignalREnums.BatchMode.LowPress:
                    await Task.WhenAll(Util.LowPressBatchProcess(packages,
                        SignalRUtils.StartConnect, concurrentConnection, batchWaitMilliSeconds));
                    break;
            }
            //Log.Information("After finishing start connection");
            //SignalRUtils.DumpConnectionStatus(connectionsSuccessFlag);
        }
    }
}