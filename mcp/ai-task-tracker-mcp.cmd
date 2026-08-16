@echo off
setlocal

set "ROOT=%~dp0.."
set "MCP_DLL=%ROOT%\AiTaskTracker.Mcp\bin\Debug\net7.0\AiTaskTracker.Mcp.dll"
set "MCP_PROJECT=%ROOT%\AiTaskTracker.Mcp\AiTaskTracker.Mcp.csproj"

if exist "%MCP_DLL%" (
  dotnet "%MCP_DLL%"
) else (
  dotnet run --project "%MCP_PROJECT%"
)
