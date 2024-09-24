using System.Reflection.Emit;
using UnityEngine.SceneManagement;

namespace UnityExplorer.ObjectExplorer
{
    public enum SearchContext
    {
        UnityObject,
        Singleton,
        Class,
        Method,
        // String
    }

    public enum ChildFilter
    {
        Any,
        RootObject,
        HasParent
    }

    public enum SceneFilter
    {
        Any,
        ActivelyLoaded,
        DontDestroyOnLoad,
        HideAndDontSave,
    }

    public static class SearchProvider
    {
        private static bool Filter(Scene scene, SceneFilter filter)
        {
            return filter switch
            {
                SceneFilter.Any => true,
                SceneFilter.DontDestroyOnLoad => scene.handle == -12,
                SceneFilter.HideAndDontSave => scene == default,
                SceneFilter.ActivelyLoaded => scene.buildIndex != -1,
                _ => false,
            };
        }

        internal static List<ObjectSearch.SearchedObject> UnityObjectSearch(string input, string customTypeInput, ChildFilter childFilter, SceneFilter sceneFilter)
        {
            List<ObjectSearch.SearchedObject> results = new();

            Type searchType = null;
            if (!string.IsNullOrEmpty(customTypeInput))
            {
                if (ReflectionUtility.GetTypeByName(customTypeInput) is Type customType)
                {
                    if (typeof(UnityEngine.Object).IsAssignableFrom(customType))
                        searchType = customType;
                    else
                        ExplorerCore.LogWarning($"Custom type '{customType.FullName}' is not assignable from UnityEngine.Object!");
                }
                else
                    ExplorerCore.LogWarning($"Could not find any type by name '{customTypeInput}'!");
            }

            if (searchType == null)
                searchType = typeof(UnityEngine.Object);

            UnityEngine.Object[] allObjects = RuntimeHelper.FindObjectsOfTypeAll(searchType);

            // perform filter comparers

            string nameFilter = null;
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            bool shouldFilterGOs = searchType == typeof(GameObject) || typeof(Component).IsAssignableFrom(searchType);

            foreach (UnityEngine.Object obj in allObjects)
            {
                // name check
                if (!string.IsNullOrEmpty(nameFilter) && !obj.name.ContainsIgnoreCase(nameFilter))
                    continue;

                GameObject go = null;
                Type type = obj.GetActualType();

                if (type == typeof(GameObject))
                    go = obj.TryCast<GameObject>();
                else if (typeof(Component).IsAssignableFrom(type))
                    go = obj.TryCast<Component>()?.gameObject;

                if (go)
                {
                    // hide unityexplorer objects
                    if (go.transform.root.name == "UniverseLibCanvas")
                        continue;

                    if (shouldFilterGOs)
                    {
                        // scene check
                        if (sceneFilter != SceneFilter.Any)
                        {
                            if (!Filter(go.scene, sceneFilter))
                                continue;
                        }

                        if (childFilter != ChildFilter.Any)
                        {
                            if (!go)
                                continue;

                            // root object check (no parent)
                            if (childFilter == ChildFilter.HasParent && !go.transform.parent)
                                continue;
                            else if (childFilter == ChildFilter.RootObject && go.transform.parent)
                                continue;
                        }
                    }
                }

                results.Add(new ObjectSearch.SearchedObject(obj));
            }

            return results;
        }

        internal static List<ObjectSearch.SearchedObject> ClassSearch(string input)
        {
            List<ObjectSearch.SearchedObject> list = new();

            string nameFilter = "";
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in asm.GetTypes())
                {
                    if (!string.IsNullOrEmpty(nameFilter) && !type.FullName.ContainsIgnoreCase(nameFilter))
                        continue;
                    list.Add(new ObjectSearch.SearchedObject(type));
                }
            }

