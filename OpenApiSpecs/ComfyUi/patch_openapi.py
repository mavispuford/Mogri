import json
import os
import yaml

# Paths
base_dir = os.path.dirname(os.path.abspath(__file__))
input_file = os.path.join(base_dir, 'openapi.yaml')
output_file = os.path.join(base_dir, 'openapi-patched.json')

# Read the original OpenAPI spec
with open(input_file, 'r') as f:
    spec = yaml.safe_load(f)

# Apply patches

# Patch 1: Ensure servers block exists
if 'servers' not in spec:
    spec['servers'] = [{'url': 'https://cloud.comfy.org', 'description': 'Comfy Cloud API'}]

# Patch 2: Fix QueueItem oneOf if present (Kiota struggles with mixed types in arrays)
# The ComfyUI queue item is [number, string, object, object, array] which is hard to type.
# We might need to simplify it to an array of objects or just "object" to let C# handle it as generic JsonElement/Node.

components = spec.get('components', {})
schemas = components.get('schemas', {})

# Patch 3: Relax strict types where Kiota might fail
# For now, we'll see what happens.

# Write the patched spec as JSON
with open(output_file, 'w') as f:
    json.dump(spec, f, indent=2)

print(f"Created patched spec at {output_file}")
