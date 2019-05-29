using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Debugger
{
    class Program
    {
        static void Main(string[] args)
        {
            var d = CODTools.PluginInfo.animExportDefaultOptions;
            var ObjectType = Type.GetType("Autodesk.Maya.OpenMaya.MFn, openmayacs");
            var EnumType = Enum.Parse(ObjectType.GetNestedType("Type"), "kNamedObject");
            var Val = Convert.ToInt32(EnumType);
            Console.Write("");
        }
    }
}
