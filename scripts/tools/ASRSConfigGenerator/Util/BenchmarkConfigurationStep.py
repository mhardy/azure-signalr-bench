from Util.Common import *

Key = {
    'ActionAfterConnect': 'Parameter.ActionAfterConnect',
    'BatchMode': 'Parameter.BatchMode',
    'BatchWait': 'Parameter.BatchWait',
    'ConcurrentConnection': 'Parameter.ConcurrentConnection',
    'ConnectionTotal': 'Parameter.ConnectionTotal',
    'CriteriaMaxFailConnectionAmount': 'Parameter.CriteriaMaxFailConnectionAmount',
    'CriteriaMaxFailConnectionPercentage': 'Parameter.CriteriaMaxFailConnectionPercentage',
    'CriteriaMaxFailSendingPercentage': 'Parameter.CriteriaMaxFailSendingPercentage',
    'Duration': 'Parameter.Duration',
    'GroupConfigMode': 'Parameter.Mode',
    'GroupCount': 'Parameter.GroupCount',
    'GroupInternalModulo': 'Parameter.GroupInternalModulo',
    'GroupInternalRemainderBegin': 'Parameter.GroupInternalRemainderBegin',
    'GroupInternalRemainderEnd': 'Parameter.GroupInternalRemainderEnd',
    'GroupLevelRemainderBegin': 'Parameter.GroupLevelRemainderBegin',
    'GroupLevelRemainderEnd': 'Parameter.GroupLevelRemainderEnd',
    'HubUrl': 'Parameter.HubUrl',
    'Interval': 'Parameter.Interval',
    'LatencyMax': 'Parameter.LatencyMax',
    'LatencyStep': 'Parameter.LatencyStep',
    'MessageSize': 'Parameter.MessageSize',
    'Method': 'Method',
    'ModuleName': 'ModuleName',
    'Modulo': 'Parameter.Modulo',
    'PercentileList': 'Parameter.PercentileList',
    'Pipeline': 'Pipeline',
    'Protocol': 'Parameter.Protocol',
    'RemainderBegin': 'Parameter.RemainderBegin',
    'RemainderEnd': 'Parameter.RemainderEnd',
    'StatisticsOutputPath': 'Parameter.StatisticsOutputPath',
    'StreamingItemCount': 'Parameter.StreamingItemCount',
    'StreamingItemSendInterval': 'Parameter.StreamingItemSendInterval',
    'TransportType': 'Parameter.TransportType',
    'Types': 'Types',
    'Type': 'Type'

}


# Step definitions
def required(type_, method):
    return {
        Key['Type']: type_,
        Key['Method']: method
    }


def wait(type_, duration):
    return [{
        **required(type_, "Wait"),
        **{
            Key['Duration']: duration
        }
    }]


def conditional_stop(type_,
                     criteria_max_fail_connection_percentage,
                     criteria_max_fail_connection_amount,
                     criteria_max_fail_sending_percentage):
    return [{
        **required(type_, "ConditionalStop"),
        **{
            Key['CriteriaMaxFailConnectionPercentage']: criteria_max_fail_connection_percentage,
            Key['CriteriaMaxFailConnectionAmount']: criteria_max_fail_connection_amount,
            Key['CriteriaMaxFailSendingPercentage']: criteria_max_fail_sending_percentage
        }
    }]


def reconnect(type_, connection_total, hub_url, protocol,
              transport_type, concurrent, batch_mode, batch_wait):
    return [{
        **required(type_, "Reconnect"),
        **{
            Key['ConnectionTotal']: connection_total,
            Key['HubUrl']: hub_url,
            Key['Protocol']: protocol,
            Key['TransportType']: transport_type,
            Key['ConcurrentConnection']: concurrent,
            Key['BatchMode']: batch_mode,
            Key['BatchWait']: batch_wait
        }
    }]

def register_callback_on_connected(type_):
    return [dict(required(type_, "RegisterCallbackOnConnected"))]

def register_callback_record_latency(type_):
    return [dict(required(type_, "RegisterCallbackRecordLatency"))]


def register_callback_join_group(type_):
    return [dict(required(type_, "RegisterCallbackJoinGroup"))]


def register_callback_leave_group(type_):
    return [dict(required(type_, "RegisterCallbackLeaveGroup"))]


def init_statistics_collector(type_, latency_max, latency_step):
    return [{
        **required(type_, "InitStatisticsCollector"),
        **{
            Key['LatencyMax']: latency_max,
            Key['LatencyStep']: latency_step
        }
    }]

def init_connection_statistics_collector(type_, latency_max, latency_step):
    return [{
        **required(type_, "InitConnectionStatisticsCollector"),
        **{
            Key['LatencyMax']: latency_max,
            Key['LatencyStep']: latency_step
        }
    }]

def collect_statistics(type_, interval, output_path):
    return [{
        **required(type_, "CollectStatistics"),
        **{
            Key['Interval']: interval,
            Key['StatisticsOutputPath']: output_path
        }
    }]

