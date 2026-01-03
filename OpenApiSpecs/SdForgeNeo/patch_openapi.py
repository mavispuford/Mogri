import json
import os

# Paths
base_dir = os.path.dirname(os.path.abspath(__file__))
input_file = os.path.join(base_dir, 'openapi.json')
output_file = os.path.join(base_dir, 'openapi-patched.json')

# Read the original OpenAPI spec
with open(input_file, 'r') as f:
    spec = json.load(f)

# Apply patches

# 1. Fix Seed type to int64 (long)
schemas = spec.get('components', {}).get('schemas', {})

for schema_name in ['StableDiffusionProcessingTxt2Img', 'StableDiffusionProcessingImg2Img']:
    if schema_name in schemas:
        props = schemas[schema_name].get('properties', {})
        if 'seed' in props:
            props['seed']['format'] = 'int64'
            print(f"Patched {schema_name}.seed format to int64")

# 2. Fix unescaped backslashes in default values
# This iterates through all schemas and properties to find string defaults with backslashes
for schema_name, schema in schemas.items():
    properties = schema.get('properties', {})
    for prop_name, prop_details in properties.items():
        if prop_details.get('type') == 'string' and 'default' in prop_details:
            default_val = prop_details['default']
            if isinstance(default_val, str) and '\\' in default_val:
                # Escape backslashes for C# string literal compatibility
                # If the JSON has "D:\\Path", Kiota writes "D:\Path" which is invalid C#.
                # We want Kiota to write "D:\\Path".
                # We double escape it in the JSON value so it survives generation.
                new_val = default_val.replace('\\', '\\\\')
                prop_details['default'] = new_val
                print(f"Patched default value for {schema_name}.{prop_name}")

# Write the patched spec
with open(output_file, 'w') as f:
    json.dump(spec, f, indent=2)

print(f"Created patched spec at {output_file}")
