#!/bin/bash

function prepare() {
  mkdir $env_g_root
}

function create_html() {
  cp tmpl/analysis.html $env_g_root/index.html
}

function publish_html() {
  if [ ! -e $env_g_nginx_root_dir ]
  then
     mkdir -p $env_g_nginx_root_dir
  fi
  mv $env_g_root $env_g_nginx_root_dir/
}

if [ -e jenkins_stat_env.sh ]
then
  . ./jenkins_stat_env.sh
  export env_g_http_base=$env_g_http_base
  export env_g_nginx_root_dir=$env_g_nginx_root_dir
  export nginx_server_dns=$env_g_dns
  export env_g_scenario_list="$env_g_scenario_list"
else
  echo "Specify jenkins_stat_env.sh which contains required parameters"
  exit 1
fi

prepare

if [ -e $env_g_root/table.csv ]
then
  rm $env_g_root/table.csv
fi

if [ "$env_g_kind" == "longrun" ]
then
 ./categorize_longrun_report.sh $env_g_start_date $env_g_end_date | tee $env_g_root/table.csv
else
 ./categorize_report.sh $env_g_start_date $env_g_end_date | tee $env_g_root/table.csv
fi

python gen_cate_html.py -i $env_g_root/table.csv > $env_g_root/latency_table_1s_category.js

create_html

publish_html

sh send_mail.sh $env_g_nginx_root_dir/$env_g_root/index.html
