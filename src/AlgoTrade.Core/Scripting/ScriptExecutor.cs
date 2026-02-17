using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
        /// Pre-process #load directives: resolve and inline referenced .csx files.
        /// All using statements are collected and moved to the top of the final code.
        /// </summary>
        private string PreProcessLoadDirectives(string code, string? sourceDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory))
                return code;

            // Match #load "filename.csx" directives
            var loadRegex = new Regex(@"^\s*#load\s+""([^""]+)""", RegexOptions.Multiline);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string ProcessCode(string source, string baseDir)
            {
                return loadRegex.Replace(source, match =>
                {
                    var fileName = match.Groups[1].Value;
                    var fullPath = Path.GetFullPath(Path.Combine(baseDir, fileName));

                    if (processedFiles.Contains(fullPath))
                        return $"// #load \"{fileName}\" (already loaded)";

                    if (!File.Exists(fullPath))
                        return $"// #load \"{fileName}\" (file not found: {fullPath})";

                    processedFiles.Add(fullPath);
                    var loadedCode = File.ReadAllText(fullPath);
                    var loadedDir = Path.GetDirectoryName(fullPath)!;

                    // Recursive: process #load in loaded files too
                    loadedCode = ProcessCode(loadedCode, loadedDir);

                    return $"// === #load \"{fileName}\" - beg ===\n{loadedCode}\n// === #load \"{fileName}\" - end ===";
                });
            }

            var inlinedCode = ProcessCode(code, sourceDirectory);

            // Collect all using statements and move to top
            var usingRegex = new Regex(@"^\s*using\s+[^(][^;]+;\s*$", RegexOptions.Multiline);
            var usings = new HashSet<string>();
            var codeWithoutUsings = usingRegex.Replace(inlinedCode, match =>
            {
                usings.Add(match.Value.Trim());
                return ""; // remove from original position
            });

            if (usings.Count > 0)
            {
                var usingBlock = string.Join("\n", usings.OrderBy(u => u));
                return usingBlock + "\n\n" + codeWithoutUsings;
            }

            return inlinedCode;
        }

        /// <summary>
        /// Compile a script without executing it. Returns compilation errors if any.
        /// sourceDirectory: script dosyasının bulunduğu dizin (#load direktifleri için)
        /// </summary>
        public ScriptExecutionResult CompileScript(string code, string? sourceDirectory = null)
        {
            var result = new ScriptExecutionResult();

            try
            {
                // Pre-process #load directives
                code = PreProcessLoadDirectives(code, sourceDirectory);

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
        /// sourceDirectory: script dosyasının bulunduğu dizin (#load direktifleri için)
        /// </summary>
        public async Task<ScriptExecutionResult> ExecuteAsync(
            string code,
            ScriptGlobals globals,
            CancellationToken cancellationToken = default,
            string? sourceDirectory = null)
        {
            var compileResult = CompileScript(code, sourceDirectory);
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
