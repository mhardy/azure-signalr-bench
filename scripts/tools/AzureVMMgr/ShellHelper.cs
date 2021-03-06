using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace JenkinsScript
{
    class ShellHelper
    {
        public static void HandleResult(int errCode, string result)
        {
            if (errCode != 0)
            {
                Util.Log($"Handle result ERR {errCode}: {result}");
                // Environment.Exit(1);
            }
            return;
        }

        public static(int, string) Bash(string cmd, bool wait = true, bool handleRes = false, bool captureConsole = false)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                }
            };
            process.Start();
            var result = "";
            var errCode = 0;
            if (wait == true)
            {
                if (captureConsole)
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        Console.WriteLine(process.StandardOutput.ReadLine());
                    }
                }
                else
                {
                    result = process.StandardOutput.ReadToEnd();
                }
                process.WaitForExit();
                errCode = process.ExitCode;
            }

            if (handleRes == true)
            {
                HandleResult(errCode, result);
            }

            return (errCode, result);
        }

        public static(int, string) ScpDirecotryLocalToRemote(string user, string host, string password, string src, string dst)
        {
            int errCode = 0;
            string result = "";
            string cmd = $"sshpass -p {password} scp -r -o StrictHostKeyChecking=no -o LogLevel=ERROR {src} {user}@{host}:{dst}";
            Util.Log($"scp cmd: {cmd}");
            (errCode, result) = Bash(cmd, wait : true, handleRes : true);
            return (errCode, result);
        }

        public static(int, string) ScpFileLocalToRemote(string user, string host, string password, string srcFile, string dstDir)
        {
            int errCode = 0;
            string result = "";
            string cmd = $"sshpass -p {password} scp -o StrictHostKeyChecking=no  -o LogLevel=ERROR {srcFile} {user}@{host}:{dstDir}";
            Util.Log($"CMD: {user}@{host}: {cmd}");
            (errCode, result) = Bash(cmd, wait : true, handleRes : true);
            return (errCode, result);
        }

        public static(int, string) ScpDirecotryRemoteToLocal(string user, string host, string password, string src, string dst)
        {
            int errCode = 0;
            string result = "";
            string cmd = $"sshpass -p {password} scp -r -o StrictHostKeyChecking=no  -o LogLevel=ERROR {user}@{host}:{src} {dst}";
            Util.Log($"CMD: {user}@{host}: {cmd}");
            (errCode, result) = Bash(cmd, wait : true, handleRes : true);
            return (errCode, result);
        }

        public static(int, string) RemoteBash(string user, string host, int port, string password, string cmd, bool wait = true, bool handleRes = false, int retry = 1, bool captureConsole = false)
        {

            int errCode = 0;
            string result = "";
            for (var i = 0; i < retry; i++)
            {
                if (host.IndexOf("localhost") >= 0 || host.IndexOf("127.0.0.1") >= 0) return Bash(cmd, wait);
                string sshPassCmd = $"echo \"\" > /home/{user}/.ssh/known_hosts; sshpass -p {password} ssh -p {port} -o StrictHostKeyChecking=no  -o LogLevel=ERROR {user}@{host} \"{cmd}\"";
                (errCode, result) = Bash(sshPassCmd, wait : wait, handleRes : retry > 1 && i < retry - 1 ? false : handleRes, captureConsole: captureConsole);
                if (errCode == 0) break;
                Util.Log($"retry {i+1}th time");
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }

            return (errCode, result);
        }

        public static(int, string) KillAllDotnetProcess(List<string> hosts, string repoUrl, string user, string password, int sshPort, string repoRoot = "/home/wanl/signalr_auto_test_framework")
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            hosts.ForEach(host =>
            {
                cmd = $"killall dotnet || true";
                if (host.Contains("localhost") || host.Contains("127.0.0.1")) return;

                Util.Log($"CMD: {user}@{host}: {cmd}");
                var i = 0;
                // retry if error occurs
                while (i < 3)
                {
                    (errCode, result) = ShellHelper.RemoteBash(user, host, sshPort, password, cmd);
                    if (errCode == 0) return;
                    i++;
                }
            });

            if (errCode != 0)
            {
                Util.Log($"ERR {errCode}: {result}");
            }

            return (errCode, result);
        }

        public static(int, string) GitCloneRepo(List<string> hosts, string repoUrl,
            string user, string password, int sshPort,
            string commit = "", string branch = "origin/master",
            string repoRoot = "/home/wanl/signalr_auto_test_framework", bool cleanOldRepo = true)
        {
            var errCode = 0;
            var result = "";

            var tasks = new List<Task>();

            hosts.ForEach(host =>
            {
                if (host.Contains("localhost") || host.Contains("127.0.0.1")) return;
                tasks.Add(Task.Run(() =>
                {
                    var errCodeInner = 0;
                    var resultInner = "";
                    var remoteScriptContent = "";
                    var leftBrace = "{";
                    var rightBrace = "}";
                    if (cleanOldRepo)
                    {
                        remoteScriptContent = $@"
#!/bin/bash
# clean the old repo directory
function cloneRepo() {leftBrace}
    if [ -d {repoRoot} ]
    then
        rm -rf {repoRoot}
    fi
";
                    }
                    else
                    {
                        remoteScriptContent = $@"
#!/bin/bash
# use existing repo if it exists to not break the running process
function cloneRepo() {leftBrace}
    if [ -d {repoRoot} ]
    then
        return
    fi
";
                    }
                    remoteScriptContent += $@"
    export GIT_TRACE_PACKET=1
    export GIT_TRACE=1
    export GIT_CURL_VERBOSE=1
    git clone {repoUrl} {repoRoot}
    rtn=$?
    ## re-check whether repo was cloned successfully
    v=0
    while [ $rtn -ne 0 ] && [ $v -lt 3 ]
    do
        if [ -d {repoRoot} ]
        then
            rm -rf {repoRoot}
        fi
        git clone {repoUrl} {repoRoot}
        rtn=$?
        if [ $rtn -eq 0 ]
        then
            break
        fi
        v=$(($v+1))
    done
    cd {repoRoot}
    git checkout {branch}
{rightBrace}
cloneRepo
";
                    var scriptFile = "remoteScript.sh";
                    using (StreamWriter sw = new StreamWriter(scriptFile))
                    {
                        sw.Write(remoteScriptContent);
                    }
                    var innerCmd = $"chmod +x {scriptFile}; ./{scriptFile}";
                    // execute script on remote machine can be retried if failed
                    var i = 0;
                    var retry = 5;
                    for (; i < retry; i++)
                    {
                        (errCode, result) = ScpFileLocalToRemote(user, host, password, scriptFile, "~/");
                        if (errCode != 0)
                        {
                            // handle error
                            Console.WriteLine("Fail to copy script from local to remote: {errCode}");
                            if (i < retry)
                            {
                                Console.WriteLine("We will retry");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Retry limit reaches, fail finally!");
                            }
                        }
                        (errCodeInner, resultInner) = RemoteBash(user, host, sshPort, password, innerCmd);
                        errCode = errCodeInner;
                        result = resultInner;
                        if (errCodeInner != 0)
                        {
                            // handle error
                            if (i < retry)
                            {
                                Console.WriteLine($"Retry remote bash for {innerCmd}");
                            }
                            else
                            {
                                Console.WriteLine($"Retry limit reaches, fail finally!");
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }));
            });

            Task.WhenAll(tasks).Wait();

            if (errCode != 0)
            {
                Util.Log($"ERR {errCode}: {result}");
                //Environment.Exit(1);
            }

            return (errCode, result);
        }

        public static void WaitServerStarted(List<string> hosts, string user, string password,
            int sshPort, List<string> logPath, string keywords)
        {
            var errCode = 0;
            var result = "";
            // Check whether server started or not in 120 seconds
            for (var i = 0; i < hosts.Count; i++)
            {
                var targetLog = logPath[i];
                var applogFolder = $"log{i}";
                var host = hosts[i];
                var recheckTimeout = 600;
                var recheck = 0;
                string content = null;
                while (recheck < recheckTimeout)
                {
                    Util.Log($"remote copy from {targetLog} to {applogFolder}");
                    (errCode, result) = ScpDirecotryRemoteToLocal(user,
                        host, password, targetLog, applogFolder);
                    if (errCode != 0)
                    {
                        Util.Log($"ERR {errCode}: {result}");
                    }
                    else
                    {
                        // check whether contains the keywords
                        using (StreamReader sr = new StreamReader(applogFolder))
                        {
                            content = sr.ReadToEnd();
                            if (content.Contains(keywords))
                            {
                                Util.Log($"{host} started!");
                                break;
                            }
                        }
                    }
                    Util.Log($"starting server {host}");
                    recheck++;
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                }
                if (recheck == recheckTimeout)
                {
                    Util.Log($"Fail to start server {host}!!!");
                    if (content != null)
                    {
                        Util.Log($"log content: {content}");
                    }

                    // Any server fails to start is a fatal error, so exit.
                    //Environment.Exit(1);
                }
            }
        }
        public static(int, string) StartAppServer(List<string> hosts, string user, string password, int sshPort, List<string> azureSignalrConnectionStrings,
            List<string> logPath, string useLocalSingalR = "false", string appSvrRoot = "/home/wanl/signalr_auto_test_framework")
        {
            var errCode = 0;
            var result = "";

            for (var j = 0; j < hosts.Count; j++)
            {
                var remoteScriptContent = $@"
#!/bin/bash
isRun=`ps axu|grep dotnet|wc -l`
if [ $isRun -eq 1 ]
then
    cd {appSvrRoot}
    export Azure__SignalR__ConnectionString=""{azureSignalrConnectionStrings[j]}""
    export useLocalSignalR={useLocalSingalR}
    dotnet run > {logPath[j]}
else
    echo 'AppServer has started'
fi
";
                var scriptFile = $"remoteScript{j}.sh";
                using (StreamWriter sw = new StreamWriter(scriptFile, false))
                {
                    sw.Write(remoteScriptContent);
                }
                var innerCmd = $"chmod +x {scriptFile}; ./{scriptFile}";
                var i = 0;
                var retry = 5;
                for (; i < retry; i++)
                {
                    (errCode, result) = ScpFileLocalToRemote(user, hosts[j], password, scriptFile, "~/");
                    if (errCode != 0)
                    {
                        // handle error
                        Console.WriteLine("Fail to copy script from local to remote: {errCode}");
                        if (i < retry)
                        {
                            Console.WriteLine("We will retry");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Retry limit reaches, fail finally!");
                        }
                    }
                    (errCode, result) = RemoteBash(user, hosts[j], sshPort, password, innerCmd, wait: false);
                    if (errCode != 0)
                    {
                        // handle error
                        if (i < retry)
                        {
                            Console.WriteLine($"Retry remote bash for {innerCmd}");
                        }
                        else
                        {
                            Console.WriteLine($"Retry limit reaches, fail finally!");
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (errCode != 0)
                {
                    Util.Log($"ERR {errCode}: {result}");
                    //Environment.Exit(1);
                }
            }
            // wait the starting of AppServer, sometimes the log file has not yet generated when we want to read it.
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            WaitServerStarted(hosts, user, password, sshPort, logPath, "HttpConnection Started");
            return (errCode, result);
        }

        public static(int, string) StartRpcAgents(List<string> agents, string user, string password, int sshPort, int rpcPort,
            List<string> logPath, string agentRoot)
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            for (var i = 0; i < agents.Count; i++)
            {
                cmd = $"cd {agentRoot}; dotnet run -- --rpcPort {rpcPort} -d 0.0.0.0 > {logPath[i]}";
                Util.Log($"CMD: {user}@{agents[i]}: {cmd}");
                (errCode, result) = ShellHelper.RemoteBash(user, agents[i], sshPort, password, cmd, wait : false);
                if (errCode != 0) break;
            };
            if (errCode != 0)
            {
                Util.Log($"RPC agent stopped: ERR {errCode}: {result}");
                //Environment.Exit(1);
            }
            // wait the starting of agent VM.
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            WaitServerStarted(agents, user, password, sshPort, logPath, "[0.0.0.0:5555] started");
            return (errCode, result);

        }

        public static(int, string) StartRpcMaster(
            string host, List<string> agents, string user, string password, int sshPort, string logPath,
            string serviceType, string transportType, string hubProtocol, string scenario,
            int connection, int concurrentConnection, int duration, int interval, List<string> pipeLine,
            int groupNum, int groupOverlap, int combineFactor, string messageSize, string serverUrl, string suffix,
            string masterRoot, string sendToFixedClient, bool enableGroupJoinLeave, bool stopSendIfLatencyBig,
            bool stopSendIfConnectionErrorBig, string connectionString)
        {

            Util.Log($"service type: {serviceType}, transport type: {transportType}, hub protocol: {hubProtocol}, scenario: {scenario}");
            var errCode = 0;
            var result = "";
            var cmd = "";

            (errCode, result) = RemoteBash(user, host, sshPort, password, "cd ~; pwd;");
            var userRoot = result.Substring(0, result.Length - 1);
            Util.Log($"user root: {userRoot}");
            var agentList = "";

            for (var i = 0; i < agents.Count; i++)
            {
                agentList += agents[i];
                if (i < agents.Count - 1)
                    agentList += ";";
            }

            var clear = "false";
            var outputCounterFile = "";

            // todo
            var outputCounterDir = Path.Join(userRoot, $"results/{Environment.GetEnvironmentVariable("result_root")}/{suffix}/");
            outputCounterFile = outputCounterDir + $"counters.txt";
            var connectionStringOpt = "";
            var serverOption = "";
            if (connectionString != null)
            {
                if (!connectionString.StartsWith('\'') && !connectionString.StartsWith('"'))
                {
                    connectionStringOpt = $"--connectionString '{connectionString}'";
                }
                else
                {
                    connectionStringOpt = $"--connectionString {connectionString}";
                }
            }
            else
            {
                serverOption = $"--serverUrl '{serverUrl}'";
            }

            var concatPipeline = string.Join(";", pipeLine);

            cmd = $"cd {masterRoot}; ";
            cmd += $"mkdir -p {outputCounterDir} || true;";
            cmd += $"dotnet run -- " +
                $"--rpcPort 5555 " +
                $"--duration {duration} --connections {connection} --interval {interval} --agents {agents.Count} {serverOption} --pipeLine '{string.Join(";", pipeLine)}' " +
                $"-v {serviceType} -t {transportType} -p {hubProtocol} -s {scenario} " +
                $" --agentList '{agentList}' " +
                $" --retry {0} " +
                $" --clear {clear} " +
                $" --concurrentConnection {concurrentConnection} " +
                $" --groupNum {groupNum} " +
                $" --groupOverlap {groupOverlap} " +
                $" --combineFactor {combineFactor} " +
                $"--messageSize {messageSize} " +
                $"--sendToFixedClient {sendToFixedClient} " +
                $"--enableGroupJoinLeave {enableGroupJoinLeave} " +
                $"--stopSendIfLatencyBig {stopSendIfLatencyBig} " +
                $"--stopSendIfConnectionErrorBig {stopSendIfConnectionErrorBig} " +
                $"{connectionStringOpt} " + // this option is only for RestAPI scenario test
                $" -o '{outputCounterFile}' | tee {logPath}";

            Util.Log($"CMD: {user}@{host}: {cmd}");
            (errCode, result) = ShellHelper.RemoteBash(user, host, sshPort, password, cmd, captureConsole: true);

            if (errCode != 0)
            {
                Util.Log($"ERR {errCode}: {result}");
            }

            return (errCode, result);
        }

        public static(int, string) StartSignalrService(List<string> hosts, string user, string password, int sshPort, string serviceDir, List<string> logPath)
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            for (var i = 0; i < hosts.Count; i++)
            {
                cmd = $"cd {serviceDir}; dotnet run > {logPath[i]}";
                Util.Log($"{user}@{hosts[i]}: {cmd}");
                (errCode, result) = ShellHelper.RemoteBash(user, hosts[i], sshPort, password, cmd, wait : false);

                if (errCode != 0)
                {
                    Util.Log($"ERR {errCode}: {result}");
                    //Environment.Exit(1);
                }
            }

            return (errCode, result);

        }
        public static(int, string) CreateSignalrService(ArgsOption argsOption, int unitCount)
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            var content = AzureBlobReader.ReadBlob("SignalrConfigFileName");
            Console.WriteLine($"content: {content}");
            var config = AzureBlobReader.ParseYaml<SignalrConfig>(content);

            // login to azure
            cmd = $"az login --service-principal --username {config.AppId} --password {config.Password} --tenant {config.Tenant}";
            Util.Log($"CMD: signalr service: az login");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            // change subscription
            cmd = $"az account set --subscription {config.Subscription}";
            Util.Log($"CMD: az account set --subscription");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            // var groupName = Util.GenResourceGroupName(config.BaseName);
            // var srName = Util.GenSignalRServiceName(config.BaseName);

            var rnd = new Random();
            var SrRndNum = (rnd.Next(10000) * rnd.Next(10000)).ToString();

            var groupName = config.BaseName + "Group";
            var srName = config.BaseName + SrRndNum + "SR";

            cmd = $"  az extension add -n signalr || true";
            Util.Log($"CMD: signalr service: {cmd}");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            // create resource group
            cmd = $"  az group create --name {groupName} --location {config.Location}";
            Util.Log($"CMD: signalr service: {cmd}");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            //create signalr service
            cmd = $"az signalr create --name {srName} --resource-group {groupName}  --sku {config.Sku} --unit-count {unitCount} --query hostName -o tsv";
            Util.Log($"CMD: signalr service: {cmd}");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            var signalrHostName = result;
            Console.WriteLine($"signalrHostName: {signalrHostName}");

            // get access key
            cmd = $"az signalr key list --name {srName} --resource-group {groupName} --query primaryKey -o tsv";
            Util.Log($"CMD: signalr service: {cmd}");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);
            var signalrPrimaryKey = result;
            Console.WriteLine($"signalrPrimaryKey: {signalrPrimaryKey}");

            // combine to connection string
            signalrHostName = signalrHostName.Substring(0, signalrHostName.Length - 1);
            signalrPrimaryKey = signalrPrimaryKey.Substring(0, signalrPrimaryKey.Length - 1);
            var connectionString = $"Endpoint=https://{signalrHostName};AccessKey={signalrPrimaryKey};";
            Console.WriteLine($"connection string: {connectionString}");
            ShellHelper.Bash($"export AzureSignalRConnectionString='{connectionString}'", handleRes : true);
            return (errCode, connectionString);
        }

        public static(int, string) DeleteSignalr(ArgsOption args)
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            var content = AzureBlobReader.ReadBlob("SignalrConfigFileName");
            var config = AzureBlobReader.ParseYaml<SignalrConfig>(content);

            var groupName = config.BaseName + "Group";

            // login to azure
            cmd = $"az login --service-principal --username {config.AppId} --password {config.Password} --tenant {config.Tenant}";
            Util.Log($"CMD: signalr service: logint azure");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            // delete resource group
            cmd = $"az group delete --name {groupName} --yes";
            Util.Log($"CMD: signalr service: {cmd}");
            (errCode, result) = ShellHelper.Bash(cmd, handleRes : true);

            return (errCode, result);
        }

        public static(int, string) RemoveSyslog(string host, string user, string password, int sshPort)
        {
            var errCode = 0;
            var result = "";
            var cmd = "sudo rm -rf /var/log/syslog";
            Util.Log($"{user}@{host}: {cmd}");
            (errCode, result) = ShellHelper.RemoteBash(user, host, sshPort, password, cmd, handleRes : true);
            return (errCode, result);
        }
        public static(int, string) PrepareLogPath(string host, string user, string password, int sshPort,
            string dstDir, string time, string suffix, bool removeOldLog=true)
        {

            var targetDir = Path.Join(dstDir, time);

            var errCode = 0;
            var result = "";
            var cmd = "";
            if (removeOldLog)
            {
                cmd = $"rm -rf ~/logs || true; rm -rf ~/results || true; mkdir -p {targetDir}";
            }
            else
            {
                cmd = $"[[ ! -e {targetDir} ]] && mkdir -p {targetDir}";
            }

            Util.Log($"{user}@{host}: {cmd}");
            (errCode, result) = ShellHelper.RemoteBash(user, host, sshPort, password, cmd, wait : false);

            if (errCode != 0)
            {
                Util.Log($"Fail to prepare log ERR {errCode}: {result}");
                //Environment.Exit(1);
            }

            result = Path.Join(targetDir, $"log_{suffix}.txt");
            return (errCode, result);

        }

        public static(int, string) CollectStatistics(List<string> hosts, string user, string password, int sshPort, string remote, string local)
        {
            var errCode = 0;
            var result = "";

            hosts.ForEach(host =>
            {
                (errCode, result) = ShellHelper.ScpDirecotryRemoteToLocal(user, host, password, remote, local);
                if (errCode != 0)
                {
                    Util.Log($"Collect statistic ERR {errCode}: {result}");
                }
            });
            return (errCode, result);
        }

        public static(int, string) TransferServiceRuntimeToVm(List<string> hosts, string user, string password, int sshPort, string srcDirParent, string srcDirName, string dstDir)
        {
            var errCode = 0;
            var result = "";
            var cmd = "";

            (errCode, result) = Bash("pwd", true, true);
            var curDir = result;
            // zip local service repo
            cmd = $"cd {srcDirParent}; zip -r serviceruntime.zip {srcDirName}/*; mv serviceruntime.zip {curDir}";
            (errCode, result) = Bash(cmd, true, true);

            // scp
            foreach (var host in hosts)
            {
                (errCode, result) = ScpFileLocalToRemote(user, host, password, "serviceruntime.zip", "~/");
                if (errCode != 0)
                {
                    Util.Log($"Fail to transfer service to VM ERR {errCode}: {result}");
                    //Environment.Exit(1);
                }

                // install unzip
                cmd = $"rm -rf OSS*/; sudo apt-get install -y zip; unzip -o -d {dstDir} ~/serviceruntime.zip";
                RemoteBash(user, host, sshPort, password, cmd, handleRes : true);

                // modify appsetting.json
                var appsettings = File.ReadAllText($"{Path.Join(srcDirParent, srcDirName, "src/Microsoft.Azure.SignalR.ServiceRuntime/appsettings.json")}");
                appsettings = appsettings.Replace("localhost", host);
                File.WriteAllText("appsettings.json", appsettings);
                (errCode, result) = ScpFileLocalToRemote(user, host, password, "appsettings.json", Path.Join(dstDir, srcDirName, "src/Microsoft.Azure.SignalR.ServiceRuntime"));

            }

            (errCode, result) = Bash($"cd {curDir}", true, true);

            return (errCode, result);

        }

        public static(int, string) ModifyServiceAppsettings(List<string> hosts, string user, string password, int sshPort, List<string> dnses, string srcDirParent, string srcDirName, string dstDir)
        {
            var errCode = 0;
            var result = "";

            Util.Log($"modify service appsettings");

            if (hosts.Count != dnses.Count)
            {
                throw new Exception();
            }

            for (var i = 0; i < hosts.Count; i++)
            {
                var appsettings = File.ReadAllText($"{Path.Join(srcDirParent, srcDirName, "src/Microsoft.Azure.SignalR.ServiceRuntime/appsettings.json")}");
                appsettings = appsettings.Replace("localhost", dnses[i]);
                File.WriteAllText("appsettings.json", appsettings);
                (errCode, result) = ScpFileLocalToRemote(user, hosts[i], password, "appsettings.json", Path.Join(dstDir, srcDirName, "src/Microsoft.Azure.SignalR.ServiceRuntime"));
            }
            return (errCode, result);
        }

        public static(int, string) CollectMachineStatistics(string host, string user, string password, int sshPort, string path)
        {
            var errCode = 0;
            var result = "";
            var cmd = "top -n 1 -b | head -n 15; exit";
            (errCode, result) = RemoteBash(user, host, sshPort, password, cmd, handleRes : true);

            File.AppendAllText(path, result);

            return (errCode, result);

        }

    }
}
