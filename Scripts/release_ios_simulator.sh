#!/bin/bash

# --- Path Logic ---
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# --- Configuration ---
PROJECT_NAME="Mogri"
PROJECT_PATH="$ROOT_DIR/$PROJECT_NAME/$PROJECT_NAME.csproj"
BUNDLE_ID="com.mavispuford.mogri"
FRAMEWORK="net10.0-ios"
CONFIGURATION="Release"

# Detect Architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RID="iossimulator-arm64"
else
    RID="iossimulator-x64"
fi

echo "🚀 Starting local build for $PROJECT_NAME ($RID)..."

# 1. Clean up old builds
echo "🧹 Cleaning previous builds..."
rm -rf "$ROOT_DIR/$PROJECT_NAME/bin/$CONFIGURATION/$FRAMEWORK/$RID/"
dotnet clean "$PROJECT_PATH"

# 2. Run Dotnet Build (instead of publish)
# We use build because publish enforces physical device architectures.
echo "🔨 Building Release version for Simulator..."
dotnet build "$PROJECT_PATH" \
    -f $FRAMEWORK \
    -c $CONFIGURATION \
    -r $RID \
    -p:BuildIpa=false

# 3. Check if build succeeded
if [ $? -ne 0 ]; then
    echo "❌ Build failed. Check the logs above."
    exit 1
fi

# 4. Locate the .app bundle
# Build puts it in the root of the RID folder, not a 'publish' subfolder
APP_PATH="$ROOT_DIR/$PROJECT_NAME/bin/$CONFIGURATION/$FRAMEWORK/$RID/$PROJECT_NAME.app"

if [ ! -d "$APP_PATH" ]; then
    echo "🔍 Searching for .app bundle..."
    APP_PATH=$(find "$ROOT_DIR/$PROJECT_NAME/bin/$CONFIGURATION/$FRAMEWORK/$RID" -name "$PROJECT_NAME.app" -type d | head -n 1)
fi

# 5. Install to Simulator
if [ -z "$APP_PATH" ] || [ ! -d "$APP_PATH" ]; then
    echo "❌ Could not find the .app bundle at $APP_PATH"
    exit 1
fi

echo "📲 Installing $PROJECT_NAME.app to the booted simulator..."
xcrun simctl install booted "$APP_PATH"

if [ $? -eq 0 ]; then
    echo "✅ Success! Launching $BUNDLE_ID..."
    xcrun simctl launch booted "$BUNDLE_ID"
else
    echo "❌ Installation failed. Ensure a simulator is booted (open -a Simulator)."
    exit 1
fi