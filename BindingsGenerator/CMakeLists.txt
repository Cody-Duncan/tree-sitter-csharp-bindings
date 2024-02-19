cmake_minimum_required(VERSION 3.29)

if(CMAKE_GENERATOR_PLATFORM STREQUAL "x64" OR CMAKE_GENERATOR_PLATFORM STREQUAL "")
  set(CMAKE_GENERATOR_PLATFORM "x64")
  set(DOTNET_GENERATOR_PLATFORM "x64")
elseif(CMAKE_GENERATOR_PLATFORM STREQUAL "Win32")
  set(DOTNET_GENERATOR_PLATFORM "x86")
else()
  message(FATAL_ERROR "Generator platform is set to '${CMAKE_GENERATOR_PLATFORM}', which is not supported by managed projects")
endif()

# Set global DOTNET / CSharp values
set(CMAKE_DOTNET_TARGET_FRAMEWORK "net6.0")
set(CMAKE_DOTNET_SDK "Microsoft.NET.Sdk")
set(CMAKE_CSharp_FLAGS "/platform:${DOTNET_GENERATOR_PLATFORM}")

# Define the BindingsGenerator Project
project(
  BindingsGenerator
  VERSION 0.1.0
  LANGUAGES CSharp)

# Define the C# csproj to build the executable
file(GLOB BindingsGenerator_SRC_FILES "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cs")
add_executable(
  ${PROJECT_NAME}
  "src/Program.cs"
	${BindingsGenerator_SRC_FILES}
)
# Set properties in the csproj file
set_target_properties(
  ${PROJECT_NAME}
  PROPERTIES
  DOTNET_SDK "Microsoft.NET.Sdk"
  DOTNET_TARGET_FRAMEWORK "net6.0"
  VS_GLOBAL_MinimumVisualStudioVersion "14.0"
  VS_GLOBAL_ROOTNAMESPACE ${PROJECT_NAME}
  VS_GLOBAL_Nullable "enable"
  VS_GLOBAL_Platforms ${DOTNET_GENERATOR_PLATFORM}
  VS_GLOBAL_ImplicitUsings "enable"
  VS_PACKAGE_REFERENCES "CppSharp_1.1.5.3168;System.CommandLine_2.0.0-beta4.22272.1"
  COMPILE_OPTIONS "/platform:${DOTNET_GENERATOR_PLATFORM}"
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Generate the launch settings for debugging at ./Properties/launchSettings.json

# get_env_or_default()
# 
# Get the value from the environment variable, otherwise get a default value.
# OUT_VARIABLE_NAME - the name of the variable to assign to.
# ENV_VARIABLE_NAME - the name of the environment variable to retrieve.
# DEFAULT_VALUE - the default value to assign if the environment variable is not defined.
#
# Example: `get_env_or_default(MY_VAR MY_VAR_ENV_VAR "default_value")`
#   If ENV{MY_VAR_ENV_VAR} is defined, its value will be assigned to MY_VAR
#   Otherwise, "default_value" will be assigned to MY_VAR
function(get_env_or_default OUT_VARIABLE_NAME ENV_VARIABLE_NAME DEFAULT_VALUE)
  if(NOT DEFINED ENV{${ENV_VARIABLE_NAME}})
    set(${OUT_VARIABLE_NAME} ${DEFAULT_VALUE})
  else()
    set(${OUT_VARIABLE_NAME} $ENV{${ENV_VARIABLE_NAME}})
  endif()
  return(PROPAGATE ${OUT_VARIABLE_NAME})
endfunction()

set(LAUNCH_SETTINGS_JSON_PATH "${CMAKE_CURRENT_BINARY_DIR}/Properties/launchSettings.json")

cmake_path(GET CMAKE_CURRENT_SOURCE_DIR PARENT_PATH ROOT_BINDINGS_DIR)
get_env_or_default(DEBUG_TREE_SITTER_SRC_PATH IN_DEBUG_TREE_SITTER_SRC_PATH "${ROOT_BINDINGS_DIR}/build/_deps/tree_sitter-src")
get_env_or_default(DEBUG_TREE_SITTER_GRAMMAR_SRC_PATH IN_DEBUG_TREE_SITTER_GRAMMAR_SRC_PATH "${ROOT_BINDINGS_DIR}/build/_deps/tree_sitter_python-src")
get_env_or_default(DEBUG_CSHARP_SOURCE_OUTDIR IN_DEBUG_CSHARP_SOURCE_OUTDIR "${ROOT_BINDINGS_DIR}/GeneratedCSharpBindings")
get_env_or_default(DEBUG_C_SOURCE_OUTDIR IN_DEBUG_C_SOURCE_OUTDIR "${ROOT_BINDINGS_DIR}/GeneratedCHeaders")
get_env_or_default(DEBUG_CSHARP_TEST_PROGRAM_SOURCE_OUTDIR IN_CSHARP_TEST_PROGRAM_SOURCE_OUTDIR "${ROOT_BINDINGS_DIR}/GeneratedCSharpTestProgram")
set(IN_DEBUG_DEBUG_COMMAND_ARGS "--TreeSitterRepoPath \\\"${DEBUG_TREE_SITTER_SRC_PATH}\\\" --TreeSitterGrammarPaths \\\"${DEBUG_TREE_SITTER_GRAMMAR_SRC_PATH}\\\" --CSharpBindingsOutputPath \\\"${DEBUG_CSHARP_SOURCE_OUTDIR}\\\" --CGeneratedHeadersOutputPath \\\"${DEBUG_C_SOURCE_OUTDIR}\\\" --CSharpTestProgramOutputPath \\\"${DEBUG_CSHARP_TEST_PROGRAM_SOURCE_OUTDIR}\\\"")

set(DEBUG_COMMAND_LINE_ARG_CONFIG "{}")
string(JSON DEBUG_COMMAND_LINE_ARG_CONFIG SET "{}" "profiles" "{}")
string(JSON DEBUG_COMMAND_LINE_ARG_CONFIG SET ${DEBUG_COMMAND_LINE_ARG_CONFIG} "profiles" "${PROJECT_NAME}" "{}")
string(JSON DEBUG_COMMAND_LINE_ARG_CONFIG SET ${DEBUG_COMMAND_LINE_ARG_CONFIG} "profiles" "${PROJECT_NAME}" "commandName" "\"Project\"")
string(JSON DEBUG_COMMAND_LINE_ARG_CONFIG SET ${DEBUG_COMMAND_LINE_ARG_CONFIG} "profiles" "${PROJECT_NAME}" "commandLineArgs" "\"${IN_DEBUG_DEBUG_COMMAND_ARGS}\"")
file(WRITE ${LAUNCH_SETTINGS_JSON_PATH} ${DEBUG_COMMAND_LINE_ARG_CONFIG})


