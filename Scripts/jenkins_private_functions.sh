#!/bin/bash

. ./func_env.sh

function prepare_result_folder_4_scenario()
{
   local tag=$1
   local Transport=$2
   local MessageEncoding=$3
   local Scenario=$4
   ## take care of the gen of this folder which is also required by gen_html.sh
   local scenario_folder=${tag}_${Transport}_${MessageEncoding}_${Scenario}
   export env_statistic_folder=${ScriptWorkingDir}/${result_root}/${scenario_folder}
   
   mkdir -p ${env_statistic_folder}
}

function start_collect_top_for_signalr_and_nginx()
{
    local k8s_result_dir=$env_statistic_folder
    cd $ScriptWorkingDir
    . ./func_env.sh
    . ./kubectl_utils.sh
    local service_name=$(extract_servicename_from_connectionstring $ConnectionString)
    if [ "$service_name" != "" ]
    then
       nohup sh collect_pod_top.sh $service_name $k8s_result_dir &
       collect_pod_top_pid=$!
       if [ "$g_nginx_ns" != "" ]
       then
          nohup sh collect_nginx_top.sh $service_name $g_nginx_ns $k8s_result_dir &
          collect_nginx_top_pid=$!
       fi
    fi
}

function stop_collect_top_for_signalr_and_nginx()
{
    if [ "$collect_pod_top_pid" != "" ]
    then
      # kill the process if it is alive
      local a=`ps -o pid= -p $collect_pod_top_pid`
      if [ "$a" != "" ]
      then
         kill $collect_pod_top_pid
      fi
    fi
    if [ "$collect_nginx_top_pid" != "" ]
    then
      local a=`ps -o pid= -p $collect_nginx_top_pid`
      if [ "$a" != "" ]
      then
         kill $collect_nginx_top_pid
      fi
    fi
}

function copy_log_from_k8s()
{
    local k8s_result_dir=$env_statistic_folder
    cd $ScriptWorkingDir
    . ./func_env.sh
    . ./kubectl_utils.sh
    local service_name=$(extract_servicename_from_connectionstring $ConnectionString)
    disable_exit_immediately_when_fail
    if [ "$service_name" != "" ]
    then
    ############# copy pod log ############
       copy_syslog $service_name $k8s_result_dir
       get_nginx_log $service_name "$g_nginx_ns" $k8s_result_dir
       get_k8s_pod_status $service_name $k8s_result_dir
    fi
    enable_exit_immediately_when_fail
}

function reboot_all_pods()
{
   local connectionString=$1
   cd $ScriptWorkingDir
   . ./kubectl_utils.sh
   local service_name=$(extract_servicename_from_connectionstring $connectionString)
   if [ "$service_name" != "" ]
   then
     restart_all_pods $service_name
   fi
}

function run_benchmark() {
  local unit=$1
  local user=$2
  local passwd="$3"
  local connectStr="$4"
  local tag="unit"$unit
  local Scenario
  local Transport
  local MessageEncoding
  
  for Scenario in $bench_scenario_list
  do
      for Transport in $bench_transport_list
      do
         for MessageEncoding in $bench_encoding_list
         do
            prepare_result_folder_4_scenario $tag $Transport $MessageEncoding $Scenario
            local k8s_result_dir=$env_statistic_folder

            start_collect_top_for_signalr_and_nginx

            run_and_gen_report $tag $Scenario $Transport $MessageEncoding $user $passwd "$connectStr" $k8s_result_dir $unit

            stop_collect_top_for_signalr_and_nginx

            copy_log_from_k8s

            reboot_all_pods "$connectStr"
         done
      done
  done

}

