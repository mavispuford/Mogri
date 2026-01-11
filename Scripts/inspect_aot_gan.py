import onnxruntime as ort
import sys
import os

def inspect_model(model_path):
    if not os.path.exists(model_path):
        print(f"Error: Model not found at {model_path}")
        return

    try:
        session = ort.InferenceSession(model_path)
    except Exception as e:
        print(f"Error loading model: {e}")
        return

    print("===== Model Inputs =====")
    for input_meta in session.get_inputs():
        print(f"Name: {input_meta.name}")
        print(f"Shape: {input_meta.shape}")
        print(f"Type: {input_meta.type}")
        print("------------------------")

    print("\n===== Model Outputs =====")
    for output_meta in session.get_outputs():
        print(f"Name: {output_meta.name}")
        print(f"Shape: {output_meta.shape}")
        print(f"Type: {output_meta.type}")
        print("------------------------")

if __name__ == "__main__":
    # Default to the expected path in the repo
    default_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), 
                               "MobileDiffusion", "Resources", "Raw", "aot_gan.onnx")
    
    inspect_model(default_path)
