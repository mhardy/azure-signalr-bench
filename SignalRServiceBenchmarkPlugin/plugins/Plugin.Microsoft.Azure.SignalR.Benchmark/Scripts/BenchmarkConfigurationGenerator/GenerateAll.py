import yaml
import BenchmarkConfigurationSample.Echo as Echo
import BenchmarkConfigurationStep

# echo
type_ = Echo.Config[BenchmarkConfigurationStep.Key['Types']][0]
with open('OutputConfigurations/{}.yaml'.format(type_), 'w') as f:
    f.write(yaml.dump(Echo.Config, Dumper=yaml.Dumper, default_flow_style=False))

# broadcast

# send to client

# send to group

# send to group while members frequently join and leave groups

# mix

