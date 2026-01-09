import onnx
from onnxruntime.quantization import quantize_dynamic, QuantType

input_model_path = "lama_fp32.onnx"
output_model_path = "lama_int8.onnx"

# Quantize the model (Dynamic quantization is usually best for mobile CPU/NNAPI)
quantize_dynamic(
    input_model_path,
    output_model_path,
    weight_type=QuantType.QUInt8  # Use QUInt8 for unsigned 8-bit integers (smaller, compatible)
)

print(f"Quantization complete! Saved to {output_model_path}")