function run_and_gen_report() {
  local tag=$1
  local Scenario=$2
  local Transport=$3
  local MessageEncoding=$4
  local user=$5
  local passwd="$6"
  local connectionString="$7"
  local outputDir="$8"
  local unit=$9

  cd $ScriptWorkingDir
  local appserverUrls=`python extract_ip.py -i $PublicIps -q appserverPub`

  cd $PluginScriptWorkingDir
  local config_path=$PluginScriptWorkingDir/${unit}_${Scenario}_${Transport}_${MessageEncoding}.config
  local maxConnectionOption=""
  if [ "$useMaxConnection" == "true" ]
  then
    maxConnectionOption="-m"
  fi
  python3 generate.py -u $unit -S $Scenario \
                      -t $Transport -p $MessageEncoding \
                      -U $appserverUrls -d $sigbench_run_duration \
                      -c $config_path $maxConnectionOption

  local connection=`python3 get_sending_connection.py -u $unit -S $Scenario -t $Transport -p $MessageEncoding -q totalConnections`
  local concurrentConnection=`python3 get_sending_connection.py -u $unit -S $Scenario -t $Transport -p $MessageEncoding -q concurrentConnection`
  local send=`python3 get_sending_connection.py -u $unit -S $Scenario -t $Transport -p $MessageEncoding -q sendingSteps`
  
  run_command $user $passwd $connectionString $outputDir $config_path
  cd $ScriptWorkingDir
  #### generate the connection configuration for HTML ####
cat << EOF > configs/cmd_4_${MessageEncoding}_${Scenario}_${tag}_${Transport}
connection=${connection}
connection_concurrent=${concurrentConnection}
send=${send}
bench_config_endpoint="$serverUrl"
EOF

  ## gen_html.sh requires bench_name_list, bench_codec_list, and bench_type_list
  export bench_name_list="$Scenario"
  export bench_codec_list="$MessageEncoding"
  export bench_type_list="${tag}_${Transport}"
  sh gen_html.sh $connectionString
}

function build_rpc_master() {
  local targetDir=$1
  local tmpMaster=/tmp/master
  if [ -e $tmpMaster ]
  then
     rm -rf $tmpMaster
  fi
  cd $PluginRpcBuildWorkingDir
  ./build.sh master $tmpMaster
  if [ -e $targetDir/publish ]
  then
     rm -rf $targetDir/publish
  fi
  mv $tmpMaster $targetDir/publish
  cd $targetDir
  tar zcvf publish.tgz publish
  cd -
}

function build_rpc_slave() {
  local targetDir=$1
  local tmpSlave=/tmp/slave
  if [ -e $tmpSlave ]
  then
     rm -rf $tmpSlave
  fi
  cd $PluginRpcBuildWorkingDir
  ./build.sh slave $tmpSlave
  if [ -e $targetDir/publish ]
  then
     rm -rf $targetDir/publish
  fi
  mv $tmpSlave $targetDir/publish
  cd $targetDir
  tar zcvf publish.tgz publish
  cd -
}

build_app_server() {
  local targetDir=$1
  local tmpAppServer=/tmp/appserver
  if [ -e $tmpAppServer ]
  then
     rm -rf $tmpAppServer
  fi
  cd $AppServerWorkingDir
  ./build.sh $tmpAppServer
  if [ -e $targetDir/publish ]
  then
     rm -rf $targetDir/publish
  fi
  mv $tmpAppServer $targetDir/publish
  cd $targetDir
  tar zcvf publish.tgz publish
  cd -
}

disable_exit_immediately_when_fail()
{
  set +e
}

enable_exit_immediately_when_fail()
{
  set -e
}

