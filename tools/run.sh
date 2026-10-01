#!/usr/bin/env bash
# ==============================================================================
# AgduBugdu Build, Dependency Repair, and Runner Script (Cygwin / MSYS2 / POSIX)
# ==============================================================================

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# Normalize path if running inside Cygwin / MSYS2
if command -v cygpath >/dev/null 2>&1; then
    REPO_ROOT_WIN="$(cygpath -w "$REPO_ROOT")"
else
    REPO_ROOT_WIN="$REPO_ROOT"
fi

CONFIGURATION="Debug"
RUN_TESTS=false
LAUNCH_APP=false
FORCE_RESTORE=false
VERBOSE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --release)
            CONFIGURATION="Release"
            shift
            ;;
        -t|--test|--tests|-RunTests)
            RUN_TESTS=true
            shift
            ;;
        -l|--launch|-LaunchApp)
            LAUNCH_APP=true
            shift
            ;;
        -f|--force-restore|-ForceRestore)
            FORCE_RESTORE=true
            shift
            ;;
        -v|--verbose|-VerboseLogging)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            echo "Usage: ./tools/run.sh [options]"
            echo "Options:"
            echo "  -c, --configuration <Debug|Release>  Build configuration (default: Debug)"
            echo "  --release                            Build in Release configuration"
            echo "  -t, --test, -RunTests                Run test suite after build"
            echo "  -l, --launch, -LaunchApp             Launch AgduBugdu GUI application"
            echo "  -f, --force-restore, -ForceRestore   Force clean package restore"
            echo "  -v, --verbose, -VerboseLogging       Enable verbose dotnet logging"
            echo "  -h, --help                           Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information."
            exit 1
            ;;
    esac
done

echo -e "\033[1;36m========================================\033[0m"
echo -e "\033[1;36m AgduBugdu Build & Dependency Runner    \033[0m"
echo -e "\033[1;36m========================================\033[0m"

# 1. Check .NET SDK prerequisite
echo -e "\n\033[1;33m[1/4] Checking .NET environment...\033[0m"
if ! command -v dotnet >/dev/null 2>&1; then
    echo -e "\033[1;31mERROR: .NET SDK is not installed or not in PATH.\033[0m"
    echo "Please install .NET SDK from https://dotnet.microsoft.com/"
    exit 1
fi

DOTNET_VER="$(dotnet --version)"
echo -e "\033[1;32mFound .NET SDK: $DOTNET_VER\033[0m"

# 2. Check and repair nuget.config
echo -e "\n\033[1;33m[2/4] Validating package sources & dependencies...\033[0m"
NUGET_CONFIG="$REPO_ROOT/nuget.config"
if [ ! -f "$NUGET_CONFIG" ]; then
    echo -e "\033[1;35mCreating missing nuget.config with nuget.org package source...\033[0m"
    cat << 'EOF' > "$NUGET_CONFIG"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF
fi

SOLUTION_FILE="$REPO_ROOT/AgduBugdu.slnx"
if [ ! -f "$SOLUTION_FILE" ]; then
    SOLUTION_FILE="$REPO_ROOT/AgduBugdu.sln"
fi

VERBOSITY="minimal"
if [ "$VERBOSE" = true ]; then
    VERBOSITY="normal"
fi

# 3. Restore dependencies
echo -e "\033[1;36mRestoring NuGet dependencies for $(basename "$SOLUTION_FILE")...\033[0m"
RESTORE_ARGS=("$SOLUTION_FILE" -v "$VERBOSITY")
if [ "$FORCE_RESTORE" = true ]; then
    RESTORE_ARGS+=("--force" "--no-cache")
fi

if ! dotnet restore "${RESTORE_ARGS[@]}"; then
    echo -e "\033[1;33mInitial restore reported issues. Attempting dependency repair with --force...\033[0m"
    if ! dotnet restore "$SOLUTION_FILE" --force -v normal; then
        echo -e "\033[1;31mFailed to restore NuGet packages. Check network connection and sources.\033[0m"
        exit 1
    fi
fi
echo -e "\033[1;32mDependencies verified and restored successfully.\033[0m"

# 4. Build solution
echo -e "\n\033[1;33m[3/4] Building solution ($CONFIGURATION)...\033[0m"
if ! dotnet build "$SOLUTION_FILE" -c "$CONFIGURATION" --no-restore -v "$VERBOSITY"; then
    echo -e "\033[1;31mBuild failed!\033[0m"
    exit 1
fi
echo -e "\033[1;32mBuild succeeded cleanly!\033[0m"

# 5. Optional tests
if [ "$RUN_TESTS" = true ]; then
    echo -e "\n\033[1;33m[Optional] Running test suite...\033[0m"
    if ! dotnet test "$SOLUTION_FILE" -c "$CONFIGURATION" --no-build -v normal --logger "console;verbosity=detailed"; then
        echo -e "\033[1;31mUnit tests failed!\033[0m"
        exit 1
    fi
    echo -e "\033[1;32mAll tests passed!\033[0m"
fi

# 6. Optional launch
if [ "$LAUNCH_APP" = true ]; then
    echo -e "\n\033[1;36m[4/4] Launching AgduBugdu.App...\033[0m"
    APP_EXE="$REPO_ROOT/src/AgduBugdu.App/bin/$CONFIGURATION/net8.0/AgduBugdu.App.exe"
    if [ -f "$APP_EXE" ]; then
        echo -e "\033[1;32mStarting GUI process: $APP_EXE\033[0m"
        if command -v cygstart >/dev/null 2>&1; then
            cygstart "$APP_EXE"
        elif command -v cmd.exe >/dev/null 2>&1; then
            cmd.exe /c start "" "$REPO_ROOT_WIN\\src\\AgduBugdu.App\\bin\\$CONFIGURATION\\net8.0\\AgduBugdu.App.exe"
        else
            "$APP_EXE" &
        fi
        echo -e "\033[1;32mAgduBugdu window launched successfully!\033[0m"
    else
        APP_CSPROJ="$REPO_ROOT/src/AgduBugdu.App/AgduBugdu.App.csproj"
        dotnet run --project "$APP_CSPROJ" -c "$CONFIGURATION" --no-build &
    fi
else
    echo -e "\n\033[0;90mBuild complete. To launch the editor, pass -l / --launch or run: dotnet run --project src/AgduBugdu.App\033[0m"
fi