def collect_connection_statistics(type_, interval, output_path, percentileList):
    return [{
        **required(type_, "CollectConnectionStatistics"),
        **{
            Key['Interval']: interval,
            Key['PercentileList']: percentileList,
            Key['StatisticsOutputPath']: output_path
        }
    }]

def stop_collector(type_):
    return [dict(required(type_, "StopCollector"))]


def create_connection(type_, connection_total, hub_url, protocol, transport_type, connection_type):
    argType = ArgType()
    createConnectionName = argType.ConnectionTypeName(connection_type)
    return [{
        **required(type_, createConnectionName),
        **{
            Key['ConnectionTotal']: connection_total,
            Key['HubUrl']: hub_url,
            Key['Protocol']: protocol,
            Key['TransportType']: transport_type
        }
    }]


def start_connection(type_, concurrent_connection, batch_mode='HighPress', batch_wait=1000):
    return [{
        **required(type_, "StartConnection"),
        **{
            Key['ConcurrentConnection']: concurrent_connection,
            Key['BatchMode']: batch_mode,
            Key['BatchWait']: batch_wait
        }
    }]


def stop_connection(type_):
    return [required(type_, "StopConnection")]


def dispose_connection(type_):
    return [required(type_, "DisposeConnection")]


def collect_connection_id(type_):
    return [required(type_, "CollectConnectionId")]

def repair_connection(type_, post_action="None"):
    return [{
        **required(type_, "RepairConnections"),
        **{
            Key['ActionAfterConnect']: post_action
        }
    }]

def generate_send(type_, method, duration, interval, remainder_begin, remainder_end, modulo, message_size):
    return {
        **required(type_, method),
        **{
            Key['Duration']: duration,
            Key['Interval']: interval,
            Key['RemainderBegin']: remainder_begin,
            Key['RemainderEnd']: remainder_end,
            Key['Modulo']: modulo,
            Key['MessageSize']: message_size
        }
    }

def streaming_echo(type_, method, duration, interval,
                   remainder_begin, remainder_end, modulo,
                   message_size, streaming_item_count,
                   streaming_item_send_interval):
    return {
        **generate_send(type_, method, duration, interval, remainder_begin, remainder_end, modulo,
                         message_size),
        **{
            Key['StreamingItemCount']: streaming_item_count,
            Key['StreamingItemSendInterval']: streaming_item_send_interval
        }
    }

def send_to_client(type_, method, connection_total, duration, interval, remainder_begin, remainder_end, modulo, message_size):
    return [{
        **generate_send(type_, method, duration, interval, remainder_begin, remainder_end, modulo,
                         message_size),
        **{
            Key['ConnectionTotal']: connection_total
        }
    }]


def join_leave_group(type_, method, group_count, connection_total):
    return {
        **required(type_, method),
        **{
            Key['GroupCount']: group_count,
            Key['ConnectionTotal']: connection_total
        }
    }


def join_group(type_, group_count, connection_total):
    return [join_leave_group(type_, "JoinGroup", group_count, connection_total)]


def leave_group(type_, group_count, connection_total):
    return [join_leave_group(type_, "LeaveGroup", group_count, connection_total)]


def group_group_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                     group_level_remainder_begin, group_level_remainder_end, group_internal_remainder_begin,
                     group_internal_remainder_end, group_internal_modulo):
    return {
        **required(type_, method),
        **{
            Key['Duration']: duration,
            Key['Interval']: interval,
            Key['MessageSize']: message_size,
            Key['ConnectionTotal']: connection_total,
            Key['GroupCount']: group_count,
            Key['GroupLevelRemainderBegin']: group_level_remainder_begin,
            Key['GroupLevelRemainderEnd']: group_level_remainder_end,
            Key['GroupInternalRemainderBegin']: group_internal_remainder_begin,
            Key['GroupInternalRemainderEnd']: group_internal_remainder_end,
            Key['GroupInternalModulo']: group_internal_modulo,
            Key['GroupConfigMode']: 'Group'
        }
    }


def group_connection_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                          remainder_begin, remainder_end, modulo):
    return {
        **required(type_, method),
        **{
            Key['Duration']: duration,
            Key['Interval']: interval,
            Key['MessageSize']: message_size,
            Key['ConnectionTotal']: connection_total,
            Key['GroupCount']: group_count,
            Key['RemainderBegin']: remainder_begin,
            Key['RemainderEnd']: remainder_end,
            Key['Modulo']: modulo,
            Key['GroupConfigMode']: 'Connection'
        }
    }


def send_to_group_group_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                             group_level_remainder_begin, group_level_remainder_end, group_internal_remainder_begin,
                             group_internal_remainder_end, group_internal_modulo):
    return [group_group_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                             group_level_remainder_begin, group_level_remainder_end, group_internal_remainder_begin,
                             group_internal_remainder_end, group_internal_modulo)]


def send_to_group_connection_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                                  remainder_begin, remainder_end, modulo):
    return [group_connection_mode(type_, method, duration, interval, message_size, connection_total, group_count,
                                  remainder_begin, remainder_end, modulo)]


