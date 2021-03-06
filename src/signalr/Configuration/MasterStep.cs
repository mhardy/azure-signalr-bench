﻿using Serilog;
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Plugin.Microsoft.Azure.SignalR.Benchmark
{
    public class MasterStep
    {
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        
        public string GetMethod()
        {
            try
            {
                Parameters.TryGetValue(SignalRConstants.Method, out object method);
                return (string)method;
            }
            catch (Exception ex)
            {
                var message = $"Method name does not exists: {ex}";
                Log.Error(message);
                throw;
            }
        }

        public bool Parse(YamlMappingNode stepNode)
        {
            var success = true;

            success = Validate(stepNode);
            if (!success) return success;

            foreach (var entry in stepNode)
            {
                var parameterName = ((YamlScalarNode)entry.Key).Value;
                var parameterValue = ((YamlScalarNode)entry.Value).Value;
                success = Parameters.TryAdd(parameterName, parameterValue);
                if (!success) return success;
            }
            return success;
        }

        public void Dump()
        {
            Log.Information("--------");
            foreach (string key in Parameters.Keys)
            {
                Log.Information($"key: {key}, value: {Parameters[key]}");
            }
        }

        // TODO: validate more on Step
        protected bool Validate(YamlMappingNode stepNode)
        {
            var keys = stepNode.Children.Keys;
            if (!keys.Contains(new YamlScalarNode(SignalRConstants.Type))) return false;
            if (!keys.Contains(new YamlScalarNode(SignalRConstants.Method))) return false;
            return true;
        }
    }
}