start_collect_slaves_appserver_top()
{
  local user=$1
  local passwd="$2"
  local outputDir="$3"
  local script_collect_slaves_top="collect_slaves_top.sh"
  local script_collect_appserver_top="collect_appserver_top.sh"
  cd $ScriptWorkingDir
cat << EOF > $script_collect_slaves_top
#!/bin/bash
while [ true ]
do
  for i in `python extract_ip.py -i $PrivateIps -q slaveList`
  do
    sshpass -p $passwd ssh -o StrictHostKeyChecking=no -o LogLevel=ERROR $user@\${i} "top -b -n 1|head -n 17" >> $outputDir/slave_\${i}_top.txt
  done
  sleep 1
done
EOF
cat << EOF > $script_collect_appserver_top
#!/bin/bash
while [ true ]
do
  for i in `python extract_ip.py -i $PrivateIps -q appserverList`
  do
    sshpass -p $passwd ssh -o StrictHostKeyChecking=no -o LogLevel=ERROR $user@\${i} "top -b -n 1|head -n 17" >> $outputDir/appserver_\${i}_top.txt
  done
  sleep 1
done
EOF
  nohup sh $script_collect_slaves_top &
  collect_slaves_top_pid=$!

  nohup sh $script_collect_appserver_top &
  collect_appserver_top_pid=$!
}

stop_collect_slaves_appserver_top()
{
  if [ "$collect_slaves_top_pid" != "" ]
  then
    # kill the process if it is alive
    local a=`ps -o pid= -p $collect_slaves_top_pid`
    if [ "$a" != "" ]
    then
       kill $collect_slaves_top_pid
    fi
  fi
  if [ "$collect_appserver_top_pid" != "" ]
  then
    # kill the process if it is alive
    local a=`ps -o pid= -p $collect_appserver_top_pid`
    if [ "$a" != "" ]
    then
       kill $collect_appserver_top_pid
    fi
  fi
}

function copy_log_from_slaves()
{
  local user=$1
  local passwd="$2"
  local outputDir="$3"
  cd $ScriptWorkingDir
  local i
  for i in `python extract_ip.py -i $PrivateIps -q slaveList`
  do
    sshpass -p $passwd scp -o StrictHostKeyChecking=no -o LogLevel=ERROR $user@${i}:~/slave*.log $outputDir/slave_${i}.log
  done
  cd $outputDir
  tar zcvf slavelog.tgz slave*.log
  rm slave*.log
  cd -
}

function run_command() {
  local user=$1
  local passwd="$2"
  local connectionString="$3"
  local outputDir="$4"
  local configPath=$5
  cd $ScriptWorkingDir
  local master=`python extract_ip.py -i $PrivateIps -q master`
  local appserver=`python extract_ip.py -i $PrivateIps -q appserver`
  local slaves=`python extract_ip.py -i $PrivateIps -q slaves`

  local masterDir=$CommandWorkingDir/master
  local slaveDir=$CommandWorkingDir/slave
  local appserverDir=$CommandWorkingDir/appserver
  mkdir -p $masterDir
  mkdir -p $slaveDir
  mkdir -p $appserverDir
  build_rpc_master $masterDir
  build_rpc_slave $slaveDir
  build_app_server $appserverDir
  cd $CommandWorkingDir
  local remoteCmd="remove_counters.sh"
  cat << EOF > $remoteCmd
#!/bin/bash
if [ -e counters.txt ]
then
  rm counters.txt
fi
EOF
  sshpass -p ${passwd} scp -o StrictHostKeyChecking=no -o LogLevel=ERROR $remoteCmd ${user}@${master}:/home/${user}/
  sshpass -p ${passwd} ssh -o StrictHostKeyChecking=no -o LogLevel=ERROR ${user}@${master} "chmod +x $remoteCmd"
  sshpass -p ${passwd} ssh -o StrictHostKeyChecking=no -o LogLevel=ERROR ${user}@${master} "./$remoteCmd"
  disable_exit_immediately_when_fail
  start_collect_slaves_appserver_top ${user} $passwd ${outputDir}
  cd $CommandWorkingDir
  dotnet run -- --RpcPort=5555 --SlaveList="$slaves" --MasterHostname="$master" --AppServerHostnames="$appserver" \
         --Username=$user --Password=$passwd \
         --AppserverProject="$appserverDir" \
         --MasterProject="$masterDir" \
         --SlaveProject="$slaveDir" \
         --AppserverTargetPath="/home/${user}/appserver.tgz" --MasterTargetPath="/home/${user}/master.tgz" \
         --SlaveTargetPath="/home/${user}/slave.tgz" \
         --BenchmarkConfiguration="$configPath" \
         --BenchmarkConfigurationTargetPath="/home/${user}/signalr.yaml" \
         --AzureSignalRConnectionString="$connectionString" \
         --AppserverLogDirectory="${outputDir}"
  stop_collect_slaves_appserver_top ${user} $passwd ${outputDir}
  enable_exit_immediately_when_fail
  sshpass -p ${passwd} scp -o StrictHostKeyChecking=no -o LogLevel=ERROR ${user}@${master}:/home/${user}/counters.txt ${outputDir}/
  copy_log_from_slaves ${user} $passwd ${outputDir}
}

