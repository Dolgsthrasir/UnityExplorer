using HarmonyLib;
using Mono.CSharp;
using System.Text;
using UnityExplorer.CSConsole;

namespace UnityExplorer.Hooks
{
    public class HookInstance
    {
        // Static 

        static readonly StringBuilder evaluatorOutput;
        static readonly ScriptEvaluator scriptEvaluator = new(new StringWriter(evaluatorOutput = new StringBuilder()));

        static HookInstance()
        {
            scriptEvaluator.Run("using System;");
            scriptEvaluator.Run("using System.Text;");
            scriptEvaluator.Run("using System.Reflection;");
            scriptEvaluator.Run("using System.Collections;");
            scriptEvaluator.Run("using System.Collections.Generic;");
        }

        // Instance

        public bool Enabled;
        public bool StartUp;

        public MethodInfo TargetMethod;
        public string PatchSourceCode;

        readonly string signature;
        PatchProcessor patchProcessor;

        MethodInfo postfix;
        MethodInfo prefix;
        MethodInfo finalizer;
        MethodInfo transpiler;

        public HookInstance(MethodInfo targetMethod)
        {
            this.TargetMethod = targetMethod;
            this.signature = this.TargetMethod.FullDescription();

            this.GenerateDefaultPatchSourceCode(targetMethod);

            if (this.CompileAndGenerateProcessor(this.PatchSourceCode)) this.Patch();
        }
        
        public HookInstance(MethodInfo targetMethod, string code)
        {
            this.TargetMethod = targetMethod;
            this.signature = this.TargetMethod.FullDescription();

            this.PatchSourceCode = code;

            if (this.CompileAndGenerateProcessor(this.PatchSourceCode))
            {
                this.Patch();
            }
        }

        // Evaluator.source_file 
        private static readonly FieldInfo fi_sourceFile = AccessTools.Field(typeof(Evaluator), "source_file");
        // TypeDefinition.Definition
        private static readonly PropertyInfo pi_Definition = AccessTools.Property(typeof(TypeDefinition), "Definition");

        public bool CompileAndGenerateProcessor(string patchSource)
        {
            this.Unpatch();

            StringBuilder codeBuilder = new();

            try
            {
                this.patchProcessor = ExplorerCore.Harmony.CreateProcessor(this.TargetMethod);

                // Dynamically compile the patch method

                codeBuilder.AppendLine($"static class DynamicPatch_{DateTime.Now.Ticks}");
                codeBuilder.AppendLine("{");
                codeBuilder.AppendLine(patchSource);
                codeBuilder.AppendLine("}");

                scriptEvaluator.Run(codeBuilder.ToString());

                if (ScriptEvaluator._reportPrinter.ErrorsCount > 0)
                    throw new FormatException($"Unable to compile the generated patch!");

                // TODO: Publicize MCS to avoid this reflection
                // Get the most recent Patch type in the source file
                TypeContainer typeContainer = ((CompilationSourceFile)fi_sourceFile.GetValue(scriptEvaluator))
                    .Containers
                    .Last(it => it.MemberName.Name.StartsWith("DynamicPatch_"));
                // Get the TypeSpec from the TypeDefinition, then get its "MetaInfo" (System.Type)
                Type patchClass = ((TypeSpec)pi_Definition.GetValue((Class)typeContainer, null)).GetMetaInfo();

                // Create the harmony patches as defined

                this.postfix = patchClass.GetMethod("Postfix", ReflectionUtility.FLAGS);
                if (this.postfix != null) this.patchProcessor.AddPostfix(new HarmonyMethod(this.postfix));

                this.prefix = patchClass.GetMethod("Prefix", ReflectionUtility.FLAGS);
                if (this.prefix != null) this.patchProcessor.AddPrefix(new HarmonyMethod(this.prefix));

                this.finalizer = patchClass.GetMethod("Finalizer", ReflectionUtility.FLAGS);
                if (this.finalizer != null) this.patchProcessor.AddFinalizer(new HarmonyMethod(this.finalizer));

                this.transpiler = patchClass.GetMethod("Transpiler", ReflectionUtility.FLAGS);
                if (this.transpiler != null) this.patchProcessor.AddTranspiler(new HarmonyMethod(this.transpiler));

                return true;
            }
            catch (Exception ex)
            {
                if (ex is FormatException)
                {
                    string output = scriptEvaluator._textWriter.ToString();
                    string[] outputSplit = output.Split('\n');
                    if (outputSplit.Length >= 2)
                        output = outputSplit[outputSplit.Length - 2];
                    evaluatorOutput.Clear();

                    if (ScriptEvaluator._reportPrinter.ErrorsCount > 0)
                    {
                        ExplorerCore.LogWarning($"Unable to compile the code. Evaluator's last output was:\r\n{output}");
                    }
                    else
                    {
                        ExplorerCore.LogWarning($"Exception generating patch source code: {ex}");
                    }
                }
                else
                {
                    ExplorerCore.LogWarning($"Exception generating patch source code: {ex}");
                }

                // ExplorerCore.Log(codeBuilder.ToString());

                return false;
            }
        }

