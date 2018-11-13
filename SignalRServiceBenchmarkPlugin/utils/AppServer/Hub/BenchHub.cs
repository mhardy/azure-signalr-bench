// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.PerfTest.AppServer
{
    public class BenchHub : Hub
    {
        public void Echo(IDictionary<string, object> data)
        {
            foreach (var entry in data) Console.WriteLine($"{entry.Key} : {entry.Value}");
            Clients.Client(Context.ConnectionId).SendAsync("RecordLatency", data);
        }

        public void Broadcast(IDictionary<string, object> data)
        {
            Console.WriteLine("broadcast");
            Clients.All.SendAsync("RecordLatency", data);
        }

        public void SendToClient(IDictionary<string, object> data)
        {
            var targetId = (string)data["information.ConnectionId"];
            Clients.Client(targetId).SendAsync("RecordLatency", data);
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Clients.Client(Context.ConnectionId).SendAsync("JoinGroup");
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            Clients.Client(Context.ConnectionId).SendAsync("LeaveGroup");
        }

        public void SendToGroup(IDictionary<string, object> data)
        {
            var groupName = (string)data["information.GroupName"];
            Clients.Group(groupName).SendAsync("RecordLatency", data);
        }
    }
}