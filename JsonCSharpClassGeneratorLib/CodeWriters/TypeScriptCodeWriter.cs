using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamasoft.JsonClassGenerator.CodeWriters
{
    public class TypeScriptCodeWriter : ICodeWriter
    {
        public string FileExtension
        {
            get { return ".ts"; }
        }

        public string DisplayName
        {
            get { return "TypeScript"; }
        }

        public string GetTypeName(JsonType type, IJsonClassGeneratorConfig config)
        {
            switch (type.Type)
            {
                case JsonTypeEnum.Anything: return "any";
                case JsonTypeEnum.String: return "string";
                case JsonTypeEnum.Boolean: return "boolean";
                case JsonTypeEnum.Integer:
                case JsonTypeEnum.Long:
                case JsonTypeEnum.Float: return "number";
                case JsonTypeEnum.Date: return "Date";
                case JsonTypeEnum.NullableInteger:
                case JsonTypeEnum.NullableLong:
                case JsonTypeEnum.NullableFloat: return "number";
                case JsonTypeEnum.NullableBoolean: return "boolean";
                case JsonTypeEnum.NullableDate: return "Date";
                case JsonTypeEnum.Object: return type.AssignedName;
                case JsonTypeEnum.Array: return "Array<" + GetTypeName(type.InternalType, config) + ">";
                case JsonTypeEnum.Dictionary: return "{ [key: string]: " + GetTypeName(type.InternalType, config) + "; }";
                case JsonTypeEnum.NullableSomething: return "any";
                case JsonTypeEnum.NonConstrained: return "any";
                default: throw new NotSupportedException("Unsupported type");
            }
        }

        public void WriteClass(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type)
        {
            var prefix = GetNamespace(config, type.IsRoot) != null ? "    " : "";
           
            var exported = !config.InternalVisibility || config.SecondaryNamespace != null;
            sw.WriteLine(prefix + (exported ? "export " : string.Empty) + "class " + type.AssignedName + " {");

            //Class properties
            foreach (var field in type.Fields)
            {
                var shouldDefineNamespace = type.IsRoot && config.SecondaryNamespace != null && config.Namespace != null && (field.Type.Type == JsonTypeEnum.Object || (field.Type.InternalType != null && field.Type.InternalType.Type == JsonTypeEnum.Object));
                if (config.ExamplesInDocumentation)
                {
                    sw.WriteLine();
                    sw.WriteLine(prefix + "    /**");
                    sw.WriteLine(prefix + "      * Examples: " + field.GetExamplesText());
                    sw.WriteLine(prefix + "      */");
                }
                sw.WriteLine();
                if (config.UseProperties) {

                    WritePropertyWithGetterSetter(config, sw, prefix, field, shouldDefineNamespace);
                }
                else
                {
                    WriteProperty(config, sw, prefix, field, shouldDefineNamespace);
                }


            }
            //Constructor
            WriteDataConstructor(config, sw, type, prefix);
            //ToService function
            WriteToServiceFunction(sw, type, prefix);


            //End of class
            sw.WriteLine(prefix + "}");
            //Return carriage for end of file
            sw.WriteLine(Environment.NewLine);
        }

        private void WriteProperty(IJsonClassGeneratorConfig config, TextWriter sw, string prefix, FieldInfo field, bool shouldDefineNamespace)
        {
            sw.WriteLine(prefix + "    " + field.JsonMemberName + (IsNullable(field.Type.Type) ? "?" : "") + ": " +
                               (shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty) + GetTypeName(field.Type, config) + ";");
        }

        private void WritePropertyWithGetterSetter(IJsonClassGeneratorConfig config, TextWriter sw, string prefix, FieldInfo field, bool shouldDefineNamespace)
        {
            sw.WriteLine(prefix + "    private _" + field.JsonMemberName + (IsNullable(field.Type.Type) ? "?" : "") + ": " +
                               (shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty) + GetTypeName(field.Type, config) + ";");

            WriteGetter(config, sw, prefix, field, shouldDefineNamespace);
          
            WriteSetter(config, sw, prefix, field, shouldDefineNamespace);
         
        }
        /// <summary>
        /// Generates a setter for the field
        ///
        /// Example:
        /// set balance(value: number)
        /// {
        ///     this._balance = value;
        /// }
        /// </summary>
        private void WriteSetter(IJsonClassGeneratorConfig config, TextWriter sw, string prefix, FieldInfo field, bool shouldDefineNamespace)
        {
            StringBuilder line = new StringBuilder();
            line.Append(prefix + "    set ");
            line.Append(field.JsonMemberName);
            line.Append("(value");
            line.Append(IsNullable(field.Type.Type) ? "?" : string.Empty);
            line.Append(": ");
            line.Append((shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty));
            line.Append(GetTypeName(field.Type, config));
            line.Append(") {");
            sw.WriteLine(line.ToString());

            line = new StringBuilder();
            line.Append(prefix + "        this._");
            line.Append(field.JsonMemberName);
            line.Append(" = value;");
            sw.WriteLine(line.ToString());
            sw.WriteLine(prefix + "    }");

        }
        /// <summary>
        /// Generates a getter function for the field
        /// 
        /// Example:
        /// get balance(): number {
        ///     return this._balance;
        /// }
        /// </summary>
        private void WriteGetter(IJsonClassGeneratorConfig config, TextWriter sw, string prefix, FieldInfo field, bool shouldDefineNamespace)
        {
            StringBuilder line = new StringBuilder();
            line.Append(prefix + "    get ");
            line.Append(field.JsonMemberName);
            line.Append("(): ");
            line.Append((shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty));
            line.Append(GetTypeName(field.Type, config));
            line.Append(" {");
            sw.WriteLine(line.ToString());

            line = new StringBuilder();
            line.Append(prefix + "        return this._");
            line.Append(field.JsonMemberName);
            line.Append(";");
            sw.WriteLine(line.ToString());
            sw.WriteLine(prefix + "    }");
        }

        /// <summary>
        /// The constructor is based on a nullable argument (data) of any type 
        /// wich has the same propierties and types of the class (or a subset of them) and provides a new instance with all of them mapped
        /// </summary>
        private void WriteDataConstructor(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type, string prefix)
        {
            sw.WriteLine(Environment.NewLine);
            sw.WriteLine(prefix + "constructor(data?) {");
            foreach (var field in type.Fields)
            {
                var shouldDefineNamespace = type.IsRoot && config.SecondaryNamespace != null && config.Namespace != null && (field.Type.Type == JsonTypeEnum.Object || (field.Type.InternalType != null && field.Type.InternalType.Type == JsonTypeEnum.Object));

                //If the type is nullable the object must be null by default
                if (IsNullable(field.Type.Type))
                {
                    
                    sw.WriteLine(prefix + "        this." + field.JsonMemberName + " = data && data." + field.JsonMemberName +
                     " || null;");
                }
                else
                {

                    //In case of non nullable objects, it will provide a proper default value
                    var defaultValue = GetDefaultValue(config, field, shouldDefineNamespace);

                    sw.WriteLine(prefix + "        this." + field.JsonMemberName + " = data && data." + field.JsonMemberName +
                        " || " + defaultValue + ";");
                }

            }
            sw.WriteLine(prefix + "    }");
            sw.WriteLine();
        }

        private string GetDefaultValue(IJsonClassGeneratorConfig config, FieldInfo field, bool shouldDefineNamespace)
        {
            string defaultValue = "";
            switch (field.Type.Type)
            {
                case JsonTypeEnum.String:
                    defaultValue = "''";
                    break;
                case JsonTypeEnum.Float:
                case JsonTypeEnum.Integer:
                case JsonTypeEnum.Long:
                    //For numeric types the default value will be zero
                    defaultValue = "0";
                    break;
                default:
                    // The property type is not primitive, so it will provide an instance of the class
                    defaultValue = " new " + (shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty)
                       + GetTypeName(field.Type, config) + "()";
                    break;
            }

            return defaultValue;
        }

        /// <summary>
        /// This function will provide a generic copy of the current object instance
        /// </summary>
        private void WriteToServiceFunction(TextWriter sw, JsonType type, string prefix)
        {
            sw.WriteLine(Environment.NewLine);
            sw.WriteLine(prefix + "public toService() {");
            
            sw.WriteLine(prefix + "        const obj: any = new Object;");
            foreach (var field in type.Fields)
            {
                
                sw.WriteLine(prefix + "        obj." + field.JsonMemberName + " = this." + field.JsonMemberName +
                    ";");


            }
            
            sw.WriteLine(prefix + "        return obj;");
            sw.WriteLine(prefix + "    }");
        }
        private bool IsNullable(JsonTypeEnum type)
        {
            return
                type == JsonTypeEnum.NullableBoolean ||
                type == JsonTypeEnum.NullableDate ||
                type == JsonTypeEnum.NullableFloat ||
                type == JsonTypeEnum.NullableInteger ||
                type == JsonTypeEnum.NullableLong ||
                type == JsonTypeEnum.NullableSomething;
        }

        public void WriteFileStart(IJsonClassGeneratorConfig config, TextWriter sw)
        {
            foreach (var line in JsonClassGenerator.FileHeader)
            {
                sw.WriteLine("// " + line);
            }
            sw.WriteLine();
        }

        public void WriteFileEnd(IJsonClassGeneratorConfig config, TextWriter sw)
        {
        }

        private string GetNamespace(IJsonClassGeneratorConfig config, bool root)
        {
            return root ? config.Namespace : (config.SecondaryNamespace ?? config.Namespace);
        }

        public void WriteNamespaceStart(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
            if (GetNamespace(config, root) != null)
            {

                sw.WriteLine("module " + GetNamespace(config, root) + " {");
                sw.WriteLine();
            }
        }

        public void WriteNamespaceEnd(IJsonClassGeneratorConfig config, TextWriter sw, bool root)
        {
            if (GetNamespace(config, root) != null)
            {
                sw.WriteLine("}");
                sw.WriteLine();
            }
        }

    }
}