function create_asrs()
{
  local rsg=$1
  local name=$2
  local unit=$3

. ./az_signalr_service.sh
. ./kubectl_utils.sh

  local signalr_service=$(create_signalr_service $rsg $name "Basic_DS2" $unit)
  if [ "$signalr_service" == "" ]
  then
    echo "Fail to create SignalR Service"
    return
  else
    echo "Create SignalR Service '${signalr_service}'"
  fi
  local dns_ready=$(check_signalr_service_dns $rsg $name)
  if [ $dns_ready -eq 1 ]
  then
    echo "SignalR Service DNS is not ready, suppose it is failed!"
    delete_signalr_service $name $rsg
    return
  fi
  if [ "$ASRSEnv" == "dogfood" ]
  then
    if [ "$Disable_UNIT_PER_POD" == "true" ]
    then
      patch_deployment_for_no_tc_and_wait $name 100
    fi
    if [ "$Disable_Connection_Throttling" == "true" ]
    then
      local limit=`python gen_connection_throttling.py -u "unit"$unit`
      if [ $limit -ne 0 ]
      then
         patch_connection_throttling_env $name $limit
      fi
    fi
  fi
  local connectionStr=$(query_connection_string $name $rsg)
  echo "[ConnectionString]: $connectionStr"
  export ConnectionString="$connectionStr"
}

## exit handler to remove resource group ##
# global env:
# CurrentWorkingDir, ServicePrincipal, AgentConfig, VMMgrDir
function remove_resource_group() {
  echo "!!Received EXIT!! and remove all created VMs"

  cd $CurrentWorkingDir
  local clean_vm_daemon=daemon_${JOB_NAME}_cleanvms
  local clean_asrs_daemon=daemon_${JOB_NAME}_cleanasrs
  ## remove all test VMs
  local pid_file_path=/tmp/${result_root}_pid_remove_rsg.txt
cat << EOF > /tmp/clean_vms.sh
${VMMgrDir}/JenkinsScript --step=DeleteResourceGroupByConfig --AgentConfigFile=$AgentConfig --DisableRandomSuffix --ServicePrincipal=$ServicePrincipal
EOF
  daemonize -v -o /tmp/${clean_vm_daemon}.out -e /tmp/${clean_vm_daemon}.err -E BUILD_ID=dontKillcenter /usr/bin/nohup /bin/sh /tmp/clean_vms.sh &
  ## remove ASRS
cat << EOF > /tmp/clean_asrs.sh
cd $ScriptWorkingDir
. ./az_signalr_service.sh

if [ "$ASRSEnv" == "dogfood" ]
then
  az_login_ASRS_dogfood
  delete_group $DogFoodResourceGroup
  unregister_signalr_service_dogfood
else
  az_login_signalr_dev_sub
  delete_group $DogFoodResourceGroup
fi
EOF
  daemonize -v -o /tmp/${clean_asrs_daemon}.out -e /tmp/${clean_asrs_daemon}.err -E BUILD_ID=dontKillcenter /usr/bin/nohup /bin/sh /tmp/clean_asrs.sh &
}
