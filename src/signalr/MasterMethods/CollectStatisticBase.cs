﻿using Common;
using Newtonsoft.Json.Linq;
using Plugin.Microsoft.Azure.SignalR.Benchmark.Internals;
using Rpc.Service;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Plugin.Microsoft.Azure.SignalR.Benchmark.MasterMethods
{
    public class CollectStatisticBase : ICollector
    {
        private long _latencyStep;
        private long _latencyMax;
        private long _interval;
        private string _type;
        private string _statisticsOutputPath;
        private double[] _percentileList = SignalRConstants.PERCENTILE_LIST
                                                           .Split(",")
                                                           .Select(ind => Convert.ToDouble(ind)).ToArray();
        private System.Timers.Timer _timer;
        private bool _printLatency = true;
        private object _lock = new object();

        protected void ExtractParams(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters)
        {
            // Get parameters
            stepParameters.TryGetTypedValue(SignalRConstants.Interval, out long interval, Convert.ToInt64);
            stepParameters.TryGetTypedValue(SignalRConstants.Type, out string type, Convert.ToString);
            stepParameters.TryGetTypedValue(SignalRConstants.StatisticsOutputPath, out string statisticsOutputPath, Convert.ToString);
            _statisticsOutputPath = statisticsOutputPath;
            _interval = interval;
            _type = type;

            // Get context
            pluginParameters.TryGetTypedValue($"{SignalRConstants.LatencyStep}.{_type}", out _latencyStep, Convert.ToInt64);
            pluginParameters.TryGetTypedValue($"{SignalRConstants.LatencyMax}.{_type}", out _latencyMax, Convert.ToInt64);

            if (stepParameters.TryGetValue(SignalRConstants.PercentileList, out _))
            {
                stepParameters.TryGetTypedValue(SignalRConstants.PercentileList, out string percentileListStr, Convert.ToString);
                _percentileList = percentileListStr.Split(",").Select(ind => Convert.ToDouble(ind)).ToArray();
            }
            if (stepParameters.TryGetValue(SignalRConstants.StatPrintMode, out _))
            {
                stepParameters.TryGetTypedValue(SignalRConstants.StatPrintMode, out _printLatency);
            }
        }

        protected void ClearOldStatistics()
        {
            if (File.Exists(_statisticsOutputPath))
            {
                File.Delete(_statisticsOutputPath);
            }
        }

        protected void CollectStatistics(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters,
            IList<IRpcClient> clients,
            Func<IDictionary<string, object>, IDictionary<string, object>, IList<IRpcClient>, Task> callback)
        {
            ExtractParams(stepParameters, pluginParameters);
            ClearOldStatistics();
            // Start timer
            _timer = new System.Timers.Timer(_interval);
            _timer.Elapsed += async (sender, e) =>
            {
                try
                {
                    await callback(stepParameters, pluginParameters, clients);
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex}");
                }
            };
            _timer.Start();

            // Save timer to plugin
            pluginParameters[$"{SignalRConstants.StopCollector}.{_type}"] = this;
        }

        private void PrintAndSave(IDictionary<string, long> merged)
        {
            // Display merged statistics
            if (_printLatency)
            {
                Log.Information(Environment.NewLine + $"Statistic type: {_type}" + Environment.NewLine + merged.GetContents());
            }
            // Save to file
            SaveToFile(merged, _statisticsOutputPath);
        }

        protected async Task LatencyEventerCallback(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters, IList<IRpcClient> clients)
        {
            var results = await Task.WhenAll(from client in clients
                                             select client.QueryAsync(stepParameters));
            // Merge statistics
            var merged = SignalRUtils.MergeStatistics(results, _latencyMax, _latencyStep);
            PrintAndSave(merged);
        }

        protected async Task ConnectionStatEventerCallback(
            IDictionary<string, object> stepParameters,
            IDictionary<string, object> pluginParameters, IList<IRpcClient> clients)
        {
            var results = await Task.WhenAll(from client in clients
                                             select client.QueryAsync(stepParameters));
            // Merge statistics
            var merged = SignalRUtils.MergeStatistics(results, _latencyMax, _latencyStep);
            var connectionStatMerged = SignalRUtils.MergeConnectionStatistics(results, _percentileList.ToArray());
            merged = merged.Union(connectionStatMerged).ToDictionary(entry => entry.Key, entry => entry.Value);
            PrintAndSave(merged);
        }

        protected void SaveToFile(IDictionary<string, long> mergedResult, string path)
        {
            var record = new JObject
            {
                { SignalRConstants.StatisticsTimestamp, Util.Timestamp2DateTimeStr(Util.Timestamp()) },
                { SignalRConstants.StatisticsCounters, JObject.FromObject(mergedResult)}
            };

            string oneLineRecord = Regex.Replace(record.ToString(), @"\s+", "");
            oneLineRecord = Regex.Replace(oneLineRecord, @"\t|\n|\r", "");
            oneLineRecord += Environment.NewLine;
            lock (_lock)
            {
                using (StreamWriter sw = new StreamWriter(path, true))
                {
                    sw.Write(oneLineRecord);
                }
            }
        }

        public void StopCollector()
        {
            _timer.Stop();
            _timer.Dispose();
            StatisticsParser.Parse(
                _statisticsOutputPath,
                _percentileList,
                _latencyStep,
                _latencyMax,
                Log.Information);
        }
    }
}
