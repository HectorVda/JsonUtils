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


                sw.WriteLine(prefix + "    " + field.JsonMemberName + (IsNullable(field.Type.Type) ? "?" : "") + ": " +
                    (shouldDefineNamespace ? config.SecondaryNamespace + "." : string.Empty) + GetTypeName(field.Type, config) + ";");
            }
            //Constructor
            WriteDataConstructor(config, sw, type, prefix);
            //ToService function
            WriteToServiceFunction(config, sw, type, prefix);


            //End of class
            sw.WriteLine(prefix + "}");
            //Return carriage for end of file
            sw.WriteLine(Environment.NewLine);
        }
        /// <summary>
        /// The constructor is based on a nullable argument (data) of any type 
        /// wich has the same propierties and types of the class (or a subset of them) and provides a new instance with all of them mapped
        /// </summary>
        private void WriteDataConstructor(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type, string prefix)
        {
            sw.WriteLine(Environment.NewLine);
            sw.WriteLine(prefix + "constructor(data?){");
            foreach (var field in type.Fields)
            {
                var shouldDefineNamespace = type.IsRoot && config.SecondaryNamespace != null && config.Namespace != null && (field.Type.Type == JsonTypeEnum.Object || (field.Type.InternalType != null && field.Type.InternalType.Type == JsonTypeEnum.Object));

                //If the type is nullable the object must be null by default
                if (IsNullable(field.Type.Type))
                {
                    //"this.property = data && data.property || null;"
                    sw.WriteLine(prefix + "        this." + field.JsonMemberName + " = data && data." + field.JsonMemberName +
                     " || null;");
                }
                else
                {
                    
                    
                    var defaultValue = "";
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



                    sw.WriteLine(prefix + "        this." + field.JsonMemberName + " = data && data." + field.JsonMemberName +
                        " || " + defaultValue + ";");
                }

            }
            sw.WriteLine(prefix + "    }");
        }
        /// <summary>
        /// This function will provide a generic copy of the current object instance
        /// </summary>
        private void WriteToServiceFunction(IJsonClassGeneratorConfig config, TextWriter sw, JsonType type, string prefix)
        {
            sw.WriteLine(Environment.NewLine);
            sw.WriteLine(prefix + "public toService(){");
            
            sw.WriteLine(prefix + "        let obj : any = new Object;");
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