            return list;
        }
        
        internal static List<ObjectSearch.SearchedObject> MethodSearch(string methodName)
        {
            List<MethodInfo> methods = new();
            List<ObjectSearch.SearchedObject> list = new();

            List<string> nameFilter = new List<string>();
            if (!string.IsNullOrEmpty(methodName))
                nameFilter = methodName.Split(' ').ToList();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in asm.GetTypes())
                {
                    var foundMethodName = string.Empty;
                    if (nameFilter.Count > 0)
                    {
                        var firstFound = type.GetMethods(BindingFlags.Instance |
                                                         BindingFlags.Static |
                                                         BindingFlags.Public |
                                                         BindingFlags.NonPublic)
                            .FirstOrDefault(method => nameFilter.All(f => method.Name.ContainsIgnoreCase(f)));
                    
                        if (!type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                .Any(method => nameFilter.All(f => method.Name.ContainsIgnoreCase(f))))
                        {
                            continue;
                        }
                        
                        foundMethodName = firstFound?.Name ?? string.Empty;
                    }

                    // Add methods with the specified name
                    // methods.AddRange(type.GetMethods().Where(method => method.Name.ContainsIgnoreCase(nameFilter)));
                    list.Add(new ObjectSearch.SearchedObject(type, foundMethodName));
                }
            }

            return list;
        }
        
        internal static List<ObjectSearch.SearchedObject> StringSearch(string searchString)
        {
            // Initialize the lookup tables for single and multi-byte opcodes
            var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                var opCode = (OpCode)field.GetValue(null);
                if (opCode.Size == 1)
                    SingleByteOpCodes[opCode.Value] = opCode;
                else
                    MultiByteOpCodes[opCode.Value & 0xff] = opCode;
            }
            
            List<ObjectSearch.SearchedObject> list = new();
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var methodBody = method.GetMethodBody();
                            if (methodBody != null)
                            {
                                var il = methodBody.GetILAsByteArray();
                                var module = method.Module;

                                int position = 0;
                                while (position < il.Length)
                                {
                                    OpCode code;
                                    ushort value = il[position];

                                    // Handle multi-byte opcodes
                                    if (value != OpCodes.Prefix1.Value)
                                    {
                                        code = SingleByteOpCodes[value];
                                        position++;
                                    }
                                    else
                                    {
                                        value = BitConverter.ToUInt16(il, position);
                                        code = MultiByteOpCodes[value];
                                        position += 2;
                                    }

                                    // Look for Ldstr (which loads a string literal)
                                    if (code == OpCodes.Ldstr)
                                    {
                                        int metadataToken = BitConverter.ToInt32(il, position);
                                        position += 4;

                                        try
                                        {
                                            string literal = module.ResolveString(metadataToken);

                                            if (literal.Contains(searchString))
                                            {
                                                Console.WriteLine($"Found string literal '{searchString}' in method '{method.Name}' of type '{type.FullName}'");
                                            }
                                        }
                                        catch (ArgumentException ex)
                                        {
                                            // Handle invalid tokens (this shouldn't happen if we're correctly identifying Ldstr)
                                            Console.WriteLine($"Error resolving string: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        // Handle other opcodes
                                        position += GetOperandSize(code);
                                    }
                                }
                            }
                        }
                    }
                }

                //     foreach (var type in assembly.GetTypes())
                //     {
                //         if (type.ContainsGenericParameters)
                //             continue;
                //         
                //         // Search static fields (constants and field initializers)
                //         foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                //         {
                //             if (field.FieldType == typeof(string))
                //             {
                //                 var fieldValue = field.GetValue(null) as string; // Static fields, so no instance needed
                //                 if (fieldValue != null && fieldValue.Contains(searchString))
                //                 {
                //                     Console.WriteLine($"Found '{searchString}' in static field '{field.Name}' of type '{type.FullName}'");
                //                     list.Add(new ObjectSearch.SearchedObject(type, fieldValue));
                //                 }
                //             }
                //         }
                //
                //         // Search static properties
                //         foreach (var prop in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                //         {
                //             if (prop.PropertyType.ContainsGenericParameters)
                //                 continue;
                //
                //             if (prop.PropertyType == typeof(string) && prop.CanRead)
                //             {
                //                 var propValue = (string)prop.GetValue(null, null); // Static properties, no instance needed
                //                 if (propValue != null && propValue.Contains(searchString))
                //                 {
                //                     Console.WriteLine($"Found '{searchString}' in static property '{prop.Name}' of type '{type.FullName}'");
                //                     list.Add(new ObjectSearch.SearchedObject(type, propValue));
                //                 }
                //             }
                //         }
                //     }
                // }
            }
            catch (Exception e)
            {
                list.Add(new ObjectSearch.SearchedObject(e, e.Message + e.StackTrace));
            }
            
            return list;
        }
        
        // Helper function to determine the operand size for different opcodes
        private static int GetOperandSize(OpCode opCode)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineString:
                case OperandType.InlineI:
                case OperandType.InlineSwitch:
                case OperandType.InlineR:
                    return 4;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineR:
                case OperandType.ShortInlineVar:
                    return 1;

                case OperandType.InlineI8:
                    return 8;

                default:
                    return 0;
            }
        }
        

        // Opcode lookup tables for single and multi-byte opcodes
        private static readonly OpCode[] SingleByteOpCodes = new OpCode[256];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[256];

        internal static string[] instanceNames = new string[]
        {
            "m_instance",
            "m_Instance",
            "s_instance",
            "s_Instance",
            "_instance",
            "_Instance",
            "instance",
            "Instance",
            "<Instance>k__BackingField",
            "<instance>k__BackingField",
        };

        internal static List<ObjectSearch.SearchedObject> InstanceSearch(string input)
        {
            List<object> instances = new();

            string nameFilter = "";
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Search all non-static, non-enum classes.
                foreach (Type type in asm.GetTypes().Where(it => !(it.IsSealed && it.IsAbstract) && !it.IsEnum))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(nameFilter) && !type.FullName.ContainsIgnoreCase(nameFilter))
                            continue;

                        ReflectionUtility.FindSingleton(instanceNames, type, flags, instances);
                    }
                    catch { }
                }
            }

            return instances.Select(i => new ObjectSearch.SearchedObject(i)).ToList();
        }

    }
}
