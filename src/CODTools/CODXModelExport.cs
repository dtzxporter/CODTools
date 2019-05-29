using Autodesk.Maya.OpenMaya;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CODTools
{
    public class CODXModelExport : MPxFileTranslator
    {
        public override string defaultExtension()
        {
            // Default extension
            return "XMODEL_EXPORT";
        }

        public override bool haveReadMethod()
        {
            return false;
        }

        public override bool haveWriteMethod()
        {
            return true;
        }

        public override MPxFileTranslator.MFileKind identifyFile(MFileObject file, string buffer, short bufferLen)
        {
            // It's our file
            if (file.fullName.ToUpper().EndsWith(".XMODEL_EXPORT"))
                return MFileKind.kIsMyFileType;

            // Failed
            return MFileKind.kNotMyFileType;
        }

        public override void writer(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            // Prepare to export, pass it off
            if (file.fullName.ToUpper().EndsWith(".XMODEL_EXPORT"))
            {
                // Parse settings
                bool ExportSiegeModel = false;
                string Cosmetic = string.Empty;

                var SplitSettings = optionsString.Trim().Split(';');
                foreach (var Setting in SplitSettings)
                {
                    if (string.IsNullOrWhiteSpace(Setting))
                        continue;

                    var SettingValue = Setting.Split('=');
                    if (SettingValue.Length < 2)
                        continue;

                    if (SettingValue[0] == "exportsiege")
                        ExportSiegeModel = (SettingValue[1] == "1");
                    else if (SettingValue[0] == "cosmeticroot")
                        Cosmetic = CODXModel.CleanNodeName((SettingValue[1].Trim()));
                }

                // Export the model
                CODXModel.ExportXModel(file.fullName, CODXModel.XModelType.Export, ExportSiegeModel, Cosmetic);
            }
        }

        public override void reader(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            throw new NotImplementedException("We only support support writer not reader");
        }
    }

    public class CODXModelBin : MPxFileTranslator
    {
        public override string defaultExtension()
        {
            // Default extension
            return "XMODEL_BIN";
        }

        public override bool haveReadMethod()
        {
            return false;
        }

        public override bool haveWriteMethod()
        {
            return true;
        }

        public override MPxFileTranslator.MFileKind identifyFile(MFileObject file, string buffer, short bufferLen)
        {
            // It's our file
            if (file.fullName.ToUpper().EndsWith(".XMODEL_BIN"))
                return MFileKind.kIsMyFileType;

            // Failed
            return MFileKind.kNotMyFileType;
        }

        public override void writer(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            // Prepare to export, pass it off
            if (file.fullName.ToUpper().EndsWith(".XMODEL_BIN"))
            {
                // Parse settings
                bool ExportSiegeModel = false;
                string Cosmetic = string.Empty;

                var SplitSettings = optionsString.Trim().Split(';');
                foreach (var Setting in SplitSettings)
                {
                    if (string.IsNullOrWhiteSpace(Setting))
                        continue;

                    var SettingValue = Setting.Split('=');
                    if (SettingValue.Length < 2)
                        continue;

                    if (SettingValue[0] == "exportsiege")
                        ExportSiegeModel = (SettingValue[1] == "1");
                    else if (SettingValue[0] == "cosmeticroot")
                        Cosmetic = CODXModel.CleanNodeName((SettingValue[1].Trim()));
                }

                // Export the model
                CODXModel.ExportXModel(file.fullName, CODXModel.XModelType.Bin, ExportSiegeModel, Cosmetic);
            }
        }

        public override void reader(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            throw new NotImplementedException("We only support support writer not reader");
        }
    }
}
