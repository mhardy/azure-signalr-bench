﻿using Grpc.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rpc.Service
{
    public class RpcClient : IRpcClient
    {
        private RpcService.RpcServiceClient _client;

        public Func<IDictionary<string, object>, string> Serialize { get; set; } = null;

        public Func<string, IDictionary<string, object>> Deserialize { get; set; } = null;

        private RpcClient(RpcService.RpcServiceClient client)
        {
            _client = client;
        }

        public async Task<IDictionary<string, object>> QueryAsync(IDictionary<string, object> data)
        {
            try
            {
                var result = await _client.QueryAsync(new Data { Json = Serialize(data) }).ResponseAsync;
                if (!result.Success) throw new Exception(result.Message);
                var returnData = Deserialize(result.Json);
                return returnData;
            }
            catch (Exception ex)
            {
                var message = $"Rpc error:{Environment.NewLine} {ex}";
                Log.Error(message);
                throw;
            }
        }

        private static Channel CreateRpcChannel(string hostname, int port)
        {
            Log.Information("Open channel to rpc server...");
            var channel = new Channel($"{hostname}:{port}", ChannelCredentials.Insecure,
                    new ChannelOption[] {
                        // For Group, the received message size is very large, so here set 8000k
                        new ChannelOption(ChannelOptions.MaxReceiveMessageLength, 8192000)
                    });

            return channel;
        }

        private static RpcService.RpcServiceClient CreateRpcClient(Channel channel)
        {
            Log.Information($"Create Rpc client...");
            var client = new RpcService.RpcServiceClient(channel);
            return client;
        }

        public static IRpcClient Create(string hostname, int port)
        {
            var channel = CreateRpcChannel(hostname, port);
            return new RpcClient(CreateRpcClient(channel));
        }

        public bool TestConnection()
        {
            var result = _client.TestConnection(new Empty());
            return result.Success;
        }

        public async Task<bool> InstallPluginAsync(string pluginName)
        {
            Log.Information($"Install plugin '{pluginName}' in agent...");
            var result = await _client.InstallPluginAsync(new Data { Json = pluginName }).ResponseAsync;
            if (!result.Success) Log.Error($"Fail to install plugin in agent: {result.Message}");
            return result.Success;
        }

        /*
        public bool CheckTypeAndMethod(IDictionary<string, object> data)
        {
            if (data.ContainsKey(Constants.Type) && data.ContainsKey(Constants.Method)) return true;
            return false;
        }
        */

        public void InstallSerializerAndDeserializer(
            Func<IDictionary<string, object>, string> serialize,
            Func<string, IDictionary<string, object>> deserialize)
        {
            Serialize = serialize;
            Deserialize = deserialize;
        }
    }
}
