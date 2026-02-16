using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// Executes C# scripts with access to AlgoTrader and project classes using Roslyn
    /// </summary>
    public class ScriptExecutor
    {
        private readonly ScriptOptions _scriptOptions;
        private CancellationTokenSource? _cancellationTokenSource;
        private Script<object>? _compiledScript;

        public ScriptExecutor()
        {
            // Configure script options with all necessary references and imports
            _scriptOptions = ScriptOptions.Default
                // Add assembly references
                .AddReferences(
                    typeof(object).Assembly,                    // mscorlib/System.Private.CoreLib
                    typeof(Console).Assembly,                   // System.Console
                    typeof(Enumerable).Assembly,                // System.Linq
                    typeof(List<>).Assembly,                    // System.Collections
                    typeof(Task).Assembly,                      // System.Threading.Tasks
                    Assembly.GetExecutingAssembly()             // This project (all classes)
                )
                // Add default imports so scripts don't need using statements
                .AddImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Threading.Tasks",
                    "AlgoTrade.Core",
                    "AlgoTrade.Core.Trading",
                    "AlgoTrade.Core.Trading.Core",
                    "AlgoTrade.Core.Trading.Strategies",
                    "AlgoTrade.Core.Trading.Strategy",
                    "AlgoTrade.Core.Trading.Indicators",
                    "AlgoTrade.Core.Trading.Queries",
                    "AlgoTrade.Core.Trading.Query",
                    "AlgoTrade.Core.StockDataReader",
                    "AlgoTrade.Core.Logging",
                    "AlgoTrade.Core.Scripting"
                );
        }

        /// <summary>
        /// Compile a script without executing it. Returns compilation errors if any.
        /// </summary>
        public ScriptExecutionResult CompileScript(string code)
        {
            var result = new ScriptExecutionResult();

            try
            {
                _compiledScript = CSharpScript.Create<object>(
                    code,
                    _scriptOptions,
                    globalsType: typeof(ScriptGlobals)
                );

                var diagnostics = _compiledScript.Compile();

                if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                {
                    result.Success = false;
                    result.CompilationErrors = diagnostics
                        .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        .Select(d => d.ToString())
                        .ToList();
                    _compiledScript = null;
                }
                else
                {
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                _compiledScript = null;
            }

            return result;
        }

        /// <summary>
        /// Run a previously compiled script. Call CompileScript() first.
        /// </summary>
        public async Task<ScriptExecutionResult> RunCompiledAsync(
            ScriptGlobals globals,
            CancellationToken cancellationToken = default)
        {
            var result = new ScriptExecutionResult();
            var startTime = DateTime.Now;

            if (_compiledScript == null)
            {
                result.Success = false;
                result.Error = "No compiled script available. Call CompileScript() first.";
                return result;
            }

            try
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var scriptState = await _compiledScript.RunAsync(globals, _cancellationTokenSource.Token);

                result.Success = true;
                result.ReturnValue = scriptState.ReturnValue;
                result.ExecutionTime = DateTime.Now - startTime;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Error = "Script execution was cancelled.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                result.StackTrace = ex.StackTrace;
            }
            finally
            {
                result.ExecutionTime = DateTime.Now - startTime;
            }

            return result;
        }

        /// <summary>
        /// Compile and run a script in one call (convenience method)
        /// </summary>
        public async Task<ScriptExecutionResult> ExecuteAsync(
            string code,
            ScriptGlobals globals,
            CancellationToken cancellationToken = default)
        {
            var compileResult = CompileScript(code);
            if (!compileResult.Success)
                return compileResult;

            return await RunCompiledAsync(globals, cancellationToken);
        }

        /// <summary>
        /// Cancel any running script
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }
    }

    /// <summary>
    /// Result of script execution
    /// </summary>
    public class ScriptExecutionResult
    {
        public bool Success { get; set; }
        public object? ReturnValue { get; set; }
        public string? Error { get; set; }
        public string? StackTrace { get; set; }
        public List<string>? CompilationErrors { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}
