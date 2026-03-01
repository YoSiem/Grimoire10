using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Archimedes;
using Archimedes.Enums;
using Grimoire.Utilities;

using MoonSharp.Interpreter;

using Serilog;
using Serilog.Events;

namespace Grimoire.Structures
{
    public sealed class StructureManager : IEnumerable<StructureObject>
    {
        private static readonly Lazy<StructureManager> instance = new(() => new StructureManager());

        private string structDir = string.Empty;

        private readonly List<StructureObject> structures = new();

        public List<float> AvailableEpics { get; } = new();

        public static StructureManager Instance => instance.Value;

        private StructureManager() { }

        /// <summary>
        /// Get a new instance of the structure object bearing the given name.
        /// </summary>
        /// <param name="name">Name of the struct to be generated</param>
        /// <returns>Partially initialized (schema only) unique structure object</returns>
        public async Task<StructureObject> GetStruct(string name)
        {
            StructureObject schema = structures.Find(s => s.StructName == name);
            if (schema == null)
            {
                LogUtility.MessageBoxAndLog("Failed to get the target structure object!", "GetStruct Exception", LogEventLevel.Error);
                return null;
            }

            StructureObject structObj = schema.Clone() as StructureObject;

            if (structObj == null)
            {
                LogUtility.MessageBoxAndLog("Failed to get the target structure object!", "GetStruct Exception", LogEventLevel.Error);

                return null;
            }

            await Task.Run(() => structObj.ParseScript(ParseFlags.Structure));

            return structObj;
        }

        public void Load(string directory = null)
        {
            structures.Clear();

            if (!string.IsNullOrWhiteSpace(directory))
                structDir = directory;

            if (string.IsNullOrWhiteSpace(structDir) || !Directory.Exists(structDir))
            {
                Log.Error($"Structures directory is invalid: {structDir}");
                return;
            }

            foreach (string filename in Directory.GetFiles(structDir))
            {
                StructureObject structObj = new StructureObject(filename, false);

                try
                {
                    structObj.ParseScript(ParseFlags.Info);

                    structures.Add(structObj);
                }
                catch (Exception ex)
                {
                    if (ex is SyntaxErrorException || ex is ScriptRuntimeException)
                        LogUtility.MessageBoxAndLog($"An exception occured while processing: {Path.GetFileNameWithoutExtension(filename)}\n\n{StringExt.LuaExceptionToString(((InterpreterException)ex).DecoratedMessage)}", "StructureManager Exception", LogEventLevel.Error);

                    return;
                }            
            }

            Log.Information($"{structures.Count} structures loaded.");
        }

        public IEnumerator<StructureObject> GetEnumerator() => structures.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