        static string FullDescriptionClean(Type type)
        {
            string description = type.FullDescription().Replace("+", ".");
            if (description.EndsWith("&"))
                description = $"ref {description.Substring(0, description.Length - 1)}";
            return description;
        }

        private string GenerateDefaultPatchSourceCode(MethodInfo targetMethod)
        {
            StringBuilder codeBuilder = new();

            codeBuilder.Append("static void Postfix(");

            bool isStatic = targetMethod.IsStatic;

            List<string> arguments = new();

            if (!isStatic)
                arguments.Add($"{FullDescriptionClean(targetMethod.DeclaringType)} __instance");

            if (targetMethod.ReturnType != typeof(void))
                arguments.Add($"{FullDescriptionClean(targetMethod.ReturnType)} __result");

            ParameterInfo[] parameters = targetMethod.GetParameters();

            int paramIdx = 0;
            foreach (ParameterInfo param in parameters)
            {
                arguments.Add($"{FullDescriptionClean(param.ParameterType)} __{paramIdx}");
                paramIdx++;
            }

            codeBuilder.Append(string.Join(", ", arguments.ToArray()));

            codeBuilder.Append(")\n");

            // Patch body

            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine("    try {");
            codeBuilder.AppendLine("       StringBuilder sb = new StringBuilder();");
            codeBuilder.AppendLine($"       sb.AppendLine(\"--------------------\");");
            codeBuilder.AppendLine($"       sb.AppendLine(\"{this.signature}\");");

            if (!targetMethod.IsStatic)
                codeBuilder.AppendLine($"       sb.Append(\"- __instance: \").AppendLine(__instance.ToString());");

            paramIdx = 0;
            foreach (ParameterInfo param in parameters)
            {
                codeBuilder.Append($"       sb.Append(\"- Parameter {paramIdx} '{param.Name}': \")");

                Type pType = param.ParameterType;
                if (pType.IsByRef) pType = pType.GetElementType();
                if (pType.IsValueType)
                    codeBuilder.AppendLine($".AppendLine(__{paramIdx}.ToString());");
                else
                    codeBuilder.AppendLine($".AppendLine(__{paramIdx}?.ToString() ?? \"null\");");

                paramIdx++;
            }

            if (targetMethod.ReturnType != typeof(void))
            {
                codeBuilder.Append("       sb.Append(\"- Return value: \")");
                if (targetMethod.ReturnType.IsValueType)
                    codeBuilder.AppendLine(".AppendLine(__result.ToString());");
                else
                    codeBuilder.AppendLine(".AppendLine(__result?.ToString() ?? \"null\");");
            }

            codeBuilder.AppendLine($"       UnityExplorer.ExplorerCore.Log(sb.ToString());");
            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("    catch (System.Exception ex) {");
            codeBuilder.AppendLine($"        UnityExplorer.ExplorerCore.LogWarning($\"Exception in patch of {this.signature}:\\n{{ex}}\");");
            codeBuilder.AppendLine("    }");

            codeBuilder.AppendLine("}");

            return this.PatchSourceCode = codeBuilder.ToString();
        }

        public void TogglePatch()
        {
            if (!this.Enabled)
                this.Patch();
            else
                this.Unpatch();
        }
        
        public void Startup()
        {
            if (!this.Enabled)
                this.EnableStartup();
            else
                this.DisableStartup();
        }
        
        public void EnableStartup()
        {
            try
            {
                HookCreator.SaveStartup(this, true);
                this.StartUp = true;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception hooking method!\r\n{ex}");
            }
        }

        public void DisableStartup()
        {
            try
            {
                HookCreator.SaveStartup(this, false);
                this.StartUp = false;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception unpatching method: {ex}");
            }
        }

        public void Patch()
        {
            try
            {
                this.patchProcessor.Patch();

                this.Enabled = true;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception hooking method!\r\n{ex}");
            }
        }

        public void Unpatch()
        {
            try
            {
                if (this.prefix != null) this.patchProcessor.Unpatch(this.prefix);
                if (this.postfix != null) this.patchProcessor.Unpatch(this.postfix);
                if (this.finalizer != null) this.patchProcessor.Unpatch(this.finalizer);
                if (this.transpiler != null) this.patchProcessor.Unpatch(this.transpiler);

                this.Enabled = false;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception unpatching method: {ex}");
            }
        }
    }
}
